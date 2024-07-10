// BRR Suite is licensed under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace BRRSuite;

/// <summary>
/// Encapsulates a process for encoding a set of audio samples to a BRR-encoded audio file.
/// </summary>
public abstract class BRREncoder {

	/// <summary>
	/// A <see cref="ResamplingAlgorithm"/> that will resample the input samples.
	/// </summary>
	required public ResamplingAlgorithm Resampler { get; set; }

	/// <summary>
	/// Filters to apply to the sample data after it is resized and resampled.
	/// </summary>
	public PreEncodingFilter Filters { get; set; }

	/// <summary>
	/// Desired resampling factor.
	/// </summary>
	/// <remarks>
	/// This value should not be 0 or negative.
	/// </remarks>
	public decimal ResampleFactor {
		get => _resampleFactor;
		set {
			if (value <= 0) {
				throw new ArgumentOutOfRangeException(null, "Resampling factor must be a positive, nonzero value.");
			}

			_resampleFactor = value;
		}

	}
	private decimal _resampleFactor = 1.0M;

	/// <summary>
	/// The point at which input PCM samples will be truncated;
	/// if 0 or negative, the input is not truncated.
	/// </summary>
	public int Truncate {
		get => _truncate;
		set {
			if (value < 1) {
				value = -1;
			}
			_truncate = value;
		}
	}
	private int _truncate = -1;

	/// <summary>
	/// The number of leading zeros to force at the start of a sample.
	/// Negative values result in no enforcement.
	/// </summary>
	public int LeadingZeros {
		get => _leadingZeros;
		set {
			if (value < 0) {
				value = -1;
			} else if (value > MaxLeadingZeros) {
				value = MaxLeadingZeros;
			}
			_leadingZeros = value;
		}
	}
	private int _leadingZeros = -1;

	/// <summary>
	/// The maximum number of leading zeros allowed. Higher values will be clamped to this number.
	/// </summary>
	public const int MaxLeadingZeros = 100;

	/// <summary>
	/// Creates a new instance with no filters.
	/// </summary>
	protected BRREncoder() {
		Filters = PreEncodingFilters.NoFilter;
	}

	/// <summary>
	/// Creates a new instance with the given resampler and no filters.
	/// </summary>
	/// <param name="resampler">The resampling algorithm to use.</param>
	[SetsRequiredMembers]
	protected BRREncoder(ResamplingAlgorithm resampler) : this() {
		Resampler = resampler;
	}

	/// <summary>
	/// Creates a new instance with the given algorithms.
	/// </summary>
	/// <param name="resampler">The resampling algorithm to use.</param>
	/// <param name="filter">The prediciton filter(s) to use.</param>
	[SetsRequiredMembers]
	protected BRREncoder(ResamplingAlgorithm resampler, PreEncodingFilter filter) {
		Resampler = resampler;
		Filters = filter;
	}

	/// <summary>
	/// Encodes the given set of PCM samples to BRR data.
	/// </summary>
	/// <param name="pcmSamples">An array containing the samples to encode.</param>
	/// <param name="pcmLoopPoint">The sample within the passed array that should be used as a loop point. A value of <c>-1</c> indicates a non-looping sample.</param>
	/// <returns>A new <see cref="BRRSample"/> object containing the encoded samples.</returns>
	public BRRSample Encode(int[] pcmSamples, int pcmLoopPoint) {
		pcmSamples = [.. pcmSamples]; // create a new array to avoid screwing stuff up

		int leadingZeros = LeadingZeros;
		int truncate = Truncate;

		decimal resampleFactor = _resampleFactor;
		var resampler = Resampler;

		int samplesLength = pcmSamples.Length;

		if (truncate < 1 || truncate > samplesLength) {
			truncate = samplesLength;
		} else {
			samplesLength = truncate;
		}

		var (newLength, loopSize) = GetResamplingSizes(samplesLength, resampleFactor, pcmLoopPoint);
		pcmSamples = resampler(pcmSamples, samplesLength, newLength);

		int length = pcmSamples.Length;

		if (length != newLength) {
			throw new BRRConversionException("The resampling algorithm did not resample to the correct size.");
		}


		Filters(pcmSamples);

		// this shouldn't be possible, but just in case...
		if (pcmSamples.Length != length) {
			throw new BRRConversionException("Something went wrong that changed the size of the array during filtering.");
		}

		pcmSamples = PadSample(pcmSamples);

		// ditto
		if ((pcmSamples.Length % PCMBlockSize) is not 0) {
			throw new BRRConversionException("Something went wrong that threw the samples array out of alignment.");
		}

		int loopBlock = (loopSize < 0)
			? NoLoop
			: (pcmSamples.Length - loopSize) / PCMBlockSize;

		return RunEncoder(pcmSamples, loopBlock);
	}

