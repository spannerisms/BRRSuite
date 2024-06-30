global using static BRRSuite.Constants;

namespace BRRSuite;

/// <summary>
/// Provides constants related to the conversion and handling of BRR files.
/// </summary>
public static class Constants {
	//*****************************************************************************
	// Audio conversion
	//*****************************************************************************

	/// <summary>
	/// The number of bytes contained in a single block of BRR data.
	/// </summary>
	public const int BrrBlockSize = 9;

	/// <summary>
	/// The number of audio samples represented by a single BRR block.
	/// </summary>
	public const int PcmBlockSize = 16;

	/// <summary>
	/// The preferred bit-depth of a PCM file when converting to BRR.
	/// </summary>
	public const int PreferredBitsPerSample = 16;

	/// <summary>
	/// The default frequency when nothing is specified.
	/// </summary>
	internal const int DefaultFrequency = 32000;

	//*****************************************************************************
	// BRR block header
	//*****************************************************************************

	/// <summary>
	/// A bitmask for block headers to isolate the filter ID.
	/// </summary>
	public const byte FilterMask = 0b0000_1100;

	/// <summary>
	/// The number of shifts required to normalize the filter ID.
	/// </summary>
	public const int FilterShift = 2;

	/// <summary>
	/// A bitmask for block headers to isolate the range (sample shift).
	/// </summary>
	public const byte RangeMask = 0b1111_0000;

	/// <summary>
	/// The number of shifts required to normalize the range (sample shift).
	/// </summary>
	public const int RangeShift = 4;

	/// <summary>
	/// The bit marking the last block of a sample.
	/// </summary>
	public const byte EndFlag = 0b0000_0001;

	/// <summary>
	/// The bit indicating a sample is to loop.
	/// </summary>
	public const byte LoopFlag = 0b0000_0010;

	/// <summary>
	/// A mask that can be used to disable the loop flag.
	/// </summary>
	public const byte LoopFlagOff = 0b1111_1101;

	/// <summary>
	/// The default value used when a sample has no loop point.
	/// </summary>
	public const int NoLoop = -1;


}
