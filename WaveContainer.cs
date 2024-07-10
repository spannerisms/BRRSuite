// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Holds a Wave Sound file with functionality to easily access the data as both a valid file with header or a raw stream of samples.
/// </summary>
public sealed class WaveContainer {
	/// <summary>
	/// The preferred extension for Wave Sound files.
	/// </summary>
	public const string Extension = "wav";

	// constants for making valid wave files
	private const int WaveChunkIDOffset = 0;
	private const int WaveChunkSizeOffset = 4;
	private const int WaveFormatOffset = 8;
	private const int WaveSubchunk1IDOffset = 12;
	private const int WaveSubchunk1SizeOffset = 16;
	private const int WaveAudioFormatOffset = 20;
	private const int WaveChannelCountOffset = 22;
	private const int WaveSampleRateOffset = 24;
	private const int WaveByteRateOffset = 28;
	private const int WaveBlockAlignOffset = 32;
	private const int WaveBitsPerSampleOffset = 34;
	private const int WaveSubchunk2IDOffset = 36;
	private const int WaveSubchunk2SizeOffset = 40;
	private const int WaveDataOffset = 44;

	private const string RiffChunkDescriptor = "RIFF";
	private const string WaveChunkDescriptor = "WAVE";
	private const string FormatChunkDescriptor = "fmt ";
	private const string DataChunkDescriptor = "data";

	/***************************************************************************************************************/

	/// <summary>
	/// Gets the sample rate this audio file should be played back at.
	/// </summary>
	public int SampleRate { get; }

	/// <summary>
	/// Gets the bit depth of one sample.
	/// </summary>
	public int BitsPerSample { get; } = Conversion.PreferredBitDepth;

	/// <summary>
	/// Gets the number of bytes required to represent each sample.
	/// </summary>
	public int BytesPerSample => BitsPerSample / 8;

	/// <summary>
	/// Gets the number of samples contained in this audio file.
	/// </summary>
	public int SampleCount { get; }

#pragma warning disable IDE0079 // Remove unnecessary suppression - get rekt
#pragma warning disable CA1822 // Mark members as static
	// This might not be constant one day. Not today.

	/// <summary>
	/// Gets the number of individual channels contained in this audio file.
	/// </summary>
	public int Channels => 1;
#pragma warning restore CA1822 // Mark members as static
#pragma warning restore IDE0079 // Remove unnecessary suppression

	/// <summary>
	/// Gets the number of bytes processed per second when this audio file is played at its intended speed.
	/// </summary>
	public int ByteRate => SampleRate * Channels * BytesPerSample;

	/// <summary>
	/// Gets the size of chunk 2 (which contains the audio data) in bytes.
	/// </summary>
	public int Chunk2Size => SampleCount * Channels * BytesPerSample;

	/// <summary>
	/// Gets or sets a sample at the given index.
	/// </summary>
	public short this[Index sample] {
		get => _samples[sample];
		set => _samples[sample] = value;
	}

	private readonly int dataSize;

	private readonly byte[] _data;

	// this will be a slice of the above
	private readonly ArraySegment<short> _samples;

	/// <summary>
	/// Initializes a new instance of the <see cref="WaveContainer"/> class with the specified properties and an initially silent waveform.
	/// </summary>
	/// <param name="sampleRate">The sample rate of this audio.</param>
	/// <param name="bitsPerSample">The fidelity of the audio expressed as the size of the sample in bits.</param>
	/// <param name="sampleCount">The number of samples in this audio.</param>
	public WaveContainer(int sampleRate, int bitsPerSample, int sampleCount) {
		SampleCount = sampleCount;
		SampleRate = sampleRate;
		BitsPerSample = bitsPerSample;

		dataSize = WaveDataOffset + Chunk2Size;

		_data = new byte[dataSize];

		_samples = GetSamplesSlice(_data);

		FixHeader();
	}

	/// <summary>
	/// Reads a Wave Sound file from a stream.
	/// </summary>
	/// <remarks>
	/// If the file contains between 2 and 4 channels, they will be mixed down to mono.
	/// Files with more channels will be rejected with an exception.
	/// </remarks>
	/// <param name="wavStream">A stream containing valid Wave Sound file data.</param>
	/// <exception cref="BRRConversionException"></exception>
	public WaveContainer(Stream wavStream) {
		// Verify WAV
		var data = new byte[(int) wavStream.Length];
		wavStream.Read(data, 0, data.Length);

		if (!VerifyWAV(data, out string? message)) {
			throw new BRRConversionException(message ?? "Not a valid 16-bit PCM WAV file!");
		}

		int channels = SuiteUtility.ReadShort(data, WaveChannelCountOffset);

		if (channels > 4) {
			throw new BRRConversionException("Too many channels. I'm not mixing this.");
		}

		SampleRate = SuiteUtility.ReadInt(data, 24);
		SampleCount = SuiteUtility.ReadInt(data, 40) / 2;

		BitsPerSample = Conversion.PreferredBitDepth;

		dataSize = WaveDataOffset + Chunk2Size;
		_data = new byte[dataSize];

		_samples = GetSamplesSlice(_data);

		// fast copy for 1 channel
		if (channels == 1) {
			Array.Copy(data, WaveDataOffset, _data, WaveDataOffset, Chunk2Size);
		} else {
			var insamples = GetSamplesSlice(data);

			// average each channel
			for (int i = 0, j = 0; i < SampleCount; i++, j += channels) {
				int cur = 0;

				for (int k = 0; k < channels; k++) {
					cur += insamples[j++];
				}

				_samples[i] = (short) (cur / channels);
			}
		}

		FixHeader();
	}

