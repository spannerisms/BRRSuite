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
	public SampleRate SampleRate { get; }

	/// <summary>
	/// Gets the fidelity of one sample, given as its size in bits.
	/// </summary>
	public int BitsPerSample { get; } = PreferredBitsPerSample;

	/// <summary>
	/// Gets number of bytes required to represent each sample.
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
	public int ByteRate => SampleRate.Frequency * Channels * BytesPerSample;

	/// <summary>
	/// Gets the size of chunk 2 (which contains the audio data) in bytes.
	/// </summary>
	public int Chunk2Size => SampleCount * Channels * BytesPerSample;

	/// <summary>
	/// Gets or sets flags that indicate how this audio file was modified from its original source or after creation.
	/// </summary>
	public WaveFileChanges ChangesFromOriginal { get; set; } = WaveFileChanges.None;

	/// <summary>
	/// Gets or sets a sample at the given index.
	/// </summary>
	public short this[Index i] {
		get => _samples[i];
		set => _samples[i] = value;
	}

	private readonly int dataSize;

	private readonly byte[] _data;

	// this will be a slice of the above
	private readonly ArraySegment<short> _samples;

	/// <summary>
	/// Initializes a new instance of the <see cref="WaveContainer"/> class with the specified properties and an initially silent waveform.
	/// </summary>
	/// <param name="samplerate">The sample rate of this audio.</param>
	/// <param name="bitsPerSample">The fidelity of the audio expressed as the size of the sample in bits.</param>
	/// <param name="sampleCount">The number of samples in this audio.</param>
	public WaveContainer(int samplerate, int bitsPerSample, int sampleCount) {
		SampleCount = sampleCount;
		SampleRate = new(samplerate);
		BitsPerSample = bitsPerSample;

		dataSize = WaveDataOffset + Chunk2Size;

		_data = new byte[dataSize];

		_samples = GetSamplesSlice(_data);

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
	public int[] GetSamplesCopy() {
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
	/// Reads a Wave Sound file from a given location.
	/// If the file contains between 2 and 4 channels, they will be mixed down to mono.
	/// Files with more channels will be rejected with an exception.
	/// </summary>
	/// <param name="path">A relative or absolute path to the audio file.</param>
	/// <returns>A new <see cref="WaveContainer"/> with a single channel of 16-bit PCM audio.</returns>
	/// <exception cref="BRRConversionException"></exception>
	public static WaveContainer ReadFromFile(string path) {
		using var rd = new FileStream(path, FileMode.Open, FileAccess.Read);

		// Verify WAV
		var data = new byte[(int) rd.Length];
		rd.Read(data, 0, data.Length);

		rd.Close();

		if (!VerifyWAV(data, out string message)) {
			throw new BRRConversionException(message ?? "Not a valid 16-bit PCM WAV file!");
		}

		int channels = GetShortFromPosition(data, WaveChannelCountOffset);

		if (channels > 4) {
			throw new BRRConversionException("Too many channels. I'm not mixing this.");
		}

		int samplerate = GetIntFromPosition(data, 24);
		int sampleCount = GetIntFromPosition(data, 40) / 2;

		WaveFileChanges changes = channels == 1 ? WaveFileChanges.None : WaveFileChanges.MixedToMono;

		var ret = new WaveContainer(samplerate, PreferredBitsPerSample, sampleCount) {
			ChangesFromOriginal = changes
		};

		// fast copy for 1 channel
		if (channels == 1) {
			Array.Copy(data, WaveDataOffset, ret._data, WaveDataOffset, ret.Chunk2Size);
		} else {
			var insamples = GetSamplesSlice(data);

			// average each channel
			for (int i = 0, j = 0; i < sampleCount; i ++, j += channels) {
				int cur = 0;

				for (int k = 0; k < channels; k++) {
					cur += insamples[j++];
				}

				ret._samples[i] = (short) (cur / channels);
			}
		}

		return ret;
	}

	/// <summary>
	/// Tests a stream of data for a properly-formed WAV header that indicates it is uncompressed, 16-bit PCM audio.
	/// </summary>
	/// <param name="data">The data to validate.</param>
	/// <param name="message">When this method returns, this will contain a message about where, if at all, the data was deemed invalid.</param>
	/// <returns><see langword="true"/> if the header is valid; otherwise <see langword="false"/>.</returns>
	public static bool VerifyWAV(byte[] data, out string message) {
		if (data.Length < 64) {
			message = "This file is too small to be of use.";
			return false;
		}

		string? badMSG = TestSubstring(WaveChunkIDOffset, RiffChunkDescriptor);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		badMSG = TestSubstring(WaveFormatOffset, WaveChunkDescriptor);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		badMSG = TestSubstring(WaveSubchunk1IDOffset, FormatChunkDescriptor);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		badMSG = TestSubstring(WaveSubchunk2IDOffset, DataChunkDescriptor);
		if (badMSG is not null) {
			message = badMSG;
			return false;
		}

		if (GetShortFromPosition(data, WaveAudioFormatOffset) != 1) {
			message = "Not an uncompressed PCM formatted wave.";
			return false;
		}

		if (GetShortFromPosition(data, WaveBitsPerSampleOffset) != PreferredBitsPerSample) {
			message = "Not a 16-bit Wave Sound file.";
			return false;
		}

		if (GetIntFromPosition(data, WaveChunkSizeOffset) != (data.Length - 8)) {
			message = $"Header file size does not match actual file size.";
			return false;
		}

		message = "Valid!";
		return true;

		string? TestSubstring(int start, string test) {
			var s = data[start..(start + 4)];
			string result = new(s.Select(o => (char) o).ToArray());

			if (result == test) {
				return null;
			}

			return $"Bad string at {start}: {result} | Expected: {test}";
		}
	}

	/// <summary>
	/// Fixes the header data to match the current properties of the wave file.
	/// </summary>
	private void FixHeader() {
		// RIFF chunk
		WriteString(RiffChunkDescriptor, WaveChunkIDOffset);
		WriteInt(Chunk2Size + WaveDataOffset - 8, WaveChunkSizeOffset);
		WriteString(WaveChunkDescriptor, WaveFormatOffset);

		// format subchunk
		WriteString(FormatChunkDescriptor, WaveSubchunk1IDOffset);
		WriteInt(16, WaveSubchunk1SizeOffset); // header size
		WriteShort(1, WaveAudioFormatOffset); // audio format => 1 (PCM)
		WriteShort(Channels, WaveChannelCountOffset); // number of channels
		WriteInt(SampleRate.Frequency, WaveSampleRateOffset);
		WriteInt(ByteRate, WaveByteRateOffset); // byte rate
		WriteShort(Channels * BytesPerSample, WaveBlockAlignOffset); // block align
		WriteShort(BitsPerSample, WaveBitsPerSampleOffset);

		// data subchunk
		WriteString(DataChunkDescriptor, WaveSubchunk2IDOffset);
		WriteInt(Chunk2Size, WaveSubchunk2SizeOffset);
		
		void WriteString(string s, int offset) {
			foreach (var c in s) {
				_data[offset++] = (byte) c;
			}
		}

		void WriteShort(int s, int offset) {
			_data[offset++] = (byte) s;
			_data[offset++] = (byte) (s >> 8);
		}

		void WriteInt(int s, int offset) {
			_data[offset++] = (byte) s;
			_data[offset++] = (byte) (s >> 8);
			_data[offset++] = (byte) (s >> 16);
			_data[offset++] = (byte) (s >> 24);
		}
	}

	/// <summary>
	/// Exports this Wave Sound file to the given location.
	/// </summary>
	/// <param name="path">The absolute or relative path this audio should be saved to.</param>
	/// <param name="fixPath">Allows the export to add or change the extension of the path to the preferred extension.</param>
	public void Export(string path, bool fixPath = false) {
		if (fixPath) {
			path = Path.ChangeExtension(path, Extension);
		}

		using var fs = File.Open(path, FileMode.Create, FileAccess.Write);
		using var ws = AsMemoryStream();

		ws.CopyTo(fs);

		fs.Flush();
	}

	private static int GetIntFromPosition(byte[] data, int position) {
		return (data[position++]) | (data[position++] << 8) | (data[position++] << 16) | (data[position] << 24);
	}

	private static int GetShortFromPosition(byte[] data, int position) {
		return (data[position++]) | (data[position] << 8);
	}

	/// <summary>
	/// Gets a segment of data corresponding to the 16 samples of the requested block.
	/// </summary>
	/// <param name="samples">The samples to get a block of.</param>
	/// <param name="block">Index of block to cover.</param>
	/// <returns>A <see cref="Span{T}"/> of length 16 over the specified block.</returns>
	/// <exception cref="ArgumentOutOfRangeException">If the index requested is negative or more than the number of blocks in the sample.</exception>
	public static Span<int> GetBlockAt(int[] samples, int block) {
		if (block >= (samples.Length / PcmBlockSize) || block < 0) {
			throw new ArgumentOutOfRangeException();
		}

		return new(samples, block * PcmBlockSize, PcmBlockSize);
	}
	
	/// <inheritdoc cref="GetBlockAt(int[], int)"/>
	public static Span<short> GetBlockAt(short[] samples, int block) {
		if (block >= (samples.Length / PcmBlockSize) || block < 0) {
			throw new ArgumentOutOfRangeException();
		}

		return new(samples, block * PcmBlockSize, PcmBlockSize);
	}
}
