// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Encapsulates a method containing an algorithm to resample a specified portion of a given set of samples to a new size.
/// </summary>
/// <remarks>
/// Preferably, new algorithms should implement error checking by calling
/// <see cref="ResamplingAlgorithms.ThrowIfInvalid(int, int, int)"/>
/// and include a short circuit for when <paramref name="inLength"/> and <paramref name="outLength"/> are equal.
/// </remarks>
/// <param name="samples">The array of samples to resample.</param>
/// <param name="inLength">The length of data to use for resampling.</param>
/// <param name="outLength">The length to resample to.</param>
/// <returns>A new array with the resampled data.</returns>
public delegate int[] ResamplingAlgorithm(int[] samples, int inLength, int outLength);
