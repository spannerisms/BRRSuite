// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Indicates the looping behavior of a sample.
/// </summary>
public enum LoopBehavior : byte {
	/// <summary>
	/// The sample does not loop.
	/// </summary>
	NonLooping = 0,

	/// <summary>
	/// The sample loops within itself.
	/// </summary>
	Looping = 1,

	/// <summary>
	/// The sample loops to an arbitrary location outside of its data.
	/// </summary>
	Extrinsic = 2,

	/// <summary>
	/// The sample loops to a location within the sample's data but not properly aligned to the beginning of a block.
	/// </summary>
	Misaligned = 3,
}
