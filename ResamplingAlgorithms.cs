namespace BRRSuite;

/// <summary>
/// <para>
///     Encapsulates a method containing an algorithm to resample a specified portion of a given set of samples to a new size.
/// </para>
///
/// <para>
///     When creating an algorithm, it is expected that the input data not be modified,
///     thus it is required that a non-writable span be passed as the accessor to the data.
/// </para>
/// 
/// <para>
///     Preferably, new algorithms should implement error checking by calling
///     <see cref="ResamplingAlgorithms.ThrowIfInvalid(int, int, int)"/>
///     and include a fast copy method for when <paramref name="inLength"/> and <paramref name="outLength"/> are equal.
/// </para>
/// </summary>
/// <param name="samples">The data to be resampled.</param>
/// <param name="inLength">The length of data to use for resampling.</param>
/// <param name="outLength">The new length to resample to.</param>
/// <returns>A new array of integers containing the resampled data.</returns>
public delegate int[] ResamplingAlgorithm(ReadOnlySpan<int> samples, int inLength, int outLength);

/// <summary>
/// Combines a <see cref="ResamplingAlgorithm"/> with a human-readable name.
/// </summary>
public static class ResamplingAlgorithms {
	/// <summary>
	/// Throws an <see cref="ArgumentException"/> if any of the following occurs:<br/>
	/// • <paramref name="inLength"/> &gt; <paramref name="samplesLength"/><br/>
	/// • <paramref name="inLength"/> &lt; 1<br/>
	/// • <paramref name="outLength"/> &lt; 1
	/// </summary>
	/// <param name="samplesLength">The length of the input data.</param>
	/// <param name="inLength"><inheritdoc cref="ResamplingAlgorithm" path='/param[@name="inLength"]'/></param>
	/// <param name="outLength"><inheritdoc cref="ResamplingAlgorithm" path='/param[@name="outLength"]'/></param>
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

	/// <summary>
	/// A resampling algorithm that uses nearest-neighbor interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm NoInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength].ToArray();
			}

			double ratio = ((double) inLength) / outLength;

			int[] outBuf = new int[outLength];

			for (int i = 0; i < outLength; i++) {
				outBuf[i] = samples[(int) (i * ratio)];
			}

			return outBuf;
		};

	/// <summary>
	/// A resampling algorithm that uses linear interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm LinearInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength].ToArray();
			}

			double ratio = ((double) inLength) / outLength;
			int[] outBuf = new int[outLength];

			int lastSample = inLength - 1;

			for (int i = 0; i < outLength; i++) {
				int a = (int) (i * ratio); // Whole part of index

				if (a == lastSample) {
					outBuf[i] = samples[a];
				} else {
					double b = i * ratio - a; // Fractional part of index
					outBuf[i] = (int) ((1 - b) * samples[a] + b * samples[a + 1]);
				}
			}

			return outBuf;
		};

	/// <summary>
	/// A resampling algorithm that uses sinusoidal interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm SineInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength].ToArray();
			}

			double ratio = ((double) inLength) / outLength;
			int[] outBuf = new int[outLength];

			int lastSample = inLength - 1;

			for (int i = 0; i < outLength; i++) {
				int a = (int) (i * ratio);

				if (a == lastSample) {
					outBuf[i] = samples[a];
				} else {
					double b = i * ratio - a;
					double c = (1.0 - Math.Cos(b * Math.PI)) / 2.0;

					outBuf[i] = (int) ((1 - c) * samples[a] + c * samples[a + 1]);
				}
			}

			return outBuf;
		};

	/// <summary>
	/// A resampling algorithm that uses cubic interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm CubicInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength].ToArray();
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

	/// <summary>
	/// A resampling algorithm that uses band-limited interpolation.
	/// </summary>
	public static readonly ResamplingAlgorithm BandlimitedInterpolation =
		(samples, inLength, outLength) => {
			ThrowIfInvalid(samples.Length, inLength, outLength);

			// Fast copy
			if (inLength == outLength) {
				return samples[..inLength].ToArray();
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
