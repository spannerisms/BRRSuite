# SuiteSample

```csharp
public sealed class SuiteSample
```
The `SuiteSample` type is a container for BRR sample data along with metadata describing how to use it.

This class provides an implementation of the [BRR Suite Sample specification](./fileformats.md/#brr-suite-sample).

----

## Constructors

| Constructor | Description |
| ----------- | ----------- |
| <samp>SuiteSample(BRRSample)</samp> | Initializes a new instance based on the given sample. 
| <samp>SuiteSample(Stream)</samp> | Initializes a new instance from a stream containing valid BRR Suite Sample file data.

----

## Properties

| Property | Access | Type | Description |
| -------- |:------:|:----:| ----------- |
| <samp>Sample</samp> | <kbd>get</kbd> | <kbd>BRRSample</kbd> | The underlying sample.
| <samp>InstrumentName</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>string</kbd> | A 24-character ISO Latin 1 string describing the sample. The property setter will enforce the character set and length when assigned to.
| <samp>VxPitch</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>ushort</kbd> | [See below](#vxpitch); Defaults to `0x1000`. 
| <samp>EncodingFrequency</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | The target frequency in hertz this sample was resampled to before being encoded. Defaults to 32000. 
| <samp>LoopBehavior</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>LoopBehavior</kbd> | Specifies the loop behavior of the sample.
| <samp>LoopPoint</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>ushort</kbd> | Specifies the location of the loop point.
| <samp>BlockCount</samp> | <kbd>get</kbd> | <kbd>int</kbd> | The number of blocks in this sample.
| <samp>SampleLength</samp> | <kbd>get</kbd> | <kbd>int</kbd> | The length of the sample data in bytes.

----

### VxPitch
This property indicates the value of the DSP register `VxPITCH` that when this sample is played back at such value, it will produce a tone that corresponds to the note C in some octave.

A value of `0x1000` plays back 1:1 at 32000 Hz. Doubling the value doubles the frequency (up one octave), and halving the value halves the frequency (down one octave).

A value of `0x0000` indicates that the C-producing frequency of this sample is unknown. The minimum valid value is `0x0001`, and the maximum valid value is `0x3FFF`. Values outside this range will be changed to `0x0000`.

----

### Loop points
By default, a `SuiteSample` will copy the loop point of its underlying sample. Setting the `LoopPoint` property of a `SuiteSample` instance will override the loop point of the underlying sample without changing it.

----

## Instance methods
| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>SetLoopPoint(int)</samp> | <kbd>void</kbd> | Sets the loop point with automatic correction of the loop type.
| <samp>ResetLoopPoint()</samp> | <kbd>void</kbd> | Resets the loop point to that of the underlying sample.
| <samp>GetNSPCMultiplier()</samp> | <kbd>ushort</kbd> | Gets a multiplier for N-SPC instrument definitions. Most implementations of N-SPC store this number in big-endian.
| <samp>Save(string, bool)</samp> | <kbd>void</kbd> | Save this instance to the given file path. See [File formats](../fileformats.md).

----

## Constant fields
| Method | Type | Value | Description |
| ------ |:----:| ----- | ----------- |
| <samp>Extension</samp> | <kbd>string</kbd> | `"brs"` | The preferred extension for BRR Suite Sample files.

----

## Static methods
| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>SetAndFlagLoopPoint(int)</samp> | <kbd>void</kbd> | Sets the loop point with automatic correction of the loop behavior property.
| <samp>ValidateSuiteSample(byte[], out string)</samp> | <kbd>bool</kbd> | Tests the given array of data for BRR Suite Sample file validity.
| <samp>ValidateSuiteSample(Stream, out string)</samp> | <kbd>bool</kbd> | Tests the given stream of data for BRR Suite Sample file validity.
| <samp>GetChecksum(Span&lt;byte&gt;)</samp> | <kbd>ushort</kbd> | Creates a checksum for the given data.
