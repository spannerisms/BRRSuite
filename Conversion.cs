// BRR Suite is licensed under the MIT license.

namespace BRRSuite;

/// <summary>
/// Provides constants and methods for encoding and decoding BRR sample files to and from Wave Sound files.
/// </summary>
public static class Conversion {
	//*****************************************************************************
	// Audio conversion
	//*****************************************************************************

	/// <summary>
	/// The number of bytes contained in a single block of BRR data.
	/// </summary>
	public const int BRRBlockSize = 9;

	/// <summary>
	/// The number of audio samples represented by a single BRR block.
	/// </summary>
	public const int PCMBlockSize = 16;

	/// <summary>
	/// The preferred bit-depth of a PCM file when converting to BRR.
	/// </summary>
	public const int PreferredBitDepth = 16;

	/// <summary>
	/// The sample rate of the SNES DSP.
	/// </summary>
	public const int DSPFrequency = 32000;

	/// <summary>
	/// The default value of VxPITCH that plays back at 32000 Hz.
	/// </summary>
	public const int DefaultVxPitch = 0x1000;

	//*****************************************************************************
	// BRR block header
	//*****************************************************************************

	/// <summary>
	/// A bitmask for block headers to isolate the range (sample shift).
	/// </summary>
	public const byte RangeMask = 0b1111_0000;

	/// <summary>
	/// A bitmask for block headers to remove the range (sample shift).
	/// </summary>
	public const byte RangeMaskOff = unchecked((byte) ~RangeMask);

	/// <summary>
	/// The number of shifts needed to normalize or position the range field.
	/// </summary>
	public const int RangeShift = 4;

	/// <summary>
	/// The maximum range that doesn't lead to unwanted behavior. Inclusive.
	/// </summary>
	public const int MaximumRange = 12;

	/// <summary>
	/// A bitmask for block headers to isolate the filter ID.
	/// </summary>
	public const byte FilterMask = 0b0000_1100;

	/// <summary>
	/// A bitmask for block headers to remove the filter.
	/// </summary>
	public const byte FilterMaskOff = unchecked((byte) ~FilterMask);

	/// <summary>
	/// The number of shifts needed to normalize or position the filter ID field.
	/// </summary>
	public const int FilterShift = 2;

	/// <summary>
	/// The bit indicating a sample is to loop.
	/// </summary>
	public const byte LoopFlag = 0b0000_0010;

	/// <summary>
	/// A mask that can be used to reset the loop flag.
	/// </summary>
	public const byte LoopFlagOff = unchecked((byte) ~LoopFlag);

	/// <summary>
	/// The value used to indicate a sample has no loop point.
	/// </summary>
	public const int NoLoop = -1;

	/// <summary>
	/// The bit marking the last block of a sample.
	/// </summary>
	public const byte EndFlag = 0b0000_0001;

	/// <summary>
	/// A mask that can be used to reset the end flag.
	/// </summary>
	public const byte EndFlagOff = unchecked((byte) ~EndFlag);

	//*****************************************************************************
	// Utility methods
	//*****************************************************************************

	/// <summary>
	/// Gets an encoder that contains the current best algorithm with the best settings.
	/// </summary>
	/// <remarks>
	/// <u><b>NOTE:</b></u> this is subject to change as the library evolves.
	/// </remarks>
	/// <returns>A new <see cref="BRREncoder"/> instance preconfigured to its recommended settings.</returns>
	public static BRREncoder GetRecommendedEncoder() {
		return new BRRtoolsEncoder() {
			Resampler = ResamplingAlgorithms.BandlimitedInterpolation,
			Filters = PreEncodingFilters.NoFilter,
			EnableFilter0 = true,
			EnableFilter1 = true,
			EnableFilter2 = true,
			EnableFilter3 = true,
			LeadingZeros = 3,
			ResampleFactor = 1.0M,
			Truncate = -1,
		};
	}

	// a = 0
	// b = 0
	private static int PredictionFilter0(int p1, int p2) => 0;

	// formula from fullsnes.txt
	// a = 0.9375    (15/16)
	// b = 0
	private static int PredictionFilter1(int p1, int p2) => p1 - (p1 >> 4);

	// formula from fullsnes.txt
	// a =  1.90625  (61/32)
	// b = -0.9375   (-15/16)
	private static int PredictionFilter2(int p1, int p2) => p1 * 2 + ((p1 * -3) >> 5) - p2 + (p2 >> 4);

