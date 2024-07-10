# PCMBlock
```csharp
public readonly ref struct PCMBlock
```
The `PCMBlock` type provides fast, efficient, type- and memory-safe access to a single 16-sample block within a PCM sample. As a ref struct, it is subject to [a number limitations](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct).

----

## Constructors

| Constructor | Description |
| ----------- | ----------- |
| <samp>PCMBlock()</samp> | Default constructor. Do not use. Always throws `InvalidOperationException`.
| <samp>PCMBlock(int[], int)</samp> | Initializes a new instance pointing to the first sample of the given block in the data.
| <samp>PCMBlock(Span&lt;int&gt;, int)</samp> | Initializes a new instance pointing to the first sample of the given block in the data.

----

## Properties

| Property | Access | Type | Description |
| -------- |:------:|:----:| ----------- |
| <samp>Item[int]</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>ref&nbsp;int</kbd> | Returns a reference to the specified sample within the block.
