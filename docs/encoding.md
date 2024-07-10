# Using and writing an encoder

BRR Suite provides robust and dynamic control of BRR sample encoding by encapsulating the process in an object.

## Getting an encoder

If you just need simple encoding, you can use [one of the existing BRREncoder classes](https://github.com/spannerisms/BRRSuite/tree/main/Encoders):

- `BRRtoolsEncoder` - Ported from [BRRtools](https://github.com/Optiroc/BRRtools)

----

## Encoding a sample

The main meat, cheese, and potatoes is performed by calling the instance method `Encode` of a `BRREncoder` object. This method only takes two arguments:

<details open>
<summary><b><samp>pcmSamples</samp></b> <kbd>int[]</kbd></summary>

A copy of the audio samples to encode. These should be 16-bit samples sign-extended to 32 bits.
</details>

<details open>
<summary><b><samp>pcmLoopPoint</samp></b> <kbd>int</kbd></summary>

The sample number of the start of the loop point. Defaults to `-1`. This should be the loop point with respect to the input sample; i.e., it should not consider truncation, resampling, or padding when passed as a parameter.

If negative or beyond the length of the truncated input, the sample will not encode for a loop point.

This method will ensure the loop and end points are aligned with the beginning and end of a BRR block, respectively. The sample will be padded with silence at the beginning and/or have the resampling ratio adjusted to reach alignment.
</details>

----

## Configuring encoders

All encoders have several properties to control their function:

----

<details open>
<summary><b><samp>Resampler</samp></b> <kbd>ResamplingAlgorithm</kbd></summary>

Resampling algorithms are functions that can adjust the length of an input to a new length by interpolating or mixing data to minimize information loss. These methods are encapsulated in [ResamplingAlgorithm delegates](./API/ResamplingAlgorithm.md).

Several existing algorithms can be obtained from the `BRRSuite.ResamplingAlgorithms` class.

```csharp
public delegate int[] ResamplingAlgorithm(ReadOnlySpan<int> samples, int inLength, int outLength);
```

The input length is the sample to which the input should be truncated. It is not necessarily the same as the length of the input.

The output length is self-descriptively the length of the output after encoding. This will not necessarily be a multiple of 16; `BRRSample.Encode` performs alignment padding after resampling.</details>

<details open>
<summary><b><samp>pcmLoopPoint</samp></b> <kbd>int</kbd></summary>


Each encoder type will have its own settings that can be used to fine-tune its output. See [BRREncoder &sect; Properties](./API/BRREncoder.md#properties) for settings common to all encoders.

----

## Encoding process

Encoding is done by `BRREncoder.Encode` in the following order:

1. Truncate the sample if given a valid trim point
1. Determine if the sample loops
1. Determine the output length of the resampled data
1. Resample the data
1. Apply pre-encoding filters
1. Enforce leading zeros, if enabled
1. Pad front of data to align length
1. Encode sample
1. Return a `BRRSample`

Developers employing this library have fine control over steps 3, 4, 5, and 8. Existing methods are provided by BRR Suite to use for these parameters, but it is also possible to write entirely new algorithms, as detailed below.
