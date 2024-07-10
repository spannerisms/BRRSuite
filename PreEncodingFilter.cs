// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Encapsulates a method that applies a filter to a waveform in place.
/// </summary>
/// <param name="samples">The PCM data to filter.</param>
public delegate void PreEncodingFilter(int[] samples);
