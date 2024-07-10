// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Contains functionality for creating filters that adjust audio waveforms before encoding to BRR.
/// </summary>
public static class PreEncodingFilters {
	/// <summary>
	/// Encapsulates a filter that does nothing.
	/// </summary>
	public static readonly PreEncodingFilter NoFilter = _ => { };

	// ported from BRRtools
	/// <summary>
	/// Creates a filter for treble boosting a waveform to compensate for the SNES Gaussian interpolation filter.
	/// </summary>
	/// <param name="matrix">A convolution matrix to run over each sample.</param>
	/// <returns>An anonymous function encapsulating the filter.</returns>
	public static PreEncodingFilter GetTrebleBoostFilter(double[] matrix) {
		int depth = matrix.Length;

		return (samples) => {
			int length = samples.Length;
			int[] samplesCopy = [.. samples];

			for (int i = 0; i < length; i++) {
				double acc = samples[i] * matrix[0];

				for (int k = depth - 1; k > 0; k--) {
					acc += matrix[k] * ((i + k < length) ? samplesCopy[i + k] : samplesCopy[^1]);
					acc += matrix[k] * ((i - k >= 0) ? samplesCopy[i - k] : samplesCopy[0]);
				}

				samples[i] = (int) acc;
			}
		};
	}

	// ported from BRRtools
	// Tepples' coefficient multiplied by 0.6 to avoid overflow in most cases <- idk what this means
	/// <summary>
	/// Returns a new convolution matrix containing the values used by the original BRRtools treble boost filter.
	/// </summary>
	/// <returns>An array of <see cref="double"/> values.</returns>
	public static double[] GetBRRtoolsTrebleMatrix() => [
		0.912962, -0.16199, -0.0153283, 0.0426783, -0.0372004, 0.023436, -0.0105816, 0.00250474
	];

	/// <summary>
	/// Creates a convolution matrix for Guassian compensation.
	/// </summary>
	/// <remarks>
	/// This is a formula discovered by Drexxx using the function <c>a*b^|x|</c>, where:
	/// <list type="bullet">
	///     <item>a = 16/sqrt(70)</item>
	///     <item>b = (16*sqrt(70)-16)/193</item>
	/// </list>
	/// </remarks>
	/// <param name="depth">The desired size of the output matrix. A recommended value is 4.</param>
	/// <returns>An array of <see cref="double"/> values.</returns>
	public static double[] GetDrexxxMatrix(int depth) {
		int length = depth;
		double[] ret = new double[length];

		for (int i = 0; i < length; i++) {
			//              a                             b                            x
			ret[i] = 1.9123657749350298 * Math.Pow(-0.3132730726295474, Math.Abs(i - depth));
		}

		return ret;
	}

	/// <summary>
	/// Creates a filter for boosting the amplitude of a waveform by a specified amount in a linear fashion.
	/// </summary>
	/// <param name="boost">The value to multiply every sample by, where 1.0 indicates no change in amplitude.</param>
	/// <returns>An anonymous function encapsulating the filter.</returns>
	public static PreEncodingFilter GetLinearAmplitudeFilter(double boost) {
		return (samples) => {
			int len = samples.Length;
			int[] ret = new int[len];

			for (int i = 0; i < len; i++) {
				samples[i] = (int) Math.Round(samples[i] * boost);
			}
		};
	}

	/// <summary>
	/// Finds the largest linear amplitude boost ratio of a waveform such that the largest magnitude of the waveform—positive or negative—will be
	/// transformed to a magnitude of 32600 (or close to it).
	/// </summary>
	/// <param name="samples">The set of samples to check.</param>
	/// <returns>A <see langword="double"/> ratio.</returns>
	public static double GetLinearBoostFactor(int[] samples) {
		int max = int.MinValue;
		int min = int.MaxValue;

		// find the largest numbers
		foreach (var n in samples) {
			if (n > max) {
				max = n;
			}
			
			if (n < min) {
				min = n;
			}
		}

		// get their absolute values
		max = Math.Abs(max);
		min = Math.Abs(min);

		// get the larger magnitude
		if (min > max) {
			max = min;
		}

		return 32600D / max;
	}
}
