// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Container for Bit Rate Reduction sample data.
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
	private const int MaxSize = 0xFFF9;
	private const int MaxBlocks = MaxSize / BRRBlockSize;

	/// <summary>
	/// Gets or sets the block within this sample that will be looped to when it reaches the end.
	/// </summary>
	/// <remarks>
	/// No loop is indicated by <c>-1</c>.
	/// Invalid values will be changed to <c>-1</c>.
	/// </remarks>
	public int LoopBlock {
		get => _loopblock;
		set {
			// prevent invalid loop points and standardize negative values
			if (value < 0 || value >= _blockCount) {
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
	public int LoopPoint => _loopblock < 0 ? NoLoop : _loopblock * BRRBlockSize;

	/// <summary>
	/// Whether or not this sample should loop based on the existence of a loop point.
	/// </summary>
	public bool IsLooping => _loopblock >= 0;

	/// <summary>
	/// Gets the length of this sample in blocks.
	/// </summary>
	public int BlockCount => _blockCount;

	private readonly int _blockCount;

	/// <summary>
	/// Gets the length of this sample in bytes.
	/// </summary>
	public int Length => _blockCount * BRRBlockSize;

	/// <summary>
	/// Gets the numbers of samples in this BRR file.
	/// </summary>
	public int SampleCount => _blockCount * PCMBlockSize;

	// Why are you reading this? Can't you see that it says "private"?
	private readonly byte[] _data;

	/// <summary>
	/// Gets or sets the data byte at a given index.
	/// </summary>
	public byte this[Index index] {
		get => _data[index];
		set => _data[index] = value;
	}

	/// <summary>
	/// Creates a new instance of the <see cref="BRRSample"/> class with the specified number of empty blocks.
	/// </summary>
	/// <param name="blockCount">The number of 72-bit blocks to allocate for this sample.</param>
	/// <exception cref="ArgumentException">If the number of blocks requested is 0, negative, or too many.</exception>
	public BRRSample(int blockCount) {
		ThrowIfBadBlocks(blockCount);

		_blockCount = blockCount;
		_data = new byte[blockCount * BRRBlockSize];
	}

	/// <summary>
	/// Creates a new instance of the <see cref="BRRSample"/> class with a copy of the given data.
	/// </summary>
	/// <param name="data">The BRR data to use for this sample. The length of this data must be a multiple of 9.</param>
	/// <exception cref="ArgumentException">If the input data is empty or too large.</exception>
	public BRRSample(byte[] data) {
		ThrowIfBadInputData(data.Length);

		_blockCount = data.Length / BRRBlockSize;
		_data = [.. data];
	}

	/// <inheritdoc cref="BRRSample(byte[])"/>
	public BRRSample(Span<byte> data) {
		ThrowIfBadInputData(data.Length);

		_blockCount = data.Length / BRRBlockSize;
		_data = [.. data];
	}

	/// <summary>
	/// Throws if not a multiple of 9, then checks for bad block count.
	/// </summary>
	private static void ThrowIfBadInputData(int length) {
		if ((length % BRRBlockSize) is not 0) {
			throw new ArgumentException("The input data is not a multiple of 9 bytes in length.");
		}

		ThrowIfBadBlocks(length / BRRBlockSize);
	}

	/// <summary>
	/// Checks if the number of blocks being encoded is valid.
	/// Throws an exception if it is not.
	/// </summary>
	private static void ThrowIfBadBlocks(int blocks) {
		switch (blocks) {
			case 0:
				throw new ArgumentException("Cannot create a BRR sample with 0 blocks.");

			case < 0:
				throw new ArgumentException("Cannot create a BRR sample with a negative number of blocks.");

			case >= MaxBlocks:
				throw new ArgumentException($"Cannot create a BRR sample with more than {MaxBlocks - 1} blocks.");
		}
	}

	/// <summary>
	/// Gets a segment of data corresponding to the 9 bytes of the requested block.
	/// </summary>
	/// <param name="block">Index of block to cover.</param>
	/// <returns>A <see cref="BRRBlock"/> ref struct over the specified block.</returns>
	/// <exception cref="ArgumentOutOfRangeException">If the index requested is negative or more than the number of blocks in the sample.</exception>
	public BRRBlock GetBlock(int block) {
		ThrowIfOutOfRangeBlock(block);

		return new(ref _data[block * BRRBlockSize]);
	}

	/// <summary>
	/// Throw if bad.
	/// </summary>
	private void ThrowIfOutOfRangeBlock(int block) {
		if (block >= _blockCount || block < 0) {
			throw new ArgumentOutOfRangeException($"Tried to index a block outside of the sample: {block} / {_blockCount}", innerException: null);
		}
	}

	/// <summary>
	/// Corrects the usage of the end and loop flags.
	/// </summary>
	/// <remarks>
	/// The final block header will have its end flag set,
	/// along with the loop flag if a loop point is defined.
	/// Both flags will be removed from all other block headers.
	/// </remarks>
	public void CorrectEndFlags() {
		const byte BothFlagsOff = unchecked((byte) ~(LoopFlag | EndFlag));

		int len = _data.Length;

		// remove from every block, including the final block
		for (int i = 0; i < len; i += BRRBlockSize) {
			_data[i] &= BothFlagsOff;
		}

		// add back to the final block header
		ref byte lastHeader = ref _data[^BRRBlockSize];

		lastHeader |= EndFlag;

		if (IsLooping) {
			lastHeader |= LoopFlag;
		}
	}

	/// <summary>
	/// Returns the raw data of this sample in a new array.
	/// </summary>
	/// <returns>A new <see langword="byte"/>[] with a copy of the underlying data.</returns>
	public byte[] ToArray() {
		return [.. _data];
	}

	/// <summary>
	/// Returns the raw data of this sample covered by a span.
	/// </summary>
	/// <returns>A span covering the raw data.</returns>
	public Span<byte> AsSpan() {
		return _data.AsSpan();
	}

	/// <summary>
	/// Throws an exception if the sample has issues that cannot be fixed programmatically.
	/// </summary>
	/// <exception cref="BRRConversionException">An unresolvable issue was found.</exception>
	internal void ThrowIfUnresolvableIssues() {
		BRRDataIssue issues = Validate();

		if (issues.HasFlag(BRRDataIssue.Unresolvable)) {
			throw new BRRConversionException("There were unresolvable issues with this object's data.");
		}
	}

	/// <summary>
	/// Saves this sample to the given file location.
	/// </summary>
	/// <param name="path">The relative or absolute path this sample should be saved to.</param>
	/// <exception cref="BRRConversionException">Data being saved is malformed.</exception>
	public void Save(string path) {
		ThrowIfUnresolvableIssues();

		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

		fs.Write(_data);
	}

	/// <summary>
	/// Exports this sample's data to a raw BRR file with a loop offset header.
	/// </summary>
	/// <inheritdoc cref="Save(string)" path="/param" />
	/// <inheritdoc cref="Save(string)" path="/exception" />
	public void ExportWithHeader(string path) {
		ThrowIfUnresolvableIssues();

		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

		int loopPoint = IsLooping ? LoopPoint : SampleCount;

		fs.WriteByte((byte) loopPoint);
		fs.WriteByte((byte) (loopPoint >> 8));

		fs.Write(_data);
	}

	/// <summary>
	/// Tests the given data for BRR validity. This method expects unheadered data.
	/// </summary>
	/// <param name="data">The data to validate.</param>
	/// <returns>A <see cref="BRRDataIssue"/> enum flagging any problems found with the data.</returns>
	public static BRRDataIssue ValidateBRRData(byte[] data) {
		return ValidateBRRData(data.AsSpan());
	}

	/// <inheritdoc cref="ValidateBRRData(byte[])"/>
	public static BRRDataIssue ValidateBRRData(Span<byte> data) {
		int len = data.Length;

		BRRDataIssue ret = BRRDataIssue.None;

		if (len < BRRBlockSize) { // Files that are too small are invalid
			ret = BRRDataIssue.BadAlignment | BRRDataIssue.DataTooSmall | BRRDataIssue.Unresolvable;
		} else if (len >= MaxSize) { // Files that are too big are invalid
			ret = BRRDataIssue.DataTooLarge | BRRDataIssue.Unresolvable;
		}

		// must be a multiple of 9
		if ((len % BRRBlockSize) != 0) {
			ret |= BRRDataIssue.BadAlignment | BRRDataIssue.Unresolvable;
		}

		// if we're unresolvable here
		// there's a problem with the size
		// so just give up
		if (ret.HasFlag(BRRDataIssue.Unresolvable)) {
			return ret;
		}

		// need end flag on last block
		if ((data[^BRRBlockSize] & EndFlag) != EndFlag) {
			ret |= BRRDataIssue.MissingEndFlag | BRRDataIssue.UndefinedBehavior;
		}

		// check for potential block 0 issues
		if ((data[0] & FilterMask) != 0) {
			ret |= BRRDataIssue.Block0Filter | BRRDataIssue.UndefinedBehavior;
		}

		//    sample 3 only   + samples 1,2
		if (((data[2] & 0xF0) | data[1]) != 0) {
			ret |= BRRDataIssue.Block0Samples | BRRDataIssue.UndefinedBehavior;
		}

		// check for bad ranges
		for (int i = 0; i < len; i += BRRBlockSize) {
			if (data[i] >= 0xD0) {
				ret |= BRRDataIssue.LargeRange | BRRDataIssue.UndefinedBehavior;
				break;
			}
		}

		// make sure no other header has an end flag
		len -= BRRBlockSize;

		for (int i = 0; i < len; i += BRRBlockSize) {
			if ((data[i] & EndFlag) == EndFlag) {
				ret |= BRRDataIssue.EarlyEndFlags | BRRDataIssue.UndefinedBehavior;
				break;
			}
		}

		return ret;
	}

	/// <summary>
	/// Validates this instance's data and metadata.
	/// </summary>
	/// <inheritdoc cref="ValidateBRRData(byte[])" path="/returns"/>
	public BRRDataIssue Validate() {
		BRRDataIssue ret = ValidateBRRData(_data.AsSpan(), LoopPoint);

		if (Length != _data.Length) {
			ret |= BRRDataIssue.WrongBlockCount | BRRDataIssue.Unresolvable;
		}

		return ret;
	}

	/// <summary>
	/// Tests the given sample data for BRR validity with consideration for a known loop point.
	/// </summary>
	/// <remarks>
	/// If the loop point is unknown, use <see cref="ValidateBRRData(byte[])"/>.
	/// </remarks>
	/// <param name="data"><inheritdoc cref="ValidateBRRData(byte[])" path="/param[@name='data']"/></param>
	/// <param name="loopPoint">The loop point of the sample; <c>-1</c> if no loop.</param>
	/// <inheritdoc cref="ValidateBRRData(byte[])" path="/returns"/>
	public static BRRDataIssue ValidateBRRData(byte[] data, int loopPoint) {
		return ValidateBRRData(data.AsSpan(), loopPoint);
	}

	/// <inheritdoc cref="ValidateBRRData(byte[], int)" path="/summary"/>
	/// <remarks>
	/// If the loop point is unknown, use <see cref="ValidateBRRData(Span{byte})"/>.
	/// </remarks>
	/// <inheritdoc cref="ValidateBRRData(byte[], int)" path="/param"/>
	/// <inheritdoc cref="ValidateBRRData(byte[])" path="/returns"/>
	public static BRRDataIssue ValidateBRRData(Span<byte> data, int loopPoint) {
		BRRDataIssue ret = ValidateBRRData(data);
		
		// don't bother with the loop point for bad data.
		if (ret.HasFlag(BRRDataIssue.BadAlignment)
			|| ret.HasFlag(BRRDataIssue.DataTooSmall)
			|| ret.HasFlag(BRRDataIssue.DataTooLarge)) {
			return ret;
		}

		// check for a loop flag from the final block
		bool hasLoopFlag = (data[^BRRBlockSize] & LoopFlag) == LoopFlag;

		// Check if loop point is aligned to a multiple of 9
		if ((loopPoint % BRRBlockSize) is not 0) {
			ret |= BRRDataIssue.MisalignedLoopPoint;

			// undefined if there's a loop
			if (hasLoopFlag) {
				ret |= BRRDataIssue.UndefinedBehavior;
			}
		}

		// check if the loop point is in range
		if (loopPoint >= data.Length) {
			ret |= BRRDataIssue.OutOfRangeLoopPoint;

			if (hasLoopFlag) {
				ret |= BRRDataIssue.UndefinedBehavior;

				if (loopPoint > 0xFFFF) {
					ret |= BRRDataIssue.Unresolvable;
				}
			}
		}

		return ret;
	}

	// Shouting out IsoFrieze here, because how to best implement this didn't click until I rewatched the RGME video.

	/// <summary>
	/// Decodes BRR data to PCM data by emulating the SNES DSP decoding process at a fixed pitch value.
	/// </summary>
	/// <remarks>
	/// <para>
	/// If the beginning of a sample decodes poorly, it is likely that it does not use filter 0
	/// and/or 0 value samples at the beginning of the sample.
	/// This method uses seeds the ADPCM sample history as a means of
	/// emulating the undefined behavior for a newly keyed-on voice.
	/// </para>
	/// <para>
	/// <u><b>DEV NOTE:</b></u> I am not 100% satisfied with the output of this decoder yet.
	/// It is fairly accurate, but I believe it is missing a couple of details that push it away
	/// from perfectly emulating the DSP.
	/// It is good enough to roughly gauge the quality of a sample, but for now it is recommended
	/// all samples be tested on actual console, emulator, or an spc player.
	/// </para>
	/// </remarks>
	/// <param name="pitch">
	///   <para>
	///     The output frequency to emulate decoding at;
	///     where 0x1000 is 32000 Hz with a 1:1 output,
	///     and higher values result in higher frequencies.
	///   </para>
	///   <para>
	///     Values outside the range [0x0001,0x3FFF] will fall back to 0x1000.
	///   </para>
	/// </param>
	/// <param name="minimumLength">
	///     The minimum length of the output audio in seconds.
	///     Ignored on non-looping samples.
	///     Don't ask for more than 10 seconds.
	/// </param>
	/// <returns>A <see cref="WaveContainer"/> containing the decoded samples to be played back at 32000 Hz.</returns>
	public WaveContainer Decode(int pitch, decimal minimumLength) {
		if (pitch is < 1 or > 0x3FFF) {
			pitch = DefaultVxPitch;
		}

		if (minimumLength < 0) {
			minimumLength = 0;
		} else if (minimumLength > 10) {
			throw new ArgumentOutOfRangeException(nameof(minimumLength), $"{nameof(minimumLength)} must be a value between 0 and 10.");
		}

		// get sample rate from vxp
		int sampleRate = pitch * DSPFrequency;
		sampleRate /= DefaultVxPitch;

		// determine length of output based on loop parameters
		int loopBlock = _loopblock;

		int loopCount = 0;

		if (loopBlock >= _blockCount || loopBlock < 0) {
			loopCount = 0;
			loopBlock = 0;
		} else {
			int minSamples = (int) decimal.Ceiling(minimumLength * sampleRate / PCMBlockSize);
			loopCount = Math.Max(loopCount, (minSamples - loopBlock) / (_blockCount - loopBlock));
			loopCount = Math.Clamp(loopCount, 1, 777);
		}

		int loopPoint = loopBlock * PCMBlockSize;
		int brrSampleSize = SampleCount;

		int loopSize = _blockCount - loopBlock;

		int blockCount = _blockCount + (loopSize * loopCount);
		int sampleCount = blockCount * PCMBlockSize;

		WaveContainer ret = new(DSPFrequency, PreferredBitDepth, sampleCount);

		int decodePos = 0;
		int pitchCounter = 0;

		// "random" numbers; just something nonzero to emulate badness if not initialized to filter 0
		int p1 = unchecked((short) 0xBEBE);
		int p2 = 5656; // Awww man... now that's all I can think about!
		int p3 = 0x4040;
		int p4 = -0x7171;
	
		for (int i = 0; i < 4; i++) {
			DecodeNextSample();
		}
	
		for (int i = 0; i < sampleCount; i++) {
			// next sample output
			int gaussX = (pitchCounter >> 4) & 0xFF;
			int wv;
			wv  = (SuiteUtility.SuiteGaussTable[0x0FF - gaussX] * p4) >> 10;
			wv += (SuiteUtility.SuiteGaussTable[0x1FF - gaussX] * p3) >> 10;
			wv += (SuiteUtility.SuiteGaussTable[0x100 + gaussX] * p2) >> 10;
			wv += (SuiteUtility.SuiteGaussTable[0x000 + gaussX] * p1) >> 10;
			wv >>= 1;
			wv = Conversion.Clip(wv);
			ret[i] = (short) wv;
	
			pitchCounter += pitch;
	
			while (pitchCounter >= 0x1000) {
				DecodeNextSample();
				pitchCounter -= 0x1000;
			}
		}
	
		void DecodeNextSample() {
			BRRBlock brb = new(ref _data[(decodePos >> 4) * BRRBlockSize]);
	
			int p5 = Conversion.ApplyRange(brb[decodePos & 0xF], brb.Range);
			p5 += Conversion.GetPrediction(brb.Filter, p1, p2);
	
			p4 = p3;
			p3 = p2;
			p2 = p1;
			p1 = p5;
	
			decodePos++;
	
			if (decodePos == brrSampleSize) {
				decodePos = loopPoint;
			}
		}
	
		return ret;
	}

	/// <inheritdoc cref="Decode(int, decimal)"/>
	/// <remarks>
	/// Uses a minimum length of <c>1.0</c>.
	/// </remarks>
	public WaveContainer Decode(int pitch) => Decode(pitch: pitch, minimumLength: 1.0M);

	/// <inheritdoc cref="Decode(int, decimal)"/>
	/// <remarks>
	/// Uses a VxPitch of <c>0x1000</c>.
	/// </remarks>
	public WaveContainer Decode(decimal minimumLength) => Decode(pitch: DefaultVxPitch, minimumLength: minimumLength);

	/// <inheritdoc cref="Decode(int, decimal)"/>
	/// <remarks>
	/// Uses a VxPitch of <c>0x1000</c> and a minimum length of <c>1.0</c>.
	/// </remarks>
	public WaveContainer Decode() => Decode(pitch: DefaultVxPitch, minimumLength: 1.0M);

}
