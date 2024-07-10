// BRR Suite is licensed under the MIT license.

using System.Runtime.CompilerServices;

namespace BRRSuite;

/// <summary>
/// Provides a by-reference wrapper for one BRR block with granular data access.
/// </summary>
public readonly ref struct BRRBlock {
	// managed pointer to header
	private readonly ref byte _header;

	// DUMB (EPIC) HACK
	// long is 8 bytes in size, or 16 nibbles
	// this can give us fast manipulation of the 4-bit samples without any unsafe pointer or indexing nonsense
	// using long because it facilitates sign extension for reading
	private readonly ref long _samples;

	/// <summary>
	/// <u><b>Do not use this constructor.</b></u> Always throws <see cref="InvalidOperationException"/>.
	/// </summary>
	[Obsolete("The default constructor BRRBlock() should not be used. Use the instance method BRRSample.GetBlock(int).", error: true)]
	public BRRBlock() {
		throw new InvalidOperationException();
	}

	/// <remarks>
	/// Fast and direct access for <see cref="BRRSample.GetBlock(int)"/>.
	/// Skips error checking because it assumes the caller already did it.
	/// </remarks>
	internal BRRBlock(ref byte headerByte) {
		_header = ref headerByte; // reference to header byte
		_samples = ref Unsafe.AddByteOffset(ref Unsafe.As<byte, long>(ref _header), 1); // recast as long, +1 byte to skip over header
	}

	/// <summary>
	/// Necessary correction for endianness of the machine.
	/// </summary>
	/// <remarks>
	/// <para>
	///     For little-endian machines, each byte is still big-endian,
	///     with the high nibble holding the earlier sample.
	///     To account for that, we just need to flip the parity of the index with <c>i^0x1</c>.
	/// </para>
	/// <para>
	///     For big-endian machines, the whole thing is big-endian.
	///     We need <c>i</c> to become <c>15-i</c>, which is equivalent to <c>i^0xF</c> for our use case.
	/// </para>
	/// </remarks>
	private static readonly int IndexCorrection = BitConverter.IsLittleEndian ? 0b0001 : 0b1111;

	/// <summary>
	/// Necessary correction for endianness of the machine.
	/// </summary>
	/// <remarks>
	/// <para>
	///     For little-endian machines, each byte is already big-endian,
	///     so no need to flip parity.
	/// </para>
	/// <para>
	///     For big-endian machines, no correction is needed.
	/// </para>
	/// </remarks>
	private static readonly int IndexCorrectionRead = BitConverter.IsLittleEndian ? 0b1111 : 0b0000;

	/// <summary>
	/// Provides signed 4-bit access to any of the 16 samples encoded in this block.
	/// </summary>
	/// <remarks>
	/// <para>
	/// When getting samples, the return value will be sign extended from bit 3.
	/// </para>
	/// <para>
	/// When setting samples, the input value is masked to the lowest 4 bits.
	/// </para>
	/// </remarks>
	/// <param name="sample">The sample ([0,15]) to access.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public readonly int this[int sample] {
		get {
			// if any bit besides the bottom 4 is set, we're not in bounds
			// so just get rid of those bits and check for 0
			if ((sample >> 4) != 0) { // very fast bounds checking
				throw new ArgumentOutOfRangeException();
			}

			sample ^= IndexCorrectionRead;
			sample *= 4; // make it nibbles

			// shift left to put value in the highest nibble for free sign extension
			long ret = _samples << sample;

			// shift to lowest nibble
			return (int) (ret >> 60);
		}

		set {
			if ((sample >> 4) != 0) { // very fast bounds checking
				throw new ArgumentOutOfRangeException();
			}

			sample ^= IndexCorrection;
			sample *= 4; // make it nibbles

			_samples &= ~(0xFL << sample); // create a mask to remove the nibble
			_samples |= (value & 0xFL) << sample; // mask the value to 4 bits and shift into place
		}
	}

	/// <summary>
	/// Gets a reference to the header byte of this block.
	/// </summary>
	public readonly ref byte Header {
		get => ref _header;
	}

	/// <summary>
	/// Gets or sets the range field in the header byte.
	/// </summary>
	public readonly int Range {
		get => (_header & RangeMask) >> RangeShift;
		set {
			value <<= RangeShift;
			value &= RangeMask;

			_header &= RangeMaskOff;
			_header |= (byte) value;
		}
	}

	/// <summary>
	/// Gets or sets the filter field in the header byte.
	/// </summary>
	public readonly int Filter {
		get => (_header & FilterMask) >> FilterShift;
		set {
			value <<= FilterShift;
			value &= FilterMask;

			_header &= FilterMaskOff;
			_header |= (byte) value;
		}
	}

	/// <summary>
	/// Gets or sets the loop flag field in the header byte.
	/// </summary>
	public readonly bool Loop {
		get => (_header & LoopFlag) != 0;
		set {
			if (value) {
				_header |= LoopFlag;
			} else {
				_header &= LoopFlagOff;
			}
		}
	}

	/// <summary>
	/// Gets or sets the end flag field in the header byte.
	/// </summary>
	public readonly bool End {
		get => (_header & EndFlag) != 0;
		set {
			if (value) {
				_header |= EndFlag;
			} else {
				_header &= EndFlagOff;
			}
		}
	}
}
