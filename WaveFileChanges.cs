namespace BRRSuite;

/// <summary>
/// Flags changes made to a WAV file during loading.
/// </summary>
[Flags]
public enum WaveFileChanges {
	/// <summary>
	/// No changes were made to the source audio
	/// </summary>
	None = 0,

	/// <summary>
	/// The source audio contained multiple channels that were remixed down to a single channel.
	/// </summary>
	MixedToMono = 1 << 0,

	/// <summary>
	/// The source audio contained too many channels, and some of them were ignored.
	/// </summary>
	AdditionalChannelsIgnored = 1 << 1,

	/// <summary>
	/// The source audio was resampled to 16 bits per sample.
	/// </summary>
	ResampledTo16Bit = 1 << 8,

	/// <summary>
	/// The source audio had its amplitude changed.
	/// </summary>
	AmplitudeAdjusted = 1 << 16,
}
