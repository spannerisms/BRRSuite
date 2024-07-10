# BRREncoder
```csharp
public abstract class BRREncoder
```
An abstract class for defining the implementation and settings of an algorithm 

----

## Properties

These properties are present by default on every encoder.

| Property | Access | Type | Description |
| -------- |:------:|:----:| ----------- |
| <samp>Resampler</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>ResamplingAlgorithm</kbd> | A [ResamplingAlgorithm](./ResamplingAlgorithm.md) delegate encapsulating the algorithm that should be used for resampling PCM input.
| <samp>Filters</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>PreEncodingFilter</kbd> | A [PreEncodingFilter](./PreEncodingFilter.md) encapsulating filters to apply to a waveform before encoding.
| <samp>ResampleFactor</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>decimal</kbd> | The ratio between the sample rate of the audio files and the sample rate at which they should be encoded.
| <samp>Truncate</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | The point at which input PCM samples will be truncated; if 0 or negative, the input is not truncated.
| <samp>LeadingZeros</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | The number of leading zeros to enforce. See [&sect;&nbsp;Leading zeros](#leading-zeros) below.

----

### Leading zeros
The number of leading zeros to enforce at the start of a sample.

Negative values result in no enforcement being made. Values greater than the maximum are clamped to the maximum. The maximum number of leading zeros can be obtained from the static field `MaxLeadingZeros`.

Irrespective of this parameter, `Encode` will pad the beginning of the sample with up to 15 extra zeros for alignment, if necessary.

If a sample has more leading zeros than what is enforced, the extraneous zeros will be trimmed away; e.g., a sample with 1000 leading 0s being enforced to 3 will only have between 3 (the enforced number) and 18 (from padding) zeros.

----

## Public instance methods
| Method | Returns | Description |
| ------ |:-------:| ----------- |
| <samp>Encode(int[], int)</samp> | <kbd>BRRBlock</kbd> | Encodes a given set of samples to BRR data for the specified loop point with the current settings.

----

## Protected instance methods

| Method |  Returns | Description |
| ------ | :-------:| ----------- |
| <samp>[GetResamplingSizes(int, decimal, int)](#getresamplingsizes)</samp> | <kbd>(int,&nbsp;int)</kbd> | Calculates the optimal target length and loop point for a given length, loop point, and resampling factor.
| <samp>[RunEncoder(int&lbrack;&rbrack;, int)](#runencoder)</samp> | <kbd>void</kbd> | Implements the encodng algorithm for the type.

----

### GetResamplingSizes

The base class provides an implementation for this method based on the algorithm used by BRRtools. The purpose of this method is finding a size for the sample that aligns the loop point to the start of a block and the end to the end of a block. The method returns that new size and the number of samples between the loop point and the end in a tuple.

This method can be overridden to provide a different algorithm.

----

### RunEncoder

This is an abstract method that must be provided by all classes deriving from `BRREncoder`.

Given an array of PCM samples and a given loop block, this method should be able to encode the waveform data as a BRR sample and return a [BRRSample](./BRRSample.md) object containing that data.

----

## Further reading
- [Tips](../tips.md)