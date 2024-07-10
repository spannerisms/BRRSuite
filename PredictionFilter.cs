// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Encapsulates one of the four sampling filters used by the BRR format.
/// </summary>
/// <param name="p1">Amplitude of the sample 1 backwards.</param>
/// <param name="p2">Amplitude of the sample 2 backwards.</param>
/// <returns>The amplitude of the next sample.</returns>
public delegate int PredictionFilter(int p1, int p2);
