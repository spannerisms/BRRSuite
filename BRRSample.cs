namespace BRRSuite;

/// <summary>
/// Contains sample data and metadata about a Bit Rate Reduction sample.
/// </summary>
public sealed class BRRSample {
	/// <summary>
	/// The preferred file extension for a raw BRR sample.
	/// </summary>
	public const string Extension = "brr";

	/// <summary>
	/// The preferred file extension for a headered BRR sample.
	/// </summary>
	public const string HeaderedExtension = "brh";

	// a very generous limit to the size of the file.
	private const int MaxSize = 0xFF00;
	private const int MaxBlocks = MaxSize / BrrBlockSize;

	/// <summary>
	/// Gets or sets the instrument name associated with this sample.
	/// The name should be exactly 24 characters in length (padded with spaces if necessary) and only use ASCII characters.
	/// These are enforced by the property setter.
	/// </summary>
	public string InstrumentName {
		get => _name;
		set {
			// sanitize to ascii
			value = string.Concat(value.Where(c => char.IsAscii(c) && !char.IsControl(c)));

			// enforce length
			_name = value.Length switch {
				SuiteSample.InstrumentNameLength => value,
				< SuiteSample.InstrumentNameLength => value.PadRight(SuiteSample.InstrumentNameLength, SuiteSample.InstrumentNamePadChar),
				> SuiteSample.InstrumentNameLength => value[..SuiteSample.InstrumentNameLength]
			};
		}
	}

	// default name
	private string _name = new(SuiteSample.InstrumentNamePadChar, SuiteSample.InstrumentNameLength);

	/// <summary>
	/// Gets or sets the block within this sample that will be looped to when it reaches the end.
	/// An invalid value will be changed to -1.
	/// </summary>
	public int LoopBlock {
		get => _loopblock;
		set {
			// prevent invalid loop points and standardize negative values
			if (value < 0 || value >= BlockCount) {
				_loopblock = NoLoop;
				return;
			}
			_loopblock = value;
		}
	}

	private int _loopblock = NoLoop;

	/// <summary>
	/// Gets the location of this sample's loop point as an offset in bytes.
	/// </summary>
	public int LoopPoint => _loopblock switch {
		< 0 => NoLoop,
		_ => _loopblock * BrrBlockSize
	};

	/// <summary>
	/// Gets whether or not this sample should loop, based on the existence of a loop point.
	/// </summary>
	public bool IsLooping => _loopblock >= 0;

	private bool HasLoopFlag => (_data[^BrrBlockSize] & LoopFlag) == LoopFlag;

	/// <summary>
	/// Gets the resampling ratio this sample was encoded at, relative its original sample. Defaults to 1.0.
	/// </summary>
	public decimal ResampleRatio { get; internal set; } = 1.0M;

	/// <summary>
	/// Gets or sets the target frequency this sample was encoded at. Defaults to 32000.
	/// This value should not be zero or negative.
	/// </summary>
	public int EncodingFrequency {
		get => _encodingFrequency;
		set {
			if (value < 1) {
				throw new ArgumentOutOfRangeException(null, "Encoding frequency cannot be zero or negative");
			}
			_encodingFrequency = value;
		}
	}
	private int _encodingFrequency = DefaultFrequency;

	/// <summary>
	/// Gets the length of this sample in bytes.
	/// </summary>
	public int Length => BlockCount * BrrBlockSize;

	/// <summary>
	/// Gets the length of this sample in blocks.
	/// </summary>
	public int BlockCount { get; }

	/// <summary>
	/// Gets the numbers of samples in this BRR file.
	/// </summary>
	public int SampleCount => BlockCount * 16;