	// formula from fullsnes.txt
	// a =  1.796875 (115/64)
	// b = -0.8125   (-13/16)
	private static int PredictionFilter3(int p1, int p2) => p1 * 2 + ((p1 * -13) >> 6) - p2 + ((p2 * 3) >> 4);

	// pre-init these for faster return
	private static readonly PredictionFilter pf0 = PredictionFilter0;
	private static readonly PredictionFilter pf1 = PredictionFilter1;
	private static readonly PredictionFilter pf2 = PredictionFilter2;
	private static readonly PredictionFilter pf3 = PredictionFilter3;

	/// <summary>
	/// Returns a <see cref="PredictionFilter"/> delegate encapsulating a filter.
	/// </summary>
	/// <param name="filter">The ID of the filter to use.</param>
	/// <exception cref="ArgumentOutOfRangeException">The requested filter ID is not 0–3.</exception>
	public static PredictionFilter GetPredictionFilter(int filter) => filter switch {
		0x00 => pf0,
		0x01 => pf1,
		0x02 => pf2,
		0x03 => pf3,

		_ => throw new ArgumentOutOfRangeException($"Not a valid filter: 0x{filter:X2}", nameof(filter))
	};

	/// <summary>
	/// Returns the result of a prediction filter with the specified samples.
	/// </summary>
	/// <param name="filter"><inheritdoc cref="GetPredictionFilter(int)" path="/param[@name='filter']"/></param>
	/// <param name="p1"><inheritdoc cref="PredictionFilter" path="/param[@name='p1']"/></param>
	/// <param name="p2"><inheritdoc cref="PredictionFilter" path="/param[@name='p2']"/></param>
	/// <returns>The prediction from the filter.</returns>
	/// <inheritdoc cref="GetPredictionFilter(int)" path="/exception"/>
	public static int GetPrediction(int filter, int p1, int p2) => filter switch {
		0x00 => 0,
		0x01 => p1 - (p1 >> 4),
		0x02 => p1 * 2 + ((p1 * -3) >> 5) - p2 + (p2 >> 4),
		0x03 => p1 * 2 + ((p1 * -13) >> 6) - p2 + ((p2 * 3) >> 4),

		_ => throw new ArgumentOutOfRangeException($"Not a valid filter: 0x{filter:X2}", nameof(filter))
	};

	/// <summary>
	/// Clamps a signed value to 15 bits.
	/// </summary>
	/// <param name="v">The value to clamp.</param>
	/// <returns>A new 15-bit value, sign extended into bits 15 through 31.</returns>
	public static int Clamp(int v) {
		if ((short) v != v) { // stolen from bsnes/blargg
			v >>= 31;
			v ^= 0x7FFF;
		}

		return (short) v;
	}

	/// <summary>
	/// Clamps a signed value to 15 bits with emulation of the hardware glitches.
	/// </summary>
	/// <param name="v">The value to clamp.</param>
	/// <inheritdoc cref="Clamp(int)" path="/returns"/>
	public static int Clip(int v) => v switch {
		>  0x7FFF    => (v + 0x7FFF) & 0x7FFF,
//		< -0x8000    => 0,          // clipped to 0
		< -0x7FFF    => 0,          // clipped to 0 - TODO i think this covers the hardware better than -0x8000?
		>  0x3FFF    => v - 0x8000, // [4000,7FFF] => [-4000,-1]
		< -0x4000    => v + 0x8000, // [-8000,-4001] => [0,-3FFF]
		_            => v,
	};

	/// <summary>
	/// Applies the range shift, with undefined range behavior emulated.
	/// </summary>
	/// <param name="sample">The 4-bit sample, sign-extended to 32-bits.</param>
	/// <param name="range">
	///     The number of shifts performed on the 4-bit value of the encoded sample. <c>[0,12]</c><br/>
	///     All other ranges will be treated exactly the same.
	///     Where negative values are maxed out, and positive values are clamped to zero.
	/// </param>
	/// <returns>The value of the shifted sample.</returns>
	public static int ApplyRange(int sample, int range) {
		if (range is < 0 or > MaximumRange) {
			sample >>= 31;
			sample <<= 11;
			return sample;
		}

		sample <<= range;
		return sample >> 1;
	}

	/// <summary>
	/// Treats a value as a signed, 4-bit number and sign-extends bit 3.
	/// </summary>
	/// <param name="s">The 4-bit number to sign extend.</param>
	/// <returns>A new value with bit 3 copied into all higher-order bits.</returns>
	public static int SignExtend4Bit(int s) {
		s <<= 28;
		s >>= 28;
		return s;
	}