	/// <summary>
	/// Calculates the appropriate target length for resampling and new loop location.
	/// </summary>
	/// <param name="inLength">The number of samples that will take part in resampling.</param>
	/// <param name="resampleFactor">The desired resample factor.</param>
	/// <param name="loopPoint">The loop point sample of the waveform.</param>
	/// <returns>A tuple containing the new length and the position of the loop relative the end of the sample.</returns>
	protected virtual (int newLength, int loopSize) GetResamplingSizes(int inLength, decimal resampleFactor, int loopPoint) {
		int targetLength;
		int loopSize = 0;

		if (loopPoint < 0) {
			targetLength = (int) decimal.Round(inLength / resampleFactor);
		} else {
			decimal oldLoopSize = (inLength - loopPoint) / resampleFactor;

			// New loopsize is the multiple of 16 that comes after loopsize
			loopSize = (int) decimal.Ceiling(oldLoopSize / PCMBlockSize) * PCMBlockSize;

			// Adjust resampling
			targetLength = (int) decimal.Round(inLength / resampleFactor * (loopSize / oldLoopSize));
		}

		return (targetLength, loopSize);
	}

	/// <summary>
	/// Pads the samples to enforce the leading zeros setting and align the length of the data to a multiple of 16.
	/// </summary>
	/// <param name="pcmSamples">The array of samples to pad.</param>
	/// <returns>A new array, padded to a multiple of 16 samples in length.</returns>
	private int[] PadSample(int[] pcmSamples) {
		int samplesLength = pcmSamples.Length;
		int leadingZeros = LeadingZeros;

		int fzero = 0;

		// start with what's needed for alignment
		int forceFront;

		if (leadingZeros < 0) {
			forceFront = PCMBlockSize - (samplesLength & 0xF); // add extra zeros for alignment
		} else {
			// find the first nonzero value, since we'll be trimming from there
			for (int i = 0; i < samplesLength; i++) {
				if (pcmSamples[i] is not 0) {
					fzero = i;
					break;
				}
			}

			forceFront = PCMBlockSize - ((samplesLength - fzero) & 0xF); // get a new padding, based on the removed zeros

			if (leadingZeros > MaxLeadingZeros) {
				leadingZeros = MaxLeadingZeros; // cap this
			}

			// account for the zeros that are required for alignment
			leadingZeros -= forceFront;

			// if we have more zeros to add, make sure it's a multiple of 16
			if (leadingZeros > 0) {
				forceFront += (leadingZeros + 15) & ~0xF;
			}
		}

		var samplesCopy = pcmSamples.AsSpan(fzero); // get a span over the real data
		
		// if we need nothing, we're fine
		if (forceFront is 0) {
			return [.. samplesCopy];
		}
		
		return [.. new int[forceFront], .. samplesCopy];
	}

	/// <summary>
	/// Runs this class's encoding algorithm over the given set of samples with the given loop block.
	/// </summary>
	/// <param name="pcmSamples">The PCM samples to encode.</param>
	/// <param name="loopBlock">The index of the loop block. -1 if no loop.</param>
	/// <returns>A new <see cref="BRRSample"/> object with the encoded samples.</returns>
	protected abstract BRRSample RunEncoder(int[] pcmSamples, int loopBlock);
}
