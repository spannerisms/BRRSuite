using System.Runtime.CompilerServices;

namespace BRRSuite;

/// <summary>
/// Provides a by-reference wrapper for one block of BRR data with nibble-level access methods.
/// </summary>
public readonly ref struct BRRBlock {
	private readonly ref byte _header;

	// DUMB (EPIC) HACK
	// ulong is 8 bytes in size, or 16 nibbles
	// this can give us fast manipulation of the 4-bit samples without any unsafe pointer or indexing nonsense
	private readonly ref ulong _samples;

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
	/// <exception cref="ArgumentException"></exception>
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
	public BRRBlock(byte[] sample, int block) {
		if (sample is null) {
			this = default;
			return;
		}

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
	/// Provides 4-bit access to any of the 16 samples encoded in this block.
	/// 
	/// </summary>
	/// <param name="i">The sample ([0,15]) to set.</param>
	/// <exception cref="IndexOutOfRangeException"></exception>
	public readonly byte this[int i] {
		get {
			if (i is < 0 or > 15) {
				throw new IndexOutOfRangeException();
			}

			i ^= 1; // each byte is big endian, so flip parity
			i *= 4; // make it nibbles

			ulong ret = _samples >> i;
			return (byte) (ret & 0xF);
		}

		set {
			if (i is < 0 or > 15) {
				throw new IndexOutOfRangeException();
			}

			i ^= 1; // each byte is big endian, so flip parity
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
		get => (_header & LoopFlag) == 0;
		set {
			_header &= LoopFlagOff;

			if (value) {
				_header |= LoopFlag;
			}
		}
	}

	/// <summary>
	/// Gets or sets the end flag in the header byte.
	/// </summary>
	public readonly bool End {
		get => (_header & EndFlag) == 0;
		set {
			_header &= EndFlagOff;

			if (value) {
				_header |= EndFlag;
			}
		}
	}
}