	/// <summary>
	/// Creates a slice over an audio data stream at the start of the samples data and recast as an array of <see langword="short"/> values.
	/// </summary>
	/// <returns>A new <see cref="ArraySegment{T}"/> of type <see langword="short"/>.</returns>
	private static ArraySegment<short> GetSamplesSlice(byte[] fullData) {
		short[] ds = System.Runtime.CompilerServices.Unsafe.As<short[]>(fullData);

		// divided by 2 because byte => short
		return new(ds, WaveDataOffset / 2, (fullData.Length - WaveDataOffset) / 2);
	}

	/// <summary>
	/// Returns a new array of signed integers copied from the samples data.
	/// </summary>
	/// <returns>A new array of integers containing a copy of this sample.</returns>
	public int[] SamplesToArray() {
		int[] ret = new int[SampleCount];

		for (int i = 0; i < SampleCount; i++) {
			ret[i] = _samples[i];
		}

		return ret;
	}

	/// <summary>
	/// Creates a read-only <see cref="MemoryStream"/> over the entire Wave Sound file's data.
	/// </summary>
	/// <returns>A <see cref="MemoryStream"/> covering the entire file, header included.</returns>
	public MemoryStream AsMemoryStream() {
		return new(_data);
	}

	/// <summary>
	/// Creates a span over the entire Wave Sound file's data.
	/// </summary>
	/// <returns>A span covering the entire file, header included.</returns>
	public Span<byte> AsSpan() {
		return _data.AsSpan();
	}

	/// <summary>
	/// Creates a span over the samples data.
	/// </summary>
	/// <returns>A span covering only the samples data.</returns>
	public Span<short> SamplesAsSpan() {
		return _samples.AsSpan();
	}

	/// <summary>
	/// Tests a stream of data for a properly-formed WAV header that indicates it is uncompressed, 16-bit PCM audio.
	/// </summary>
	/// <param name="data">The data to validate.</param>
	/// <param name="message">When this method returns, this will contain a message about where, if at all, the data was deemed invalid.</param>
	/// <returns><see langword="true"/> if the header is valid; otherwise <see langword="false"/>.</returns>
	public static bool VerifyWAV(byte[] data, out string? message) {
		if (data.Length < 59) {
			message = "This file is too small to be of use.";
			return false;
		}

		if (!SuiteUtility.TestSubstring(data, WaveChunkIDOffset, RiffChunkDescriptor, out message)) {
			return false;
		}

		if (!SuiteUtility.TestSubstring(data, WaveFormatOffset, WaveChunkDescriptor, out message)) {
			return false;
		}

 		//Annoyingly, a lot of people don't follow the spec, so these shouldn't be enforced
/*
		badMSG = SuiteUtility.TestSubstring(data, WaveSubchunk1IDOffset, FormatChunkDescriptor);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		badMSG = SuiteUtility.TestSubstring(data, WaveSubchunk2IDOffset, DataChunkDescriptor);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}
*/

		if (SuiteUtility.ReadShort(data, WaveAudioFormatOffset) != 1) {
			message = "Not an uncompressed PCM formatted wave.";
			return false;
		}

		if (SuiteUtility.ReadShort(data, WaveBitsPerSampleOffset) != Conversion.PreferredBitDepth) {
			message = "Not a 16-bit Wave Sound file.";
			return false;
		}

		if (SuiteUtility.ReadInt(data, WaveChunkSizeOffset) != (data.Length - 8)) {
			message = $"Header file size does not match actual file size.";
			return false;
		}

		message = "Valid!";
		return true;
	}

	/// <summary>
	/// Fixes the header data to match the current properties of the wave file.
	/// </summary>
	private void FixHeader() {
		// RIFF chunk
		SuiteUtility.WriteString(_data, WaveChunkIDOffset, RiffChunkDescriptor);
		SuiteUtility.WriteInt(_data, WaveChunkSizeOffset, Chunk2Size + WaveDataOffset - 8);
		SuiteUtility.WriteString(_data, WaveFormatOffset, WaveChunkDescriptor);

		// format subchunk
		SuiteUtility.WriteString(_data, WaveSubchunk1IDOffset, FormatChunkDescriptor);
		SuiteUtility.WriteInt(_data, WaveSubchunk1SizeOffset, 16); // header size
		SuiteUtility.WriteShort(_data, WaveAudioFormatOffset, 1); // audio format => 1 (PCM)
		SuiteUtility.WriteShort(_data, WaveChannelCountOffset, Channels); // number of channels
		SuiteUtility.WriteInt(_data, WaveSampleRateOffset, SampleRate);
		SuiteUtility.WriteInt(_data, WaveByteRateOffset, ByteRate); // byte rate
		SuiteUtility.WriteShort(_data, WaveBlockAlignOffset, Channels * BytesPerSample); // block align
		SuiteUtility.WriteShort(_data, WaveBitsPerSampleOffset, BitsPerSample);

		// data subchunk
		SuiteUtility.WriteString(_data, WaveSubchunk2IDOffset, DataChunkDescriptor);
		SuiteUtility.WriteInt(_data, WaveSubchunk2SizeOffset, Chunk2Size);
	}

	/// <summary>
	/// Saves this Wave Sound file to the given location.
	/// </summary>
	/// <param name="path">The absolute or relative path this audio should be saved to.</param>
	public void Save(string path) {
		using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
		using var ws = AsMemoryStream();

		ws.CopyTo(fs);
	}
}
