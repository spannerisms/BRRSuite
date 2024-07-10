// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Contains methods for creating <see cref="ResamplingAlgorithm"/> delegates.
/// </summary>
public static class ResamplingAlgorithms {
	/// <summary>
	/// Tests the parameters of a resampling invocation, throwing an error if they are not valid.
	/// </summary>
	/// <remarks>
	/// Throws an <see cref="ArgumentException"/> if any of the following is true:<br/>
	/// - <paramref name="inLength"/> &gt; <paramref name="samplesLength"/><br/>
	/// - <paramref name="inLength"/> &lt; 1<br/>
	/// - <paramref name="outLength"/> &lt; 1
	/// </remarks>
	/// <param name="samplesLength">The length of the input data.</param>
	/// <param name="inLength"><inheritdoc cref="ResamplingAlgorithm" path="/param[@name='inLength']"/></param>
	/// <param name="outLength"><inheritdoc cref="ResamplingAlgorithm" path="/param[@name='outLength']"/></param>
	/// <exception cref="ArgumentException">If a problem occurs</exception>
	public static void ThrowIfInvalid(int samplesLength, int inLength, int outLength) {
		if (inLength > samplesLength) {
			throw new ArgumentException(
				$"The input length for resampling should not be larger than the size of the data: {inLength}/{samplesLength}.",
				nameof(inLength));
		}

		if (inLength < 1) {
			throw new ArgumentException("The input length should not be 0 or negative.", nameof(inLength));
		}

		if (outLength < 1) {
			throw new ArgumentException("The output length should not be 0 or negative.", nameof(outLength));
		}
	}

	// ported from BRRtools
	/// <summary>
	/// A resampling algorithm that uses nearest-neighbor interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm NoInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength];
			}

			double ratio = ((double) inLength) / outLength;

			int[] outBuf = new int[outLength];

			for (int i = 0; i < outLength; i++) {
				outBuf[i] = samples[(int) (i * ratio)];
			}

			return outBuf;
		};

	// ported from BRRtools
	/// <summary>
	/// A resampling algorithm that uses cubic interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm CubicInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength];
			}

			double ratio = ((double) inLength) / outLength;
			int[] outBuf = new int[outLength];

			for (int i = 0; i < outLength; i++) {
				int a = (int) (i * ratio);

				short s0 = (short) ((a == 0) ? samples[0] : samples[a - 1]);
				short s1 = (short) samples[a];
				short s2 = (short) ((a + 1 >= inLength) ? samples[inLength - 1] : samples[a + 1]);
				short s3 = (short) ((a + 2 >= inLength) ? samples[inLength - 1] : samples[a + 2]);

				double a0 = s3 - s2 - s0 + s1;
				double a1 = s0 - s1 - a0;
				double a2 = s2 - s0;
				double b0 = i * ratio - a;
				double b2 = b0 * b0;
				double b3 = b2 * b0;

				outBuf[i] = (int) (b3 * a0 + b2 * a1 + b0 * a2 + s1);
			}

			return outBuf;
		};

	// ported from BRRtools
	/// <summary>
	/// A resampling algorithm that uses bandlimited interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm BandlimitedInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength];
			}

			double ratio = ((double) inLength) / outLength;
			int[] outBuf = new int[outLength];

			const int FIROrder = 15;
			if (ratio > 1.0) {
				int[] samples_antialiased = new int[inLength];
				double[] fir_coefs = new double[FIROrder + 1];

				// Compute FIR coefficients
				for (int k = 0; k <= FIROrder; k++) {
					fir_coefs[k] = Sinc(k / ratio) / ratio;
				}

				// Apply FIR filter to samples
				for (int i = 0; i < inLength; i++) {
					double acc = samples[i] * fir_coefs[0];
					for (int k = FIROrder; k > 0; k--) {
						acc += fir_coefs[k] * ((i + k < inLength) ? samples[i + k] : samples[inLength - 1]);
						acc += fir_coefs[k] * ((i - k >= 0) ? samples[i - k] : samples[0]);
					}
					samples_antialiased[i] = (int) acc;
				}
				samples = samples_antialiased;
			}

			// Actual resampling using sinc interpolation
			for (int i = 0; i < outLength; i++) {
				double a = i * ratio;
				double acc = 0.0;
				int aend = (int) (a + FIROrder) + 1;

				for (int j = (int) (a - FIROrder); j < aend; j++) {
					int sample;

					if (j >= 0) {
						if (j < inLength) {
							sample = samples[j];
						} else {
							sample = samples[inLength - 1];
						}
					} else {
						sample = samples[0];
					}

					acc += sample * Sinc(a - j);
				}

				outBuf[i] = (int) acc;
			}

			return outBuf;
		};

	/// <summary>
	/// Performs the normalized sinc function on a value.
	/// </summary>
	/// <param name="x">The argument of the sinc function.</param>
	/// <returns>The result of sinc(x).</returns>
	public static double Sinc(double x) {
		if (x == 0D) {
			return 1D;
		}

		return Math.Sin(Math.PI * x) / (Math.PI * x);
	}
}
