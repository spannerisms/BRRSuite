# Tips

This document contains tips for writing code employing this library.

----

## Use library methods and constants

The BRR Suite library includes a number of static utility classes:

- `Conversion` contains constants and methods for encoding, decoding, and manipulating BRR data.
- `ResamplingAlgorithms` contains algorithms methods and utility for writing new algorithms.
- `PreEncodingFilters` contains predefined filters and utility for writing new filters.
- `SuiteSampleConstants` contains constants for implementing the [BRR Suite Sample specification](./fileformats.md/#brr-suite-sample).

----

## Adding additional arguments to delegates

The method headers of the delegates used by encoders are non-negotiable. This is by design: the simplicity improves the overall workflow of using and writing algorithms; however, it comes with the drawback of limiting the level of fine control available to the algorithm. There is a very simple&mdash;yet powerfully dynamic&mdash;work-around:

To add more parameters to any algorithm, write a new method that returns an anonymous method as the respective delegate type. This creator method can take in any number of parameters that will be captured by the returned delegate.

*Example:*
```csharp
public static ResamplingAlgorithm GetResampler(bool arg1, int arg2)
	=> (int[] samples, int inLength, int outLength) => { // delegate parameters
		/* implementation of algorithm */
		/* arg1 and arg2 can be used within this scope */
	}
```

----

## Use PredictionFilter delegates

Use the method `Conversion.GetPredictionFilter(int)`. This will return a `PredictionFilter` delegate that can be invoked for each sample. The advantage of this pattern is only performing a check on the filter ID once per block. This leads to more readable code and a tiny performance boost[^0].

[^0]: Maybe

*Example:*
```csharp
int p0 = 0;
int p1 = 0;
int p2 = 0;

for (int f = 0; f < 4; f++) {
	PredictionFilter filter = GetPredictionFilter(f);

	for (int s = 0; s < 16; s++) {
		/* code implementing sample encoding */

		p0 = filter(p1, p2);

		/* more code */
	}
}
```

----

## Use block ref structs
The [BRRBlock](./API/BRRBlock.md) and [PCMBlock](./API/PCMBlock.md) types provide fast and efficient access to segments of data. Especially with BRR data, accessing individual samples or header properties requires slightly complex calculations. Keep your code simpler and neater by using these dedicated wrappers.