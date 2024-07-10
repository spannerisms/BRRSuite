// BRR Suite is licensed under the MIT license.
// Had to copy a couple things directly from Span.cs to get this working.
// Span.cs is licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed that file to me under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace BRRSuite;

/// <summary>
/// Creates a by-ref wrapper over one block of 16 PCM samples.
/// </summary>
public readonly ref struct PCMBlock {
	private readonly ref int _block;

	/// <summary>
	/// <u><b>Do not use this constructor.</b></u> Always throws <see cref="InvalidOperationException"/>.
	/// </summary>
	[Obsolete("The default constructor PCMBlock() should not be used.", error: true)]
	public PCMBlock() {
		throw new InvalidOperationException();
	}

	/// <summary>
	/// Creates a new <see cref="PCMBlock"/> over the specified samples.
	/// </summary>
	/// <param name="samples">the source of the samples.</param>
	/// <param name="block">The 16-sample block to get from the sample.</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public PCMBlock(int[] samples, int block) {
		block *= PCMBlockSize;

		uint index = (uint) block; // cute trick I stole from Span.cs

		if (index > (samples.Length - PCMBlockSize)) {
			throw new ArgumentOutOfRangeException();
		}

		_block = ref Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(samples), (nint) index);
	}

	/// <inheritdoc cref="PCMBlock(int[], int)"/>
	public PCMBlock(Span<int> samples, int block) {
		block *= PCMBlockSize;

		if (block > (samples.Length - PCMBlockSize) || block < 0) {
			throw new ArgumentOutOfRangeException();
		}

		_block = ref samples[block];
	}

	/// <summary>
	/// Gets a sample from the block.
	/// </summary>
	/// <param name="sample">The sample to index</param>
	/// <returns>A reference to the sample at the given index inside the block.</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public ref int this[int sample] {
		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		get {
			if ((sample >> 4) != 0) { // very fast bounds checking
				throw new ArgumentOutOfRangeException();
			}

			return ref Unsafe.Add(ref _block, (nint) (uint) sample);
		}
	}
}
