using System.Runtime.CompilerServices;

namespace BRRSuite;

/// <summary>
/// Provides a by-reference wrapper for one block of BRR data with nibble-level access methods.
/// </summary>
public readonly ref struct BRRBlock {
	// managed pointer to header
	private readonly ref byte _header;

	// DUMB (EPIC) HACK
	// ulong is 8 bytes in size, or 16 nibbles
	// this can give us fast manipulation of the 4-bit samples without any unsafe pointer or indexing nonsense
	private readonly ref ulong _samples;

	/// <summary>
	/// <u><b>Do not use this constructor.</b></u> Always throws <see cref="InvalidOperationException"/>.
	/// </summary>
	public BRRBlock() {
		throw new InvalidOperationException();
	}

	/// <summary>
	/// Privileged faster and direct access for <see cref="BRRSample.GetBlock(int)"/>.
	/// Skips error checking because it assumes the caller already did it.
	/// </summary>
	internal BRRBlock(ref byte headerByte) {
		_header = ref headerByte; // reference to header byte
		_samples = ref Unsafe.AddByteOffset(ref Unsafe.As<byte, ulong>(ref _header), 1); // recast as long, +1 byte to skip over header
	}

	/// <summary>
	/// Creates a new wrapper from the specified span.
	/// The input span must be 9 bytes in length.
	/// </summary>
	/// <param name="block">A span over 9 bytes of data to use for this block.</param>
	/// <exception cref="ArgumentException">If <paramref name="block"/> is not 9 bytes.</exception>
	public BRRBlock(Span<byte> block) {
		if (block.Length is not BrrBlockSize) {
			throw new ArgumentException($"The input span must have a length of {BrrBlockSize}.");
		}

		_header = ref block[0]; // reference to header byte
		_samples = ref Unsafe.AddByteOffset(ref Unsafe.As<byte, ulong>(ref _header), 1); // recast as long, +1 byte to skip over header
	}

	/// <summary>
	/// Creates a new wrapper from a byte array, indexed to the specified block.
	/// </summary>
	/// <param name="sample">The sample to capture a block from.</param>
	/// <param name="block">The block to capture.</param>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="NullReferenceException">If <paramref name="sample"/> is null.</exception>
	public BRRBlock(byte[] sample, int block) {
		// No need to check for sample being null
		// It will throw from trying to access .Length
		if ((sample.Length % BrrBlockSize) is not 0) {
			throw new ArgumentException($"The input array must have a length that is a multiple of {BrrBlockSize}.");
		}

		if (block < 0) {
			throw new ArgumentOutOfRangeException(nameof(block), $"{nameof(block)} should not be negative.");
		}

		block *= BrrBlockSize;

		if (block > (sample.Length - BrrBlockSize + 1)) {
			throw new ArgumentOutOfRangeException(nameof(block));
		}

		_header = ref sample[block]; // reference to header byte
		_samples = ref Unsafe.AddByteOffset(ref Unsafe.As<byte, ulong>(ref _header), 1); // recast as long, +1 byte to skip over header
	}

	/// <summary>
	/// <para>
	///     Provides a necessary correction depending on the endianness of the machine.
	/// </para>
	/// <para>
	///     For little-endian machines, each byte is still big-endian,
	///     with the high nibble holding the earlier sample.
	///     To account for that, we just need to flip the parity of the index with <c>i^0x1</c>.
	/// </para>
	///     For big-endian machines, the whole thing is big-endian.
	///     We need <c>i</c> to become <c>15-i</c>, but that's equivalent to <c>i^0xF</c> for our use case.
	/// <para>
	/// </para>
	/// </summary>
	private static readonly int IndexCorrection = BitConverter.IsLittleEndian ? 0b0001 : 0b1111;

	/// <summary>
	/// Provides 4-bit access to any of the 16 samples encoded in this block.
	/// </summary>
	/// <param name="i">The sample ([0,15]) to set.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public readonly byte this[int i] {
		get {
			// if any bit besides the bottom 4 is set, we're not in bounds
			// so just get rid of those bits and check for 0
			if ((i >> 4) != 0) { // very fast bounds checking
				throw new ArgumentOutOfRangeException();
			}

			i ^= IndexCorrection;
			i *= 4; // make it nibbles

			ulong ret = _samples >> i;
			return (byte) (ret & 0xF);
		}

		set {
			if ((i >> 4) != 0) { // very fast bounds checking
				throw new ArgumentOutOfRangeException();
			}

			i ^= IndexCorrection;
			i *= 4; // make it nibbles

			_samples &= ~(0xFUL << i); // create a mask to remove the nibble
			_samples |= (value & 0xFUL) << i; // mask the value to 4 bits and shift into place
		}
	}

	/// <summary>
	/// Gets or sets the header byte of the block.
	/// </summary>
	public readonly byte Header {
		get => _header;
		set => _header = value;
	}

	/// <summary>
	/// Gets or sets the filter specified by the header byte.
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
	/// Gets or sets the range specified by the header byte.
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
	/// Gets or sets the loop flag in the header byte.
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
	/// Gets or sets the end flag in the header byte.
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