	/// <inheritdoc cref="SignExtend4Bit(int)"/>
	public static short SignExtend4Bit(short s) {
		s <<= 12;
		s >>= 12;
		return s;
	}

	/// <summary>
	/// Gets the number of 16-sample blocks required to cover a given length.
	/// </summary>
	/// <param name="length">The length of the data to size for.</param>
	/// <returns>A block count, rounded up to the nearest multiple of 16.</returns>
	public static int GetBlockCount(int length) {
		return (length + 15) / PCMBlockSize; // +15 to round up
	}

	/// <summary>
	/// <para>
	///     Encodes a single block of BRR data in-place from a given set of samples.
	/// </para>
	/// <para>
	///     The parameters <paramref name="pcmBlock"/> and <paramref name="brrBlock"/> should capture
	///     existing memory containing the input audio samples and the output BRR block, respectively.
	/// </para>
	/// <para>
	///     This method is designed to be chained with itself by using the <see langword="ref"/> parameters
	///     to pass samples from one call to the next, where:<br />
	///     • <paramref name="p1"/> is the 15-bit previous sample<br/>
	///     • <paramref name="p2"/> is the 15-bit sample preceding <paramref name="p1"/><br/>
	///     These previous samples should generally be initialized to 0 at the start of conversion.
	/// </para>
	/// </summary>
	/// <param name="pcmBlock">
	///     A reference to the 16 PCM samples to encode.
	/// </param>
	/// <param name="brrBlock">
	///     A <see cref="BRRBlock"/> ref struct over the given block to encode.
	///     See also: <seealso cref="BRRSample.GetBlock(int)"/>.
	/// </param>
	/// <param name="range">
	///     <inheritdoc cref="ApplyRange(int, int)" path="/param[@name='range']"/>
	/// </param>
	/// <param name="filter">The ID of the filter to encode with. <c>[0,1,2,3]</c></param>
	/// <param name="p1">
	///     A reference to the 15-bit value of the most-recently encoded sample.
	///     When this method returns, <paramref name="p1"/> will contain the value of the newly encoded sample.
	/// </param>
	/// <param name="p2">
	///     A reference to the 15-bit value of the second-most-recenently encoded sample.
	///     When this method returns, <paramref name="p2"/> will contain the value that <paramref name="p1"/> held when this method was called.
	/// </param>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static void EncodeBlock(PCMBlock pcmBlock, BRRBlock brrBlock, int range, int filter, ref int p1, ref int p2) {
		if (range is < 0 or > MaximumRange) {
			throw new ArgumentOutOfRangeException($"Range must be between 0 and {MaximumRange}, inclusive.", nameof(range));
		}

		if (filter is < 0 or > 3) {
			throw new ArgumentOutOfRangeException("Filter must be between 0 and 3, inclusive.", nameof(filter));
		}

		for (int i = 0; i < PCMBlockSize; i++) {
			brrBlock[i] = EncodeSample(pcmBlock[i], range, filter, ref p1, ref p2);
		}

		// write header
		brrBlock.Range = range;
		brrBlock.Filter = filter;
	}


	/// <summary>
	/// Encodes a signed 16-bit sample to a signed 4-bit value for a given set of block parameters.
	/// The returned value will be the one which mostly closely matches the input sample.
	/// </summary>
	/// <remarks>
	/// Take care to note that <paramref name="sample"/> is a signed, 16-bit number,
	/// while <paramref name="p1"/> and <paramref name="p2"/> are signed, 15-bit numbers.
	/// </remarks>
	/// <param name="sample">The signed 16-bit sample to encode.</param>
	/// <param name="range">
	///     <inheritdoc cref="ApplyRange(int, int)" path="/param[@name='range']"/>
	/// </param>
	/// <param name="filter"><inheritdoc cref="EncodeBlock(PCMBlock, BRRBlock, int, int, ref int, ref int)" path="/param[@name='filter']"/></param>
	/// <param name="p1"><inheritdoc cref="EncodeBlock(PCMBlock, BRRBlock, int, int, ref int, ref int)" path="/param[@name='p1']"/></param>
	/// <param name="p2"><inheritdoc cref="EncodeBlock(PCMBlock, BRRBlock, int, int, ref int, ref int)" path="/param[@name='p2']"/></param>
	/// <returns>The signed 4-bit value this sample should be encoded as, given the input parameters with bit 3 sign-extended.</returns>
	/// <exception cref="ArgumentOutOfRangeException">A bad filter ID is passed.</exception>
	public static int EncodeSample(int sample, int range, int filter, ref int p1, ref int p2) {
		// return / scratch variable
		int ret;

		// Get the filter addend from the previous two samples
		int linearValue = GetPrediction(filter, p1, p2);

		if (range is < 0 or MaximumRange) {
			ret = sample >> 31;
			range = MaximumRange;
		} else { // valid ranges
			// get the difference between the sample (shifted right to normalize to 15-bit) and the filter addend
			ret = (sample >> 1) - linearValue;
			
			// find the ratio between the remaining difference and the range base
			double rat = (double) ret / (1 << range);
			
			// round to nearest integer
			ret = (int) (rat + 32.5D);
			
			ret -= 32;
			
			// bound to correct range
			if (ret < -8) {
				ret = -8;
			} else if (ret > 7) {
				ret = 7;
			}
		}

		// what was the previous sample is now the next previous sample
		p2 = p1;

		// the previous sample is now what we just encoded
		p1 = Clip(linearValue + ((ret << range) >> 1)); // TODO should this be clip or clamp?

		// return the encoded 4-bit, sign-extended value
		return ret;
	}

