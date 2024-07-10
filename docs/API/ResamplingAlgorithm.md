# ResamplingAlgorithm
```csharp
public delegate int[] ResamplingAlgorithm(int[] samples, int inLength, int outLength);
```

Encapsulates an algorithm that resamples a set of PCM samples.

Predefined algorithms and method-writing utilities can be found in the static class `BRRSuite.ResamplingAlgorithms`.

----

## Writing a new resampling algorithm

### Best practices

It is recommended you use the following pattern when creating new resampling algorithms:

*Example:*
```csharp
public int[] Exsample(int[] samples, int inLength, int outLength) {
	// Use library static method to perform bounds checking
	ResamplingAlgorithms.ThrowIfInvalid(samples.Length, inLength, outLength);

	// Fast copy when the array will not change size
	if (inLength == outLength) {
		return samples[..inLength];
	}
	
	// create a new array of the appropriate size to work on
	int[] outBuf = new int[outLength];

	/* Implementation of algorithm */

	// return the resampled array
	return outBuf;
};
```

----

#### Perform bounds checking

Ensure that the inputs are not negative and that the input length is not larger than the length of the PCM data. The BRR Suite library includes the static class method `ResamplingAlgorithms.ThrowIfInvalid(int,int,int)` for basic bounds checking.

#### Skip pointless resampling

If the input and output lengths are identical, then resampling will not be very fruitful. Compare the lengths and return an identical copy of the covered if both lengths are equal.

Fast copying can also be done more aggressively when the lengths are close or skipped entirely to apply certain corrections built into the algorithm.

#### Only resample

Avoid unnecessary functionality in resampling algorithms. Functions such as amplitude boosting or noise reduction should be performed with a [PreEncodingFilter](./PreEncodingFilter.md).

#### Use a buffer

You will often need to reference or copy values from the original data during resampling. Create a new buffer of the requested size and write the resampled data there. 

----

### Adding more parameters

See [Tips &sect; Adding additional arguments to delegates](../tips.md#adding-additional-arguments-to-delegates).