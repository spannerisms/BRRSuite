# Conversion
```csharp
public static class Conversion
```

Contains constants and methods for working with BRR data.

----

## Constant fields
| Method | Type | Value | Description |
| ------ |:----:|:-----:| ----------- |
| <samp>BRRBlockSize</samp> | <kbd>int</kbd> | `9` | The number of bytes in a BRR block.
| <samp>PCMBlockSize</samp> | <kbd>int</kbd> | `16` | The number of audio samples encoded in one BRR block.
| <samp>PreferredBitDepth</samp> | <kbd>int</kbd> | `16` | The preferred bit-depth for PCM files in this library.
| <samp>DSPFrequency</samp> | <kbd>int</kbd> | `32000` | The frequency of the SNES sound source DSP.
| <samp>DefaultVxPitch</samp> | <kbd>ushort</kbd> | `0x1000` | The value of DSP register `VxPITCH` that corresponds to 100% playback frequency.
| <samp>RangeMask</samp> | <kbd>byte</kbd> | `0b11110000` | A bit mask for extracting the range field from a BRR block header.
| <samp>RangeMaskOff</samp> | <kbd>byte</kbd> | `0b00001111` | A bit mask for removing the range field from a BRR block header.
| <samp>RangeShift</samp> | <kbd>int</kbd> | `4` | The number of shifts needed to normalize or position the range field.
| <samp>MaximumRange</samp> | <kbd>int</kbd> | `12` | The maximum range that produces useful, defined behavior.
| <samp>FilterMask</samp> | <kbd>byte</kbd> | `0b00001100` | A bit mask for extracting the filter ID field from a BRR block header.
| <samp>FilterMaskOff</samp> | <kbd>byte</kbd> | `0b11110011` | A bit mask for extracting the filter ID field from a BRR block header.
| <samp>FilterShift</samp> | <kbd>int</kbd> | `2` | The number of shifts needed to normalize or position the filter field.
| <samp>LoopFlag</samp> | <kbd>byte</kbd> | `0b00000010` | A bit mask for extracting the loop flag field from a BRR block header.
| <samp>LoopFlagOff</samp> | <kbd>byte</kbd> | `0b11111101` | A bit mask for removing the loop flag field from a BRR block header.
| <samp>NoLoop</samp> | <kbd>int</kbd> | `-1` | Indicates a sample does not loop.
| <samp>EndFlag</samp> | <kbd>byte</kbd> | `0b00000001` | A bit mask for extracting the end flag field from a BRR block header.
| <samp>EndFlagOff</samp> | <kbd>byte</kbd> | `0b11111110` | A bit mask for removing the end flag field from a BRR block header.


----

## Static methods
| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>GetRecommendedEncoder()</samp> | <kbd>BRREncoder</kbd> | Returns a [BRREncoder](./BRREncoder.md) that represents the generally optimal algorithms available in BRR Suite.
| <samp>GetPredictionFilter(int)</samp> | <kbd>PredictionFilter</kbd> | Returns a [PredictionFilter](PredictionFilter.md) delegate for the specified filter.
| <samp>GetPrediction(int, int, int)</samp> | <kbd>int</kbd> | Calculates the value of the prediction filter with the given ID for the given inputs.
| <samp>Clamp(int)</samp> | <kbd>int</kbd> | Clamps a value to a signed, 15-bit number; i.e. a range of [&minus;16384,+16383].
| <samp>Clip(int)</samp> | <kbd>int</kbd> | Clamps a value to a signed, 15-bit number, with emulation of the SNES DSP overflow glitches.
| <samp>ApplyRange(int, int)</samp> | <kbd>int</kbd> | Shifts a sample by the given range, with correct behavior for undefined range.
| <samp>SignExtend4Bit(int)</samp> | <kbd>int</kbd> | Sign extends a number from bit 3.
| <samp>SignExtend4Bit(short)</samp> | <kbd>short</kbd> | Sign extends a number from bit 3.
| <samp>GetBlockCount(int)</samp> | <kbd>int</kbd> | Returns the number of blocks required to cover a given length.
| <samp>GetBlockCount&lt;T&gt;(IEnumerable&lt;T&gt;)</samp> | <kbd>int</kbd> | Returns the number of blocks required to cover the given collection.
| <samp>GetBlockCount&lt;T&gt;(Span&lt;T&gt;)</samp> | <kbd>int</kbd> | Returns the number of blocks required to cover the given array.
| <samp>EncodeBlock(PCMBlock, BRRBlock, int, int, ref&nbsp;int, ref&nbsp;int)</samp> | <kbd>void</kbd> | Encodes and headers a block in place.
| <samp>EncodeSample(int, int, int, ref&nbsp;int, ref&nbsp;int)</samp> | <kbd>int</kbd> | Encodes a single sample and returns the encoded 4-bit value.
| <samp>GetGaussTable()</samp> | <kbd>int[]</kbd> | Returns a new array with a copy of the SNES sound source's Gaussian table.

### Encode methods
Take care to note that the methods `EncodeBlock` and `EncodeSample` expect 16-bit PCM samples and 15-bit DSP samples. This is important to know when calculating the error between the input value and the decoded value.

*Example:*
```csharp
	int inputSample = 24000; // signed 16-bit
	int p1 = 4000; // signed 15-bit
	int p2 = -2819; // signed 15-bit

	EncodeSample(inputSample, 3, 3, ref p1, ref p2);

	// p1 holds the value of inputSample once it is decoded
	int compareSample = p1 << 1; // p1 needs to be shifted to 16 bits for comparison

	int error = compareSample - inputSample;
```