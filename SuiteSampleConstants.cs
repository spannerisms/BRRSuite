namespace BRRSuite;

/// <summary>
/// Contains constants for the BRR Suite Sample file specification.
/// </summary>
public static class SuiteSampleConstants {
	/// <summary>
	/// The preferred file extension for a BRR Suite Sample file.
	/// </summary>
	public const string Extension = "brs";

	/// <summary>
	/// The preferred name for BRR Suite Sample files.
	/// </summary>
	public const string FormatName = "BRR Suite Sample";

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

	// Header
	public const string FormatSignature                    = "BRRS";
	public const int FormatSignatureLocation               = 0;
	public const int ChecksumLocation                      = 4;
	public const int ChecksumComplementLocation            = 6;

	// Metadata block
	public const string MetaBlockSignature                 = "META";
	public const int MetaBlockLocation                     = 8;

	public const int MetaBlockSignatureLocation            = MetaBlockLocation+0;

	public const int InstrumentNameLocation                = MetaBlockLocation+4;
	public const int InstrumentNameLength                  = 24;
	public const int InstrumentNameEnd                     = InstrumentNameLocation+InstrumentNameLength;
	public const char InstrumentNamePadChar                = ' ';

	public const int PitchLocation                         = InstrumentNameEnd;
	public const int EncodingFrequencyLocation             = InstrumentNameEnd+4;

	// Data block
	public const string DataBlockSignature                 = "DATA";

	public const int DataBlockLocation                     = 51;
	public const int DataBlockSignatureLocation            = DataBlockLocation+0;

	public const int LoopTypeLocation                      = DataBlockLocation+4;
	public const byte NonloopingSample                     = 0;
	public const byte LoopingSample                        = 1;
	public const byte ForeignLoopingSample                 = 2;

	public const int LoopBlockLocation                     = DataBlockLocation+5;
	public const int LoopPointLocation                     = DataBlockLocation+7;
	public const int SampleBlocksLocation                  = DataBlockLocation+9;
	public const int SampleLengthLocation                  = DataBlockLocation+11;

	public const int SamplesDataLocation                   = 64;

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
