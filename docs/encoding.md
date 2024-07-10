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

Developers employing this library have fine control over steps 4, 5, and 8. Existing methods are provided by BRR Suite to use for these parameters, but it is also possible to write entirely new algorithms, as detailed below.

----

## Resampling algorithms



----

When writing a new resampling algorithm, ideally it should check for valid arguments and perform a fast copy when possible.

Basic argument checking can be performed with `ResamplingAlgorithms.ThrowIfInvalid(int, int, int)`.


See also: [§ Adding additional arguments to delegates](#adding-additional-arguments-to-delegates)

----

## Filter algorithms

```csharp
public delegate int[] PreEncodingFilter(ReadOnlySpan<int> samples);
```

A pre-encoding filter takes a set of samples and returns a new set of samples with some function applied. The output should be exactly the same length as the input.

Any number of filters can be passed to `BRRSample.Encode`, which accepts an array of `PreEncodingFilter` delegates. If no filters should be applied, pass `null`.

As with resampling algorithms, filter algorithms take input through a `ReadOnlySpan<int>` and return a new array.

See also: [§ Adding additional arguments to delegates](#adding-additional-arguments-to-delegates)

----

## Encoding algorithms

Encoding is the final step in the conversion process. When using `BRRSample.Encode`, the input passed through the `samples` parameter will be a multiple of 16 samples in length and already have any filters applied. This array will also be built from a copy of the original waveform, making it non-destructive to manipulate if required.

The method `BRRSample.Encode` requires passing of an `EncodingAlgorithm` delegate as a parameter. This should be a function that takes in an array of integers and an integer loop point, and returns a [BRRSample object](brrsample.md).

```csharp
public delegate BRRSample EncodingAlgorithm(int[] samples, int loopBlock);
```

Broadly speaking, we need to do the following to encode a ready-to-convert set of samples as a BRR sample:

- Pick a range and filter for each block
- Encode each of the sixteen 4-bit samples within the block
- Keep track of the output value for the last 2 samples

### Filters and ranges

With 4-bit samples alone, fidelity is extremely limited. The header byte of each BRR block contains a 4-bit range and a 2-bit filter ID that effectively expands the overall fidelity of a sample file. More technically speaking, BBR uses a form of Adaptive Differential Pulse-Code Modulation (ADPCM).

The `range` of the block is the number of left shifts to perform on each 4-bit sample. Range should be a value between 0 and 12, inclusive. The `filter` is a function that defines two coefficients for the decoding of the block.

----

### Pre-built methods for converting data

#### EncodeBlock

Entire blocks can be encoded by calling `Conversion.EncodeBlock`.

```csharp
public static void EncodeBlock(PCMBlock pcmBlock, BRRBlock brrBlock, int range, int filter, ref int p1, ref int p2)
```

----

<details open>
<summary><b><samp>pcmBlock</samp></b> <kbd>PCMBlock</kbd></summary>

A wrapper over the 16 contiguous samples in memory constituting the block to be encoded.

See the documentation of [PCMBlock](./API/PCMBlock.md) for more detail.

</details>

----

<details open>
<summary><b><samp>brrBlock</samp></b> <kbd>BRRBlock</kbd></summary>

A wrapper over the 9 bytes of contiguous memory the block should be encoded to.

See the documentation of [BRRBlock](./API/BRRBlock.md) for more detail.
</details>

----

<details open>
<summary><b><samp>range</samp></b> <kbd>int</kbd></summary>

Thhe range (0 to 12, inclusive) the block is to be encoded with.

While range is a 4-bit field, the values of `13`, `14`, and `15` are technically undefined. And while they have a determinate result, it is not useful for encoding.

All values outside of the valid range will be treated the same: positive values clipped to `0x000` and negative values clipped to `-0x800`.
</details>

----

<details open>
<summary><b><samp>filter</samp></b> <kbd>int</kbd></summary>

The filter ID (0, 1, 2, or 3) the block is to be encoded with. 

Attempting to pass any other value will result in an exception being thrown.
</details>

----

<details open>
<summary><b><samp>p1</samp></b>, <b><samp>p2</samp></b> <kbd>ref int</kbd></summary>

These parameters are the value of the previous sample and the value of the sample before the previous, respectively. These are passed by reference to allow automatic updating of the sample history.

Before any encoding, initialize two `int` variables to `0`. Pass these using `ref` for each call.
</details>

----

### Example

Below is an incomplete example encoding method illustrating the above points:

```csharp
public static BRRSample EncodeExample(int[] samples, int loopBlock) {
	int blockCount = samples.Length / 16;

	BRRSample brrRet = new BRRSample(blockCount) {
		LoopBlock = loopBlock
	};

	int p1 = 0; // previous sample
	int p2 = 0; // next previous sample

	for (int block = 0; block < blockCount; block++) {
		int filter, range;

		/* code to pick a filter and range */

		var encodeFrom = new PCMBlock(samples, block);
		var encodeTo = brrRet.GetBlock(block);
		BRRSample.Encode(encodeFrom, encodeTo, range, filter, ref p1, ref p2);
	}
}
```

See also: [§ Adding additional arguments to delegates](#adding-additional-arguments-to-delegates)


### Getting a filter

----