//		// brr tools method i don't totally understand
//		error = Clip(error) + (1 << (range + 2)) + ((1 << range) >> 2);
//
//		// default to the lowest value
//		ret = -8;
//
//		// TODO is error ever negative? need to investigate
//		if (error > 0) {
//			ret = (error << 1) >> range;
//
//			// keep the value in range
//			if (ret > 0xF) {
//				ret = 0xF;
//			}
//
//			// change the domain of ret from [0,15] to [-8,7]
//			ret -= 8;
//		}

	// lifted directly from fullsnes.txt
	/// <summary>
	/// Returns a new array of integers with the SNES Gaussian interpolation table.
	/// </summary>
	public static int[] GetGaussTable() =>
	[
		0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000,
		0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x002, 0x002, 0x002, 0x002, 0x002,
		0x002, 0x002, 0x003, 0x003, 0x003, 0x003, 0x003, 0x004, 0x004, 0x004, 0x004, 0x004, 0x005, 0x005, 0x005, 0x005,
		0x006, 0x006, 0x006, 0x006, 0x007, 0x007, 0x007, 0x008, 0x008, 0x008, 0x009, 0x009, 0x009, 0x00A, 0x00A, 0x00A,
		0x00B, 0x00B, 0x00B, 0x00C, 0x00C, 0x00D, 0x00D, 0x00E, 0x00E, 0x00F, 0x00F, 0x00F, 0x010, 0x010, 0x011, 0x011,
		0x012, 0x013, 0x013, 0x014, 0x014, 0x015, 0x015, 0x016, 0x017, 0x017, 0x018, 0x018, 0x019, 0x01A, 0x01B, 0x01B,
		0x01C, 0x01D, 0x01D, 0x01E, 0x01F, 0x020, 0x020, 0x021, 0x022, 0x023, 0x024, 0x024, 0x025, 0x026, 0x027, 0x028,
		0x029, 0x02A, 0x02B, 0x02C, 0x02D, 0x02E, 0x02F, 0x030, 0x031, 0x032, 0x033, 0x034, 0x035, 0x036, 0x037, 0x038,
		0x03A, 0x03B, 0x03C, 0x03D, 0x03E, 0x040, 0x041, 0x042, 0x043, 0x045, 0x046, 0x047, 0x049, 0x04A, 0x04C, 0x04D,
		0x04E, 0x050, 0x051, 0x053, 0x054, 0x056, 0x057, 0x059, 0x05A, 0x05C, 0x05E, 0x05F, 0x061, 0x063, 0x064, 0x066,
		0x068, 0x06A, 0x06B, 0x06D, 0x06F, 0x071, 0x073, 0x075, 0x076, 0x078, 0x07A, 0x07C, 0x07E, 0x080, 0x082, 0x084,
		0x086, 0x089, 0x08B, 0x08D, 0x08F, 0x091, 0x093, 0x096, 0x098, 0x09A, 0x09C, 0x09F, 0x0A1, 0x0A3, 0x0A6, 0x0A8,
		0x0AB, 0x0AD, 0x0AF, 0x0B2, 0x0B4, 0x0B7, 0x0BA, 0x0BC, 0x0BF, 0x0C1, 0x0C4, 0x0C7, 0x0C9, 0x0CC, 0x0CF, 0x0D2,
		0x0D4, 0x0D7, 0x0DA, 0x0DD, 0x0E0, 0x0E3, 0x0E6, 0x0E9, 0x0EC, 0x0EF, 0x0F2, 0x0F5, 0x0F8, 0x0FB, 0x0FE, 0x101,
		0x104, 0x107, 0x10B, 0x10E, 0x111, 0x114, 0x118, 0x11B, 0x11E, 0x122, 0x125, 0x129, 0x12C, 0x130, 0x133, 0x137,
		0x13A, 0x13E, 0x141, 0x145, 0x148, 0x14C, 0x150, 0x153, 0x157, 0x15B, 0x15F, 0x162, 0x166, 0x16A, 0x16E, 0x172,
		0x176, 0x17A, 0x17D, 0x181, 0x185, 0x189, 0x18D, 0x191, 0x195, 0x19A, 0x19E, 0x1A2, 0x1A6, 0x1AA, 0x1AE, 0x1B2,
		0x1B7, 0x1BB, 0x1BF, 0x1C3, 0x1C8, 0x1CC, 0x1D0, 0x1D5, 0x1D9, 0x1DD, 0x1E2, 0x1E6, 0x1EB, 0x1EF, 0x1F3, 0x1F8,
		0x1FC, 0x201, 0x205, 0x20A, 0x20F, 0x213, 0x218, 0x21C, 0x221, 0x226, 0x22A, 0x22F, 0x233, 0x238, 0x23D, 0x241,
		0x246, 0x24B, 0x250, 0x254, 0x259, 0x25E, 0x263, 0x267, 0x26C, 0x271, 0x276, 0x27B, 0x280, 0x284, 0x289, 0x28E,
		0x293, 0x298, 0x29D, 0x2A2, 0x2A6, 0x2AB, 0x2B0, 0x2B5, 0x2BA, 0x2BF, 0x2C4, 0x2C9, 0x2CE, 0x2D3, 0x2D8, 0x2DC,
		0x2E1, 0x2E6, 0x2EB, 0x2F0, 0x2F5, 0x2FA, 0x2FF, 0x304, 0x309, 0x30E, 0x313, 0x318, 0x31D, 0x322, 0x326, 0x32B,
		0x330, 0x335, 0x33A, 0x33F, 0x344, 0x349, 0x34E, 0x353, 0x357, 0x35C, 0x361, 0x366, 0x36B, 0x370, 0x374, 0x379,
		0x37E, 0x383, 0x388, 0x38C, 0x391, 0x396, 0x39B, 0x39F, 0x3A4, 0x3A9, 0x3AD, 0x3B2, 0x3B7, 0x3BB, 0x3C0, 0x3C5,
		0x3C9, 0x3CE, 0x3D2, 0x3D7, 0x3DC, 0x3E0, 0x3E5, 0x3E9, 0x3ED, 0x3F2, 0x3F6, 0x3FB, 0x3FF, 0x403, 0x408, 0x40C,
		0x410, 0x415, 0x419, 0x41D, 0x421, 0x425, 0x42A, 0x42E, 0x432, 0x436, 0x43A, 0x43E, 0x442, 0x446, 0x44A, 0x44E,
		0x452, 0x455, 0x459, 0x45D, 0x461, 0x465, 0x468, 0x46C, 0x470, 0x473, 0x477, 0x47A, 0x47E, 0x481, 0x485, 0x488,
		0x48C, 0x48F, 0x492, 0x496, 0x499, 0x49C, 0x49F, 0x4A2, 0x4A6, 0x4A9, 0x4AC, 0x4AF, 0x4B2, 0x4B5, 0x4B7, 0x4BA,
		0x4BD, 0x4C0, 0x4C3, 0x4C5, 0x4C8, 0x4CB, 0x4CD, 0x4D0, 0x4D2, 0x4D5, 0x4D7, 0x4D9, 0x4DC, 0x4DE, 0x4E0, 0x4E3,
		0x4E5, 0x4E7, 0x4E9, 0x4EB, 0x4ED, 0x4EF, 0x4F1, 0x4F3, 0x4F5, 0x4F6, 0x4F8, 0x4FA, 0x4FB, 0x4FD, 0x4FF, 0x500,
		0x502, 0x503, 0x504, 0x506, 0x507, 0x508, 0x50A, 0x50B, 0x50C, 0x50D, 0x50E, 0x50F, 0x510, 0x511, 0x511, 0x512,
		0x513, 0x514, 0x514, 0x515, 0x516, 0x516, 0x517, 0x517, 0x517, 0x518, 0x518, 0x518, 0x518, 0x518, 0x519, 0x519
	];
}
