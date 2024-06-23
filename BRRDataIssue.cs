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

	/// <summary>
	/// Indicates raw data was not a multiple of 9.
	/// </summary>
	WrongAlignment            = 1 << 0,

	/// <summary>
	/// Indicates raw data was not a multiple of 9, and that there appears to be a loop header.
	/// </summary>
	WrongAlignmentForHeadered = 1 << 1,

	/// <summary>
	/// Indicates that the data is not large enough to be a proper sample. This issue is never resolvable.
	/// </summary>
	DataTooSmall              = 1 << 2,

	/// <summary>
	/// Indicates that the data is far too large to reasonably be interpreted as a BRR sample.
	/// </summary>
	DataTooLarge              = 1 << 3,

	/// <summary>
	/// Indicates that the final block's header does not contain the end of sample flag.
	/// </summary>
	MissingEndFlag            = 1 << 4,

	/// <summary>
	/// Indicates that the end flag appears on any header other than the final block.
	/// </summary>
	EarlyEndFlags             = 1 << 5,

	/// <summary>
	/// Indicates that the final block's header does not contain the end of sample flag.
	/// </summary>
	MissingLoopFlag           = 1 << 6,

	/// <summary>
	/// Indicates that no loop point was specified for the data. This issue is never resolvable.
	/// </summary>
	MissingLoopPoint          = 1 << 7,

	/// <summary>
	/// Indicates that the specified loop point is not aligned to a BRR block. This issue is never resolvable for a looped sample but is technically okay for an unlooped sample.
	/// </summary>
	MisalignedLoopPoint       = 1 << 8,

	/// <summary>
	/// Indicates that the specified loop point is past the end of the data. This issue is never resolvable for a looped sample but is technically okay for an unlooped sample.
	/// </summary>
	OutOfRangeLoopPoint       = 1 << 9,

	/// <summary>
	/// Indicates issues were found with the data that cannot be fixed without more information.
	/// </summary>
	Unresolvable              = 1 << 31,
}
