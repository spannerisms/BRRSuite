# PreEncodingFilter
```csharp
public delegate void PreEncodingFilter(int[] samples);
```

Encapsulates an algorithm that resamples a set of PCM samples.

Filters are designed to be run in succession from a single object as [multitask delegates](https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/delegates/how-to-combine-delegates-multicast-delegates). They operate on an array in place.

Predefined filters and method-writing utilities can be found in the static class `BRRSuite.PreEncodingFilters`.

----

## Multicasting

To run multiple filters on a sample, use the `+` or `+=` operator to chain delegates together.

*Example:*
```csharp
PreEncodingFilter filter = FilterX + FilterY;
filter += FilterZ;
```

## Unassigning

To reassign a filter to do nothing, use the static instance `PreEncodingFilters.NoFilter`.

*Example:*
```csharp
BRREncoder bebe = new ExampleEncoder();

bebe.Filters = PreEncodingFilters.NoFilter;
```

----

## Writing a new filter

### Best practices

If you'll need to reference the original array, make a copy of it and write to the original array.

*Example:*
```csharp
public void FilterX(int[] samples) {
	// create a new array with a copy of the samples
	int[] samplesCopy = [.. samples];

	for (int i = 0; i < samples.Length; i++) {
		int temp = samplesCopy[i];

		/* Implementation of algorithm */

		samples[i] = temp;
	}
}
```
----

### Adding more parameters

See [Tips &sect; Adding additional arguments to delegates](../tips.md#adding-additional-arguments-to-delegates).