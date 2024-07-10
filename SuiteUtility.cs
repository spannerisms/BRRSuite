// BRR Suite is licensed under the MIT license.

global using System;
global using System.IO;

global using static BRRSuite.Conversion;

using System.Runtime.CompilerServices;

//using System.Buffers;

[assembly: CLSCompliant(true)]

namespace BRRSuite;

/// <summary>
/// Just holds stuff common to the library.
/// </summary>
internal static class SuiteUtility {

	// TODO look into implementing this to reduce garbage collection
	//internal static readonly ArrayPool<int> SamplesPool = ArrayPool<int>.Create(100000, 50);

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadInt(byte[] data, int index) {
		return Unsafe.As<byte, int>(ref data[index]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static int ReadShort(byte[] data, int index) {
		return Unsafe.As<byte, ushort>(ref data[index]);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteInt(byte[] data, int index, int value) {
		Unsafe.As<byte, int>(ref data[index]) = value;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static void WriteShort(byte[] data, int index, int value) {
		Unsafe.As<byte, ushort>(ref data[index]) = (ushort) value;
	}

	internal static void WriteString(byte[] data, int index, string text) {
		foreach (var c in text) {
			data[index++] = (byte) c;
		}
	}

	internal static bool TestSubstring(byte[] data, int start, string test, out string? message) {
		string result = GetLatin1String(data, start, test.Length);

		if (string.Equals(result, test, StringComparison.Ordinal)) {
			message = null;
			return true;
		}

		message = $"Bad string at {start}: {result} | Expected: {test}";

		return false;
	}

	internal static string GetLatin1String(byte[] data, int start, int length) {
		char[] charList = new char[length];

		for (int i = 0; i < length; i++) {
			char charAdd = (char) data[start + i];

			switch (charAdd) {
				case < '\x20': // control characters
				case >= '\x7F' and < '\xA0': // DEL + more control characters
				case '\xAD': // SHY
				case '\xA0': // NBSP
					charAdd = '?';
					break;
			}

			charList[i] = charAdd;
		}

		return new string(charList);
	}

	/// <summary>
	/// Permanent copy for the library.
	/// </summary>
	internal static readonly System.Collections.Immutable.ImmutableArray<int> SuiteGaussTable = [.. GetGaussTable()];
}