	/// <summary>
	/// Gets or sets the SPC sound system's DSP VxPITCH value that corresponds to an audible frequency for a C note.
	/// <list type="bullet">
	/// <item>The value <c>0x1000</c> corresponds to no frequency change on playback; i.e., a 32kHz sample being played at 32kHz.</item>
	/// <item>The value <c>0x0000</c> indicates the frequency of C for this sample is not known.</item>
	/// <item>Values of <c>0x4000</c> and higher are considered invalid. Removal of these bits is enforced by the property setter.</item>
	/// </list>
	/// </summary>
	public int VxPitch {
		get => _vxpitch;
		set => _vxpitch = (ushort) (value & 0x3FFF);
	}

	private ushort _vxpitch = 0x1000;

	/// <summary>
	/// Gets the raw data stream of this BRR sample.
	/// </summary>
	public byte[] Data => _data;

	// Why are you reading this? Can't you see that it says "private"?
	private readonly byte[] _data;

	/// <summary>
	/// Gets or sets the data at a given index.
	/// </summary>
	public byte this[Index i] {
		get => _data[i];
		set => _data[i] = value;
	}

	/// <summary>
	/// Creates a new instance of the <see cref="BRRSample"/> class with the specified number of blocks.
	/// The total size of the samples data will be <paramref name="blocks"/> * 9.
	/// </summary>
	/// <param name="blocks">The number of 72-bit blocks to alllocate for this sample.</param>
	/// <exception cref="ArgumentException">If the number of blocks requested is 0, negative, or too many blocks.</exception>
	public BRRSample(int blocks) {
		ThrowIfBadBlocks(blocks);

		BlockCount = blocks;

		_data = new byte[blocks * BrrBlockSize];
	}

	/// <summary>
	/// Creates a new instance of the <see cref="BRRSample"/> class with a copy of the given data.
	/// </summary>
	/// <param name="data">The BRR data to use for this sample. The length of this array should be a multiple of 9.</param>
	/// <exception cref="ArgumentException">If the input array is empty or too large.</exception>
	public BRRSample(byte[] data) {
		if ((data.Length % BrrBlockSize) is not 0) {
			throw new ArgumentException("The input array is not a multiple of 9 bytes in length.", nameof(data));
		}

		BlockCount = data.Length / BrrBlockSize;

		ThrowIfBadBlocks(BlockCount);

		_data = [.. data];
	}

	/// <summary>
	/// Checks if the number of blocks being encoded is valid.
	/// Throws an exception if it is not.
	/// </summary>
	/// <param name="blocks">The number of blocks being encoded.</param>
	/// <exception cref="ArgumentException"></exception>
	private static void ThrowIfBadBlocks(int blocks) {
		switch (blocks) {
			case 0:
				throw new ArgumentException("Cannot create a BRR sample with 0 blocks.", nameof(blocks));

			case < 0:
				throw new ArgumentException("Cannot create a BRR sample with a negative number of blocks.", nameof(blocks));

			case >= MaxBlocks:
				throw new ArgumentException($"Cannot create a BRR sample with more than {MaxBlocks - 1} blocks.", nameof(blocks));
		}
	}

	/// <summary>
	/// Gets a segment of data corresponding to the 9 bytes of the requested block.
	/// </summary>
	/// <param name="block">Index of block to cover.</param>
	/// <returns>A <see cref="Span{T}"/> of type <see langword="byte"/> over the specified block.</returns>
	/// <exception cref="IndexOutOfRangeException">If the index requested is negative or more than the number of blocks in the sample.</exception>
	public Span<byte> GetBlockSpan(int block) {
		ThrowIfOutOfRangeBlock(block);

		return new(_data, block * BrrBlockSize, BrrBlockSize);
	}

	/// <inheritdoc cref="GetBlockSpan(int)" path="/summary"/>
	/// <inheritdoc cref="GetBlockSpan(int)" path="/param"/>
	/// <returns>A <see cref="BRRBlock"/> ref struct over the specified block.</returns>
	/// <inheritdoc cref="GetBlockSpan(int)" path="/exception"/>
	public BRRBlock GetBlock(int block) {
		ThrowIfOutOfRangeBlock(block);

		return new(ref _data[block * BrrBlockSize]);
	}

