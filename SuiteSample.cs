// BRR Suite is licensed under the MIT license.

using static BRRSuite.SuiteSampleConstants;

namespace BRRSuite;

/// <summary>
/// Implements the BRR Suite Sample format.
/// </summary>
public sealed class SuiteSample {
	/// <summary>
	/// The preferred file extension for a BRR Suite Sample file.
	/// </summary>
	public const string Extension = SuiteSampleConstants.Extension;

	/// <summary>
	/// The underlying sample this instance describes.
	/// </summary>
	public BRRSample Sample { get; }

	/// <summary>
	/// Gets or sets the instrument name associated with this sample.
	/// </summary>
	/// <remarks>
	/// Names should be exactly 24 characters in length (padded with spaces if necessary)
	/// and only use printable ISO/IEC 8859-1 characters
	/// (Unicode blocks Basic Latin and Latin-1 Supplement).
	/// These restrictions are enforced by the property setter.
	/// </remarks>
	public string InstrumentName {
		get => _name;
		set {
			char[] sanitize = new char[InstrumentNameLength];
			Array.Fill(sanitize, InstrumentNamePadChar);

			int len = 0;

			// sanitize
			foreach (char c in value) {
				char addChar = c;
				switch (c) {
					case < '\x20': // control characters
					case >= '\x7F' and < '\xA0': // DEL + Latin-1 Supplement control characters
					case '\xAD': // SHY
						continue; // skip character

					case '\xA0': // NBSP => SPACE
						addChar = ' ';
						break;
				}

				sanitize[len++] = addChar;
				if (len == InstrumentNameLength) {
					break;
				}
			}

			// sanitize to ascii
			_name = new(sanitize);
		}
	}
	private string _name = new(InstrumentNamePadChar, InstrumentNameLength);

	/// <summary>
	/// Gets or sets the SPC sound system's DSP VxPITCH value that corresponds
	/// to an audible frequency for the note C note of a non-specific octave.
	/// </summary>
	/// <remarks>
	/// Defaults to 0x1000.
	/// <list type="bullet">
	/// <item>The value <c>0x1000</c> corresponds to no frequency change on playback; i.e., the sample is played at 32kHz.</item>
	/// <item>The value <c>0x0000</c> indicates the frequency of C for this sample is not known.</item>
	/// <item>Values of <c>0x4000</c> and higher are considered invalid and will be changed to 0.</item>
	/// </list>
	/// </remarks>
	public int VxPitch {
		get => _vxpitch;
		set {
			if (value is < 0 or > 0x3FFF) {
				value = 0;
			}
			_vxpitch = value;
		}
	}
	private int _vxpitch = DefaultVxPitch;

	/// <summary>
	/// Gets or sets the target frequency this sample was encoded at.
	/// </summary>
	/// <remarks>
	/// Defaults to 32000.
	/// This value should not be zero or negative.
	/// </remarks>
	public int EncodingFrequency {
		get => _encodingFrequency;
		set {
			if (value < 1) {
				throw new ArgumentOutOfRangeException(null, "Encoding frequency cannot be zero or negative");
			}
			_encodingFrequency = value;
		}
	}
	private int _encodingFrequency = DSPFrequency;

	/// <summary>
	/// Gets or sets the looping behavior of the sample.
	/// </summary>
	public LoopBehavior LoopBehavior { get; set; } = LoopBehavior.NonLooping;

	/// <summary>
	/// Gets or sets the relative location of the loop point in bytes.
	/// </summary>
	/// <remarks>
	/// <para>This overrides the behavior of the underlying BRR when set.</para>
	/// <para>Use <see cref="ResetLoopPoint"/> to recover the sample's loop point.</para>
	/// <para>Use <see cref="SetAndFlagLoopPoint(int)"/> to simultaneously correct <see cref="SuiteSample.LoopBehavior"/></para>
	/// </remarks>
	public int LoopPoint {
		get => _loopPoint;
		set {
			if (value > 0xFFFF) {
				throw new ArgumentOutOfRangeException(null, "Loop point must be less than 0x10000");
			}

			if (value < 0) {
				value = NoLoop;
			}
			_loopPoint = value;
		}
	}
	private int _loopPoint;

	/// <summary>
	/// Gets the number of blocks in this sample.
	/// </summary>
	public int BlockCount => Sample.BlockCount;

	/// <summary>
	/// Gets the length of the sample data.
	/// </summary>
	public int SampleLength => Sample.Length;

