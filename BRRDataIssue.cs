// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Flags problems with BRR sample data.
/// </summary>
[Flags]
public enum BRRDataIssue : int {
	/// <summary>
	/// No issues were found with this data.
	/// </summary>
	None                      = 0,

	// file size issues

	/// <summary>
	/// Raw data is not a multiple of 9.
	/// </summary>
	BadAlignment              = 1 << 0,

	/// <summary>
	/// The data is not large enough to be a proper sample. This issue is never resolvable.
	/// </summary>
	DataTooSmall              = 1 << 1,

	/// <summary>
	/// The data is far too large to reasonably be interpreted as a BRR sample.
	/// </summary>
	DataTooLarge              = 1 << 2,

	// header issues

	/// <summary>
	/// The final block's header does not contain the end of sample flag.
	/// </summary>
	MissingEndFlag            = 1 << 3,

	/// <summary>
	/// The end flag appears on one or more blocks before the final block.
	/// </summary>
	EarlyEndFlags             = 1 << 4,

	/// <summary>
	/// A loop point is defined, but the final block's header does not contain the loop flag.
	/// </summary>
	MissingLoopFlag           = 1 << 5,

	// loop point issues

	/// <summary>
	/// No loop point was specified, but the final block's header contains a loop flag. This issue is not easily resolvable if the sample should loop.
	/// </summary>
	MissingLoopPoint          = 1 << 6,

	/// <summary>
	/// The specified loop point is not aligned to a BRR block. This issue is not easily resolvable for self-looping samples.
	/// </summary>
	MisalignedLoopPoint       = 1 << 7,

	/// <summary>
	/// The specified loop point is past the end of the data. This issue is not easily resolvable for self-looping samples.
	/// </summary>
	OutOfRangeLoopPoint       = 1 << 8,

	// more header issues

	/// <summary>
	/// One or more blocks contain a range of 13 or higher.
	/// </summary>
	LargeRange                = 1 << 16,

	/// <summary>
	/// Block 0 is not using filter 0, which may cause unwanted decoding issues.
	/// </summary>
	Block0Filter              = 1 << 17,

	/// <summary>
	/// Block 0 begins with nonzero samples, which may cause errors in the Gaussian interpolation.
	/// </summary>
	Block0Samples             = 1 << 18,

	// container issues

	/// <summary>
	/// A <see cref="BRRSample"/> has a mismatch between its internal block count and the actual size of its data. This issue is never resolvable.
	/// </summary>
	WrongBlockCount           = 1 << 24,

	// meta issues

	/// <summary>
	/// The data has an issue that results in undefined, unusual, or potentially unwanted behavior.
	/// </summary>
	UndefinedBehavior         = 1 << 30,

	/// <summary>
	/// Issues were found which cannot be fixed without more information.
	/// </summary>
	Unresolvable              = 1 << 31,
}
