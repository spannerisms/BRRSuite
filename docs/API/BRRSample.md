# BRRSample
```csharp
public sealed class BRRSample
```
The `BRRSample` type is a container for holding and manipulating BRR sample data along with its loop point.

----

## Constructors

| Constructor | Description |
| ----------- | ----------- |
| <samp>BRRSample(int)</samp> | Initializes a new instance with the specified number of empty blocks. The total size of the data will be 9&times;`blocks`.
| <samp>BRRSample(byte[])</samp> | Initializes a new instance with a copy of the specified data. 
| <samp>BRRSample(Span&lt;byte&gt;)</samp> | Initializes a new instance with a copy of the specified data.

### Size constraints
When using an array or span, the length of the input data must be a nonzero multiple of 9 bytes in length. Passing data with an improperly aligned length will result in an `ArgumentException` being thrown.

The maximum data length of any `BRRSample` is 7,281 blocks (65,529 bytes). This limit is imposed with the size of the SNES APU's memory space in mind (65,536 bytes). Attempting to make an instance of the `BRRSample` class larger than this will throw an `ArgumentException`.

Likewise, zero and negative sizes are also not allowed.

----

## Properties

| Property | Access | Type | Description |
| -------- |:------:|:----:| ----------- |
| <samp>Item[Index]</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | Accesses the underlying data.
| <samp>BlockCount</samp> | <kbd>get</kbd> | <kbd>int</kbd> | Returns the number of blocks in the sample.
| <samp>Length</samp> | <kbd>get</kbd> | <kbd>int</kbd> | Returns `BlockCount * 9`. This is the length of the raw data in bytes.
| <samp>SampleCount</samp> | <kbd>get</kbd> | <kbd>int</kbd> | Returns `BlockCount * 16`. This is the number of samples encoded in the data.
| <samp>LoopBlock</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | The index of the block that should be returned to after reaching the end of the sample. A value of `-1` denotes a non-looping sample. Values less than 0 or past the end of the sample will be changed to `-1`.
| <samp>LoopPoint</samp> | <kbd>get</kbd> | <kbd>int</kbd> | Returns `LoopPoint * 9`. This is the number of bytes past the start of the sample where the loop point is located. Non-looping samples will return `-1`.
| <samp>IsLooping</samp> | <kbd>get</kbd> | <kbd>bool</kbd> | Returns `true` if the sample has a valid loop point.

### Loop points

The loop point of a `BRRSample` instance is always defined by the `LoopBlock` property. In other words: this class does not support the use of loop points outside the sample or misaligned with the sample data. These behaviors cannot be emulated without emulating the full memory of the APU, which is outside the scope of this class.

The [SuiteSample](SuiteSample.md) class includes support for defining such loop points. Accurate decoding must be done via other means.

----

## Instance methods

| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>GetBlock(int)</samp> | <kbd>BRRBlock</kbd> | Returns a [BRRBlock](BRRBlock.md) for the specified index.
| <samp>ToArray()</samp> | <kbd>byte[]</kbd> | Returns a new copy of the underlying sample data.
| <samp>AsSpan()</samp> | <kbd>Span&lt;byte&gt;</kbd> | Creates a new span over the underlying data.
| <samp>CorrectEndFlags()</samp> | <kbd>void</kbd> | Fixes the usage of end and loop flags.
| <samp>Validate()</samp> | <kbd>BRRDataIssue</kbd> | Validates this instance's data and properties.
| <samp>Decode()</samp> | <kbd>WaveContainer</kbd> | Decodes this instance's BRR data into PCM data. Uses a VxPitch of `0x1000` and a minimum length of `1.0`.
| <samp>Decode(int)</samp> | <kbd>WaveContainer</kbd> | Decodes this instance's BRR data into PCM data. Uses a minimum length of `1.0`.
| <samp>Decode(decimal)</samp> | <kbd>WaveContainer</kbd> | Decodes this instance's BRR data into PCM data. Uses a VxPitch of `0x1000`.
| <samp>Decode(int, decimal)</samp> | <kbd>WaveContainer</kbd> | Decodes this instance's BRR data into PCM data.
| <samp>Save(string, bool)</samp> | <kbd>void</kbd> | Saves as a raw data file. See [File formats](../fileformats.md).
| <samp>ExportWithHeader(string, bool)</samp> | <kbd>void</kbd> | Saves as raw data with a loop point header. See [File formats](../fileformats.md).

----

## Constant fields
| Method | Type | Value | Description |
| ------ |:----:| ----- | ----------- |
| <samp>Extension</samp> | <kbd>string</kbd> | `"brr"` | The preferred extension for raw brr data files.
| <samp>HeaderedExtension</samp> | <kbd>string</kbd> | `"brh"` | The preferred extension for raw brr data files with a header.

----

## Static methods
| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>ValidateBRRData(byte[])</samp> | <kbd>BRRDataIssue</kbd> | Validates raw data for BRR validity.
| <samp>ValidateBRRData(Span&lt;byte&gt;)</samp> | <kbd>BRRDataIssue</kbd> | Validates raw data for BRR validity.
| <samp>ValidateBRRData(byte[], int)</samp> | <kbd>BRRDataIssue</kbd> | Validates raw data with a known loop point for BRR validity.
| <samp>ValidateBRRData(Span&lt;byte&gt;, int)</samp> | <kbd>BRRDataIssue</kbd> | Validates raw data with a known loop point for BRR validity.