	/// <summary>
	/// Initializes a new instance of the <see cref="SuiteSample"/> class using a given <see cref="BRRSample"/> as its base.
	/// </summary>
	/// <param name="brr">The sample to base this instance on.</param>
	public SuiteSample(BRRSample brr) {
		Sample = brr;
		ResetLoopPoint();
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="SuiteSample"/> class from a given data stream.
	/// </summary>
	/// <param name="stream">The stream to read from.</param>
	/// <exception cref="FormatException">The stream's data is not valid.</exception>
	public SuiteSample(Stream stream) {
		// Verify
		var data = new byte[(int) stream.Length];
		stream.Read(data, 0, data.Length);

		if (!ValidateSuiteSample(data, out string? msg)) {
			throw new FormatException(msg);
		}

		int length = ReadShort(SampleLengthLocation);

		InstrumentName = SuiteUtility.GetLatin1String(data, InstrumentNameLocation, InstrumentNameLength);

		VxPitch = (ushort) ReadShort(PitchLocation);

		EncodingFrequency = ReadInt(EncodingFrequencyLocation);
		LoopPoint = (ushort) ReadShort(LoopPointLocation);
		LoopBehavior = (LoopBehavior) data[LoopTypeLocation];

		Sample = new(new Span<byte>(data, SamplesDataLocation, length));

		int ReadShort(int i) {
			return data[i] | (data[i + 1] << 8);
		}

		int ReadInt(int i) {
			return data[i] | (data[i + 1] << 8) | (data[i + 2] << 16) | (data[i + 3] << 24);
		}
	}

	/// <summary>
	/// Sets the sample's loop point and auto corrects the loop type.
	/// </summary>
	/// <param name="loop">The new loop point, relative the sample's start..</param>
	public void SetAndFlagLoopPoint(int loop) {
		if (loop is < 0 or > 0xFFFF) {
			LoopPoint = 0x0000;
			LoopBehavior = LoopBehavior.NonLooping;
			return;
		}

		LoopPoint = (ushort) (loop & 0xFFFF);

		if (loop >= SampleLength) {
			LoopBehavior = LoopBehavior.Extrinsic;
		} else if (loop % BRRBlockSize != 0) {
			LoopBehavior = LoopBehavior.Misaligned;
		} else {
			LoopBehavior = LoopBehavior.Looping;
		}
	}

	/// <summary>
	/// Resets the loop point to that of the underlying sample's.
	/// </summary>
	public void ResetLoopPoint() {
		SetAndFlagLoopPoint(Sample.LoopPoint);
	}

	/// <summary>
	/// Gets the 16-bit, fixed-point multiplier used by the N-SPC engine to tune this sample to 0x1000.
	/// </summary>
	/// <remarks>
	/// Note that most implementations of N-SPC store this number in big-endian.
	/// </remarks>
	/// <returns>The N-SPC instrument pitch multiplier.</returns>
	public int GetNSPCMultiplier() {
		return (ushort) (DefaultVxPitch * 0x100 / VxPitch);
	}

	private const int DefaultVxPitchOctave = 4;
	private const double ConcertC4 = 261.6256D;

	/// <inheritdoc cref="SetVxPitchForFrequency(double, int)"/>
	/// <inheritdoc cref="GetVxPitchForFrequency(double)" path="/remarks"/>
	public void SetVxPitchForFrequency(double frequency) => SetVxPitchForFrequency(frequency, DefaultVxPitchOctave);

	/// <summary>
	/// Sets the VxPitch of this sample to the value that most closely tunes it nearest the specified octave.
	/// </summary>
	/// <inheritdoc cref="GetVxPitchForFrequency(double, int)" path="/param"/>
	public void SetVxPitchForFrequency(double frequency, int octave) {
		VxPitch = GetVxPitchForFrequency(frequency, octave);
	}

	/// <inheritdoc cref="GetVxPitchForFrequency(double, int)" path="//summary|//param|//returns"/>
	/// <remarks>
	/// Uses a default octave of 4, corresponding to a VxPitch of 0x1000.
	/// </remarks>
	public static int GetVxPitchForFrequency(double frequency) => GetVxPitchForFrequency(frequency, DefaultVxPitchOctave);

	/// <summary>
	/// Gets the VxPitch that most closely tunes this sample nearest the specified octave.
	/// </summary>
	/// <param name="frequency">The frequency of the sample.</param>
	/// <param name="octave">
	///     The octave of C that should be tuned to.
	///     This should be a value in the range [0,5],
	///     where the VxPITCH target is calculated as <c>0x1000*2^(octave-4)</c>.
	/// </param>
	/// <returns>The corresponding VxPITCH value of the given frequency tuned to the specified octave.</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static int GetVxPitchForFrequency(double frequency, int octave) {
		if (frequency < 1) {
			throw new ArgumentOutOfRangeException(nameof(frequency), "Frequency cannot be less than 1.");
		}

		int targetVx = octave switch {
			0 => DefaultVxPitch/16,
			1 => DefaultVxPitch/8,
			2 => DefaultVxPitch/4,
			3 => DefaultVxPitch/2,
			4 => DefaultVxPitch,
			5 => DefaultVxPitch*2,
			_ => throw new ArgumentOutOfRangeException(nameof(octave), "Octave must be a value from 0 to 5, inclusive.")
		};

		// get ratio between the given frequency and C4
		decimal retvxp = DefaultVxPitch * (decimal) (ConcertC4 / frequency);

		if (retvxp < targetVx) {
			decimal bounds = targetVx / 2;

			while (retvxp <= bounds) {
				retvxp *= 2;
			}
		} else {
			decimal bounds = targetVx * 2;

			while (retvxp >= bounds) {
				retvxp /= 2;
			}
		}

		return (int) decimal.Round(retvxp, 0, MidpointRounding.ToZero);
	}