	/// <summary>
	/// Throw if bad.
	/// </summary>
	private void ThrowIfOutOfRangeBlock(int block) {
		if (block > BlockCount || block < 0) {
			throw new ArgumentOutOfRangeException();
		}
	}

	/// <summary>
	/// Adjusts the header of the final block to include the end of sample flag
	/// and corrects the loop flag based on the existence of a loop point.
	/// </summary>
	public void FixEndBlock() {
		ref byte lastHeader = ref _data[^BrrBlockSize];
		lastHeader |= EndFlag;
		
		if (IsLooping) {
			lastHeader |= LoopFlag;
		} else {
			lastHeader &= LoopFlagOff;
		}
	}

	/// <summary>
	/// Throws an error if the given sample has issues that cannot be fixed programmatically.
	/// </summary>
	/// <param name="brr">The sample to validate.</param>
	/// <exception cref="BRRConversionException">An unresolvable issue was found.</exception>
	private static void ThrowIfUnresolvableIssues(BRRSample brr) {
		BRRDataIssue issues = ValidateBRR(brr);

		if (issues.HasFlag(BRRDataIssue.Unresolvable)) {
			throw new BRRConversionException("There were unresolvable issues with this object's data.");
		}

		// Force validation to run on the name as a precaution
		brr.InstrumentName = brr._name;

		if (brr.InstrumentName.Length is not SuiteSample.InstrumentNameLength) {
			throw new BRRConversionException($"Something went terribly wrong with the name: \"{brr.InstrumentName}\" (length: {brr.InstrumentName.Length})");
		}
	}

	/// <summary>
	/// Exports this sample's data to a raw BRR file.
	/// The extension of the exported file will be added or changed to the preferred extension defined by <see cref="Extension"/>.
	/// </summary>
	/// <param name="path">The relative or absolute path this sample should be saved to.</param>
	/// <exception cref="BRRConversionException">Data being exported is malformed.</exception>
	public void ExportRaw(string path) {
		ThrowIfUnresolvableIssues(this);

		path = Path.ChangeExtension(path, Extension);

		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

		fs.Write(_data);
		fs.Flush();
	}

	/// <summary>
	/// Exports this sample's data to a raw BRR file with a loop offset header.
	/// The extension of the exported file will be added or changed to the preferred extension defined by <see cref="HeaderedExtension"/>.
	/// </summary>
	/// <inheritdoc cref="ExportRaw(string)" path="/param" />
	/// <inheritdoc cref="ExportRaw(string)" path="/exception" />
	public void ExportHeadered(string path) {
		ThrowIfUnresolvableIssues(this);

		path = Path.ChangeExtension(path, HeaderedExtension);
		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

		int loopPoint = IsLooping ? LoopPoint : BlockCount;

		fs.WriteByte((byte) loopPoint);
		fs.WriteByte((byte) (loopPoint >> 8));

		fs.Write(_data);
		fs.Flush();
	}

