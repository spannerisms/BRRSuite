namespace BRRSuite;

/// <summary>
/// Represents an audio sample rate given in hertz.
/// </summary>
public sealed class SampleRate : IComparable<SampleRate> {
	/// <summary>
	/// Gets the number of samples per second expressed by this frequency.
	/// </summary>
	public int Frequency { get; }

	/// <summary>
	/// Gets the frequency expressed as and rounded to the nearest kilohertz.
	/// </summary>
	public int FrequencykHz => Frequency / 1000;

	/// <summary>
	/// Gets the ratio between the SNES DSP frequency of 32000 and this frequency.
	/// </summary>
	public decimal Cram => 32000M / Frequency;

	/// <summary>
	/// Gets the value of this frequency with units (Hz).
	/// </summary>
	public string Name { get; }

	/// <summary>
	/// Creates a new instance of the <see cref="SampleRate"/> class with the specified frequency.
	/// </summary>
	/// <param name="frequency">The frequency of this sample. This value should be a positive, nonzero value.</param>
	/// <exception cref="ArgumentException">When <paramref name="frequency"/> is 0 or negative.</exception>
	public SampleRate(int frequency) {
		if (frequency < 1) {
			throw new ArgumentException("Frequency should be a positive, non-zero value.");
		}

		Name = $"{frequency} Hz";
		Frequency = frequency;
	}

	/// <inheritdoc cref="object.ToString()"/>
	public override string ToString() => Name;

	/// <summary>
	/// Calculates the ratio representing this sample rate resampled to the given target frequency.
	/// </summary>
	/// <param name="targetFrequency">The target frequency to resample to</param>
	/// <returns>A <see langword="decimal"/> resampling ratio.</returns>
	public decimal ResampleTo(int targetFrequency) {
		return (decimal) Frequency / targetFrequency;
	}

	/// <inheritdoc cref="ResampleTo(int)"/>
	public decimal ResampleTo(SampleRate targetFrequency) {
		return ResampleTo(targetFrequency.Frequency);
	}

	/// <inheritdoc cref="IComparable.CompareTo(object?)"/>
	/// <exception cref="ArgumentNullException">If <paramref name="other"/> is <see langword="null"/>.</exception>
	public int CompareTo(SampleRate? other) {
		if (other is null) {
			throw new ArgumentNullException(nameof(other), "Comparator argument was null.");
		}
		return Frequency.CompareTo(other.Frequency);
	}

	/// <summary>
	/// Represents a sample rate of 32000 Hz, the frequency of the SNES DSP.
	/// </summary>
	public static readonly SampleRate SR32000 = new(32000);

	/// <summary>
	/// Represents a sample rate of 16000 Hz.
	/// </summary>
	public static readonly SampleRate SR16000 = new(16000);

	/// <summary>
	/// Represents a sample rate of 8000 Hz.
	/// </summary>
	public static readonly SampleRate SR8000 = new(8000);

	/// <summary>
	/// Represents a sample rate of 4000 Hz.
	/// </summary>
	public static readonly SampleRate SR4000 = new(4000);

	/// <summary>
	/// Represents a sample rate of 44100 Hz, the standard for CD-quality audio.
	/// </summary>
	public static readonly SampleRate SR44100 = new(44100);
}