	/// <summary>
	/// Saves this to the given file location.
	/// </summary>
	/// <param name="path">The relative or absolute path this sample should be saved to.</param>
	public void Save(string path) {
		using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);

		fs.Write(ToArray());
	}

	internal byte[] ToArray() {
		ThrowIfUnresolvableIssues();

		byte[] header = new byte[SamplesDataLocation];

		// header
		SuiteUtility.WriteString(header, FormatSignatureLocation, FormatSignature);

		int cksm = GetChecksum(Sample.AsSpan());

		SuiteUtility.WriteShort(header, ChecksumLocation, cksm);
		SuiteUtility.WriteShort(header, ChecksumComplementLocation, ~cksm);

		// meta block
		SuiteUtility.WriteString(header, MetaBlockSignatureLocation, MetaBlockSignature);
		SuiteUtility.WriteString(header, InstrumentNameLocation, InstrumentName);
		SuiteUtility.WriteShort(header, PitchLocation, VxPitch);
		SuiteUtility.WriteInt(header, EncodingFrequencyLocation, EncodingFrequency);

		// 7 unused bytes

		// data block
		SuiteUtility.WriteString(header, DataBlockSignatureLocation, DataBlockSignature);
		header[LoopTypeLocation] = (byte) LoopBehavior;

		(int loopPoint, int loopBlock) = LoopBehavior switch {
			LoopBehavior.NonLooping => (0x0000, 0x0000),
			LoopBehavior.Looping => (LoopPoint, BRRBlockSize / BRRBlockSize),
			LoopBehavior.Misaligned or
			LoopBehavior.Extrinsic => (LoopPoint, 0x0000),
			_ => (0x0000, 0x0000)
		};

		SuiteUtility.WriteShort(header, LoopBlockLocation, loopBlock);
		SuiteUtility.WriteShort(header, LoopPointLocation, loopPoint);
		SuiteUtility.WriteShort(header, SampleBlocksLocation, BlockCount);
		SuiteUtility.WriteShort(header, SampleLengthLocation, SampleLength);

		return [.. header, .. Sample.AsSpan()];
	}


	private void ThrowIfUnresolvableIssues() {
		Sample.ThrowIfUnresolvableIssues();

		// Force validation to run on the name as a precaution
		InstrumentName = _name;

		if (_name.Length is not InstrumentNameLength) {
			throw new BRRConversionException($"Something went terribly wrong with the name: \"{_name}\" (length: {_name.Length})");
		}
	}

	/// <summary>
	/// Tests a stream of data for a properly-formed BRR Suite Sample file header and valid BRR data.
	/// </summary>
	/// <param name="data">The data to validate.</param>
	/// <param name="message">A string containing a message about where, if at all, the data was deemed invalid.</param>
	/// <returns><see langword="true"/> if the file is valid; otherwise <see langword="false"/>.</returns>
	public static bool ValidateSuiteSample(byte[] data, out string? message) {
		// check for a valid file size
		int length = data.Length;

		int rd16, rd16b;

		if (length < (SamplesDataLocation + 9)) {
			message = $"File is too small to be a {FormatName} file.";
			return false;
		}

		// check the signatures

		if (!SuiteUtility.TestSubstring(data, FormatSignatureLocation, FormatSignature, out message)) {
			return false;
		}

		if (!SuiteUtility.TestSubstring(data, MetaBlockSignatureLocation, MetaBlockSignature, out message)) {
			return false;
		}

		if (!SuiteUtility.TestSubstring(data, DataBlockSignatureLocation, DataBlockSignature, out message)) {
			return false;
		}

		int samplesLength = length - SamplesDataLocation;

		if ((samplesLength % BRRBlockSize) != 0) {
			message = $"Sample data is not a multiple of {BRRBlockSize} bytes: {samplesLength}";
			return false;
		}

		// checksums
		rd16 = GetChecksum(new Span<byte>(data, SampleBlocksLocation, samplesLength));

		rd16b = SuiteUtility.ReadShort(data, ChecksumLocation);
		if (rd16 != rd16b) {
			message = $"Bad checksum: {rd16b:X4} | Expected: {rd16:X4}";
			return false;
		}

		rd16b = SuiteUtility.ReadShort(data, ChecksumComplementLocation);
		rd16 ^= 0xFFFF;
		if (rd16 != rd16b) {
			message = $"Bad checksum complement: {rd16b:X4} | Expected: {rd16:X4}";
			return false;
		}

		rd16 = SuiteUtility.ReadShort(data, SampleLengthLocation);
		if (samplesLength != rd16) {
			message = $"Incorrect length: {rd16} | Expected: {samplesLength}";
			return false;
		}

		rd16 = SuiteUtility.ReadShort(data, SampleBlocksLocation);

		if ((rd16 * BRRBlockSize) != samplesLength) {
			message = $"Incorrect block count: {rd16} | Expected: {samplesLength / BRRBlockSize}";
			return false;
		}

		rd16 = SuiteUtility.ReadShort(data, LoopBlockLocation);
		rd16b = SuiteUtility.ReadShort(data, LoopPointLocation);

		if ((rd16 * BRRBlockSize) != rd16b) {
			message = $"Loop block and loop point do not match: b:[{rd16}, {rd16 * BRRBlockSize}] | l:[{rd16b / BRRBlockSize},{rd16b}]";
			return false;
		}

		byte loopType = data[LoopTypeLocation];

		if (rd16b >= length && loopType is LoopingSample) {
			message = $"Invalid loop point: {rd16b}";
			return false;
		}

		bool hasLoopFlag = (data[^BRRBlockSize] & LoopFlag) is LoopFlag;

		switch (loopType, hasLoopFlag) {
			case (NonloopingSample, true):
			case (LoopingSample, false):
			case (ForeignLoopingSample, false):
				message = $"Loop type does not match final block header.";
				return false;
		}

		if ((data[^BRRBlockSize] & EndFlag) is 0) {
			message = $"The sample data does not contain an end flag on the final block header.";
			return false;
		}

		samplesLength -= BRRBlockSize;

		for (int i = 0; i < samplesLength; i += BRRBlockSize) {
			if ((data[SamplesDataLocation + i] & EndFlag) is EndFlag) {
				message = $"The sample data contains too many end block flags.";
				return false;
			}
		}

		// Valid file! tada emoji
		message = null;

		return true;
	}

	/// <inheritdoc cref="ValidateSuiteSample(byte[], out string?)"/>
	public static bool ValidateSuiteSample(Stream data, out string? message) {
		// Verify
		var dataCopy = new byte[(int) data.Length];
		data.Read(dataCopy, 0, dataCopy.Length);

		return ValidateSuiteSample(dataCopy, out message);
	}

	/// <summary>
	/// Creates a BRR Suite Sample specification checksum for the given sample data.
	/// </summary>
	/// <remarks>
	/// This method should not be passed a BRR Suite Sample file data stream with header;
	/// callers should only pass the raw BRR-encoded samples.
	/// </remarks>
	/// <param name="sampleData">The sample data to checksum.</param>
	/// <returns>The checksum as a 16-bit value.</returns>
	/// <exception cref="ArgumentException">The length of the data is 0 or not a multiple of 9.</exception>
	public static int GetChecksum(Span<byte> sampleData) {
		int length = sampleData.Length;

		if (length == 0) {
			throw new ArgumentException("Cannot checksum an empty set of samples.", nameof(sampleData));
		}

		if (length % BRRBlockSize != 0) {
			throw new ArgumentException($"Cannot checksum data whose length is not a multiple of {BRRBlockSize}.", nameof(sampleData));
		}

		// Step 1: Begin with a sum accumulator of 0
		int ret = 0;

		// Step 2: For each block:
		for (int i = 0; i < length; i += BRRBlockSize) {
			// Step 2.1: Reset the block accumulator
			int accum = 0;

			// Step 2.2: Add the 8 bytes of the block, each shifted left by their index within the block minus 1
			for (int j = 1; j < BRRBlockSize; j++) {
				accum += sampleData[i + j] << (j - 1);
			}

			// Step 2.3: Shift the header byte 4 bits left
			int hbs = sampleData[i] << 4;

			// Step 2.4: Exclusive OR the shifted header with the block accumulator
			accum ^= hbs;

			// Step 2.5: Add the block accumulator to the sum accumulator
			ret += accum;
		}

		// Step 3: Truncate the sum accumulator to 16-bits and return.
		return ret & 0xFFFF;
	}
}