	/// <summary>
	/// Creates a new <see cref="BRRSample"/> from data contained in a BRR Suite Sample file.
	/// </summary>
	/// <param name="path">The absolute or relative path to the file.</param>
	/// <returns>A new <see cref="BRRSample"/> object containing the data.</returns>
	/// <exception cref="BRRConversionException"></exception>
	public static BRRSample ReadSuiteFile(string path) {
		using var rd = new FileStream(path, FileMode.Open, FileAccess.Read);

		// Verify
		var data = new byte[(int) rd.Length];
		rd.Read(data, 0, data.Length);

		rd.Close();

		if (!VerifySuiteSample(data, out string? msg)) {
			throw new BRRConversionException(msg);
		}

		int blocks = ReadShort(SuiteSample.SampleBlocksLocation);

		string name = new(data[SuiteSample.InstrumentNameLocation..SuiteSample.InstrumentNameEnd].Select(c=>(char) c).ToArray());

		BRRSample ret = new(blocks){
			InstrumentName = name,
			LoopBlock = ReadShort(SuiteSample.LoopBlockLocation),
			EncodingFrequency = ReadInt(SuiteSample.EncodingFrequencyLocation),
			VxPitch = ReadShort(SuiteSample.PitchLocation),
		};

		Array.Copy(data, SuiteSample.SamplesDataLocation, ret._data, 0, data.Length-SuiteSample.SamplesDataLocation);

		return ret;

		int ReadShort(int i) {
			return data[i] | (data[i + 1] << 8);
		}

		int ReadInt(int i) {
			return data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24);
		}
	}

	/// <summary>
	/// Tests a stream of data for a properly-formed BRR Suite Sample file header and valid BRR data.
	/// </summary>
	/// <param name="data">The data to validate.</param>
	/// <param name="message">A string containing a message about where, if at all, the data was deemed invalid.</param>
	/// <returns><see langword="true"/> if the file is valid; otherwise <see langword="false"/>.</returns>
	public static bool VerifySuiteSample(byte[] data, out string? message) {
		// check for a valid file size
		int length = data.Length;

		ushort rd16, rd16b;

		if (length < (SuiteSample.SamplesDataLocation + 9)) {
			message = $"File is too small to be a {SuiteSample.FormatName} file.";
			return false;
		}

		// check the signatures
		string? badMSG = TestSubstring(SuiteSample.FormatSignatureLocation, SuiteSample.FormatSignature);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		badMSG = TestSubstring(SuiteSample.MetaBlockSignatureLocation, SuiteSample.MetaBlockSignature);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		badMSG = TestSubstring(SuiteSample.DataBlockSignatureLocation, SuiteSample.DataBlockSignature);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		int samplesLength = length - SuiteSample.SamplesDataLocation;

		if ((samplesLength % BrrBlockSize) != 0) {
			message = $"Sample data is not a multiple of {BrrBlockSize} bytes: {samplesLength}";
			return false;
		}

		// checksums
		rd16 = GetChecksum(data[SuiteSample.SamplesDataLocation..]);
		rd16b = ReadShort(SuiteSample.ChecksumLocation);
		if (rd16 != rd16b) {
			message = $"Bad checksum: {rd16b:X4} | Expected: {rd16:X4}";
			return false;
		}

		rd16b = ReadShort(SuiteSample.ChecksumComplementLocation);
		rd16 ^= 0xFFFF;
		if (rd16 != rd16b) {
			message = $"Bad checksum complement: {rd16b:X4} | Expected: {rd16:X4}";
			return false;
		}

		
		rd16 = ReadShort(SuiteSample.SampleLengthLocation);
		if (samplesLength != rd16) {
			message = $"Incorrect length: {rd16} | Expected: {samplesLength}";
			return false;
		}

		rd16 = ReadShort(SuiteSample.SampleBlocksLocation);

		if ((rd16 * BrrBlockSize) != samplesLength) {
			message = $"Incorrect block count: {rd16} | Expected: {samplesLength / BrrBlockSize}";
			return false;
		}

		rd16 = ReadShort(SuiteSample.LoopBlockLocation);
		rd16b = ReadShort(SuiteSample.LoopPointLocation);

		if ((rd16 * BrrBlockSize) != rd16b) {
			message = $"Loop block and loop point do not match: b:[{rd16}, {rd16* BrrBlockSize}] | l:[{rd16b / BrrBlockSize},{rd16b}]";
			return false;
		}

		byte loopType = data[SuiteSample.LoopTypeLocation];

		if (rd16b >= length && loopType is SuiteSample.LoopingSample) {
			message = $"Invalid loop point: {rd16b}";
			return false;
		}

		bool hasLoopFlag = (data[^BrrBlockSize] & LoopFlag) is LoopFlag;

		switch (loopType, hasLoopFlag) {
			case (SuiteSample.NonloopingSample, true):
			case (SuiteSample.LoopingSample, false):
			case (SuiteSample.ForeignLoopingSample, false):
				message = $"Loop type does not match final block header.";
				return false;
		}

		if ((data[^BrrBlockSize] & EndFlag) is 0) {
			message = $"The sample data does not contain an end flag on the final block header.";
			return false;
		}

		samplesLength -= BrrBlockSize;

		for (int i = 0; i < samplesLength; i += BrrBlockSize) {
			if ((data[SuiteSample.SamplesDataLocation + i] & EndFlag) is EndFlag) {
				message = $"The sample data contains too many end block flags.";
				return false;
			}
		}

		// Valid file!
		message = null;

		return true;

		string? TestSubstring(int start, string test) {
			var s = data[start..(start + 4)];
			string result = new(s.Select(o => (char) o).ToArray());

			if (result == test) {
				return null;
			}

			return $"Bad signature at {start}: {result} | Expected: {test}";
		}

		ushort ReadShort(int i) {
			return (ushort) (data[i] | (data[i + 1] << 8));
		}

	}

	/// <summary>
	/// Exports this sample's data to a BRR Suite Sample file.
	/// The extension of the exported file will be added or changed to the preferred extension defined by <see cref="SuiteSample.Extension"/>.
	/// </summary>
	/// <inheritdoc cref="ExportRaw(string)" path="/param" />
	/// <inheritdoc cref="ExportRaw(string)" path="/exception" />
	public void ExportSuiteSample(string path) {
		ThrowIfUnresolvableIssues(this);

		byte[] header = new byte[SuiteSample.SamplesDataLocation];

		// header
		WriteString(SuiteSample.FormatSignature, SuiteSample.FormatSignatureLocation);

		ushort cksm = GetChecksum(this);
		WriteShort(cksm, SuiteSample.ChecksumLocation);
		WriteShort(~cksm, SuiteSample.ChecksumComplementLocation);

		// meta block
		WriteString(SuiteSample.MetaBlockSignature, SuiteSample.MetaBlockSignatureLocation);
		WriteString(InstrumentName, SuiteSample.InstrumentNameLocation);
		WriteShort(VxPitch, SuiteSample.PitchLocation);
		WriteInt(EncodingFrequency, SuiteSample.EncodingFrequencyLocation);

		// 7 unused bytes

		// data block
		WriteString(SuiteSample.DataBlockSignature, SuiteSample.DataBlockSignatureLocation);
		header[SuiteSample.LoopTypeLocation] = IsLooping ? SuiteSample.LoopingSample : SuiteSample.NonloopingSample;

		int loopBlock = IsLooping ? LoopBlock : 0x0000;

		WriteShort(loopBlock, SuiteSample.LoopBlockLocation);
		WriteShort(loopBlock * BrrBlockSize, SuiteSample.LoopPointLocation);
		WriteShort(BlockCount, SuiteSample.SampleBlocksLocation);
		WriteShort(Data.Length, SuiteSample.SampleLengthLocation);

		path = Path.ChangeExtension(path, SuiteSample.Extension);
		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

		fs.Write(header);
		fs.Write(Data);
		fs.Flush();

		void WriteString(string s, int i) {
			foreach (var c in s) {
				header[i++] = (byte) c;
			}
		}

		void WriteInt(int w, int i) {
			header[i + 0] = (byte) w;
			header[i + 1] = (byte) (w >> 8);
			header[i + 2] = (byte) (w >> 16);
			header[i + 3] = (byte) (w >> 24);
		}

		void WriteShort(int w, int i) {
			w &= 0xFFFF;
			header[i + 0] = (byte) w;
			header[i + 1] = (byte) (w >> 8);
		}

	}

	/// <summary>
	/// Tests the given sample data for BRR validity. This method expects unheadered data.
	/// </summary>
	/// <param name="data">The data to validate.</param>
	/// <returns>A <see cref="BRRDataIssue"/> enum flagging any problems found with the data.</returns>
	public static BRRDataIssue ValidateBRR(byte[] data) {
		return ValidateBRRDataBasic(data);
	}

	/// <inheritdoc cref="ValidateBRR(byte[])"/>
	public static BRRDataIssue ValidateBRR(BRRSample data) {
		BRRDataIssue ret = BRRDataIssue.None;

		if (data.Length != data._data.Length) {
			ret |= BRRDataIssue.WrongBlockCount | BRRDataIssue.Unresolvable;
		}

		ret |= ValidateBRRDataBasic(data._data);

		if (data.LoopBlock >= data.BlockCount) {
			ret |= BRRDataIssue.OutOfRangeLoopPoint;

			if (data.HasLoopFlag) {
				ret |= BRRDataIssue.Unresolvable;
			}
		}

		if (data.HasLoopFlag) {
			// if there's a loop flag but no specified block, then there's an issue
			if (!data.IsLooping) {
				ret |= BRRDataIssue.Unresolvable | BRRDataIssue.MissingLoopPoint;
			}
		} else {
			// if there's no loop flag, assume an in-range loop point means there should in fact be a loop
			if (data.LoopBlock >= 0 && data.LoopBlock < data.BlockCount) {
				ret |= BRRDataIssue.MissingLoopFlag;
			}
		}

		return ret;
	}

	/// <summary>
	/// Tests the given sample data for BRR validity with automatic finding of loop point if a header be present.
	/// </summary>
	/// <param name="data"><inheritdoc cref="ValidateBRR(byte[])" path="/param[@name='data']"/></param>
	/// <param name="loopBlock">If the given data passes all validity checks and has a 16-bit loop offset header,
	/// this will contain the index of the BRR block defined by that offset.
	/// If the data is invalid, no header is present, or the sample should not loop, this will contain -1.
	/// </param>
	/// <inheritdoc cref="ValidateBRR(byte[])" path="/returns"/>
	public static BRRDataIssue ValidateBRRWithHeader(byte[] data, out int loopBlock) {
		// Validate alignment
		int len = data.Length;
		int dataStart = len % BrrBlockSize;

		loopBlock = NoLoop;

		// no header
		if (dataStart is 0) {
			return ValidateBRR(data);
		}

		// bad size
		if (dataStart is not 2) {
			return BRRDataIssue.Unresolvable | BRRDataIssue.WrongAlignmentForHeadered | BRRDataIssue.WrongAlignment;
		}

		// do quick bounds check before passing to the basic validater
		if (len < 9) {
			return BRRDataIssue.Unresolvable | BRRDataIssue.DataTooSmall;
		}

		// Get loop point if from header
		int loopStart = data[0] | (data[1] << 8);

		// Perform validity checks
		BRRDataIssue ret = ValidateBRRDataWithLoop(new(data, 2, len - 2), loopStart);

		// give up if there are unresolvable issues
		if (ret.HasFlag(BRRDataIssue.Unresolvable)) {
			return ret;
		}

		bool hasLoopFlag = (data[^BrrBlockSize] & LoopFlag) != LoopFlag;

		if (hasLoopFlag) {
			loopBlock = loopStart / BrrBlockSize;
		}

		// Valid file
		return ret;
	}

	/// <summary>
	/// Performs a basic validity check on data for BRR compliance.
	/// </summary>
	/// <inheritdoc cref="ValidateBRR(byte[])" path="/returns"/>
	private static BRRDataIssue ValidateBRRDataBasic(Span<byte> data) {
		int len = data.Length;

		// Files that are too small are invalid
		if (len < BrrBlockSize) {
			return BRRDataIssue.Unresolvable | BRRDataIssue.DataTooSmall | BRRDataIssue.WrongAlignment;
		}

		if (len >= MaxSize) {
			return BRRDataIssue.DataTooLarge;
		}

		// must be a multiple of 9
		if ((len % BrrBlockSize) is not 0) {
			return BRRDataIssue.Unresolvable | BRRDataIssue.WrongAlignment;
		}

		BRRDataIssue ret = BRRDataIssue.None;

		// need end flag on last block
		if ((data[^BrrBlockSize] & EndFlag) != EndFlag) {
			ret |= BRRDataIssue.MissingEndFlag;
		}

		// make sure no other header has an end flag
		len -= BrrBlockSize;

		for (int i = 0; i < len; i += BrrBlockSize) {
			if ((data[i] & EndFlag) is EndFlag) {
				ret |= BRRDataIssue.EarlyEndFlags;
				break;
			}
		}

		return ret;
	}

	/// <summary>
	/// Performs a validity check on data for BRR compliance with acknowledgement of a loop point.
	/// </summary>
	/// <inheritdoc cref="ValidateBRR(byte[])" path="/returns"/>
	private static BRRDataIssue ValidateBRRDataWithLoop(Span<byte> data, int loopPoint) {
		BRRDataIssue ret = ValidateBRRDataBasic(data);

		// give up if there are unresolvable issues
		if (ret.HasFlag(BRRDataIssue.Unresolvable)) {
			return ret;
		}

		// check for a loop flag from the final block
		bool hasLoopFlag = (data[^BrrBlockSize] & LoopFlag) != LoopFlag;

		// Check if loop point is aligned to a multiple of 9
		if ((loopPoint % BrrBlockSize) is not 0) {
			ret |= BRRDataIssue.MisalignedLoopPoint;

			// Unresolvable if there's a loop
			if (hasLoopFlag) {
				ret |= BRRDataIssue.Unresolvable;
			}
		}

		// check if the loop point is in range
		if (loopPoint >= data.Length) {
			ret |= BRRDataIssue.OutOfRangeLoopPoint;

			// Unresolvable if there's a loop
			if (hasLoopFlag) {
				ret |= BRRDataIssue.Unresolvable;
			}
		}

		return ret;

	}

	/// <inheritdoc cref="GetChecksum(byte[])"/>
	public static ushort GetChecksum(BRRSample brr) {
		return GetChecksum(brr.Data);
	}

	/// <summary>
	/// Creates a BRR Suite Sample specification checksum for the given sample.
	/// </summary>
	/// <param name="brr">The sample data to checksum.</param>
	/// <returns>The checksum as a <see langword="ushort"/> value.</returns>
	/// <exception cref="BRRConversionException">The file is malformed.</exception>
	public static ushort GetChecksum(byte[] brr) {
		int length = brr.Length;

		if (length == 0) {
			throw new BRRConversionException("Cannot checksum an empty set of samples.");
		}

		if (length % BrrBlockSize != 0) {
			throw new BRRConversionException($"Cannot checksum data whose length is not a multiple of {BrrBlockSize}.");
		}

		// Step 1: Begin with a sum accumulator of 0
		int ret = 0;

		// Step 2: For each block:
		for (int i = 0; i < length; i += BrrBlockSize) {
			// Step 2.1: Reset the block accumulator
			int accum = 0;

			// Step 2.2: Add the 8 bytes of the block, each shifted left by their index within the block minus 1
			for (int j = 1; j < BrrBlockSize; j++) {
				accum += brr[i + j] << (j - 1);
			}

			// Step 2.3: Shift the header byte 4 bits left
			int hbs = brr[i] << 4;

			// Step 2.4: Exclusive OR the shifted header with the block accumulator
			accum ^= hbs;

			// Step 2.5: Add the block accumulator to the sum accumulator
			ret += accum;
		}

		// Step 3: Truncate the sum accumulator to 16-bits and return.
		return (ushort) ret;
	}
}
