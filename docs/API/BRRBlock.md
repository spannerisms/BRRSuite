# BRRBlock
```csharp
public readonly ref struct BRRBlock
```
The `BRRBlock` type provides fast, efficient, type- and memory-safe access to a single 9-byte BRR block within a BRR sample. As a ref struct, it is subject to [a number limitations](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/ref-struct).

----

## Constructors
The `BRRBlock` type features no public constructors besides the default constructor, which should not be used, and which only exists as a requirement of struct types. Use of this constructor will always throw an `InvalidOperationException`.

Instead, use the instance method [BRRSample.GetBlock(int)](BRRSample.md#instance-methods).

----

## Properties

| Property | Access | Type | Description |
| -------- |:------:|:----:| ----------- |
| <samp>[Item&lbrack;int&rbrack;](#itemint)</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | 4-bit sample access.
| <samp>[Header](#header)</samp> | <kbd>get</kbd> | <kbd>ref&nbsp;byte</kbd> | A reference to the header byte.
| <samp>[Range](#range)</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | The range (shift) of this block.
| <samp>[Filter](#filter)</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>int</kbd> | The prediction filter ID of this block.
| <samp>[Loop](#loop)</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>bool</kbd> | The header's loop flag.
| <samp>[End](#end)</samp> | <kbd>get</kbd><br/><kbd>set</kbd> | <kbd>bool</kbd> | The header's end flag.

----

### Item[int]
```csharp
public int Item[int sample] { get; set; }
```
The indexer of a `BRRBlock` can be used to access any of the sixteen 4-bit sample values without masking or shifting on the part of the caller. Attempting to access a sample outside of the range `[0,15]` will result in an `ArgumentOutOfRangeException` being thrown.

When getting samples, only the lowest 4 bits are retrieved. Bits 4 through 31 will be sign extended from bit 3.

When setting samples, values outside the range `[−8,+7]` will be masked to the lowest 4 bits. This allows negative numbers to be passed and interpreted as signed, 4-bit values. Note that this means positive values may be interpreted as negative values. For example, `8` will be treated as `−8`.

*Example:*
```csharp
public static void SampleExample(BRRSample brr, int block) {
	BRRBlock brokenBlock = brr.GetBlock(block); // Get a BRRBlock over the specified data

	brokenBlock[0] = -4;  // changes sample 0
	brokenBlock[8] = 4;   // changes sample 8
	brokenBlock[9] = 0xF; // changes sample 9

	int a = brokenBlock[0]; // read sample 0; a = -4

	// the original data is changed by the BRRBlock
	// b now holds 0x4F from samples 8 and 9
	byte b = brr[block * 9 + 5];
}
```

----

### Header
```csharp
public ref byte header { get; }
```
Returns a reference to the header byte of the block.

*Example:*
```csharp
	BRRBlock blk = brr.GetBlock(9);

	blk.Header = 0xFF; // block header becomes FF
	byte g = blk.Header; // g is FF
	ref h = blk.Header; // h points to the block header
	h = 0xBE; // block header becomes BE; g is still FF
```

----

### Range
```csharp
public int Range { get; set; }
```
Gets or the sets the range field of the header byte (bits 4&ndash;7).

When getting this property, the header will be masked to only the range bits and shifted right four times to produce a value from 0&ndash;15.

When setting this property, a value from 0&ndash;15 is expected. The setter will mask away extra bits and shift the value into place.

----

### Filter
```csharp
public int Filter { get; set; }
```
Gets or the sets the filter field of the header byte (bits 2&ndash;3).

When getting this property, the header will be masked to only the filter bits and shifted right twice to produce a value from 0&ndash;3.

When setting this property, a value from 0&ndash;3 is expected. The setter will mask away extra bits and shift the value into place.

----

### Loop
```csharp
public bool Loop { get; set; }
```
Gets or sets the loop field of the header byte (bit 1).

This property's getter will return whether or not bit 1 is set in the header. The setter will set bit 1 when passed `true` and reset bit 1 when passed `false`.

----

### End
```csharp
public bool End { get; set; }
```
Gets or sets the end field of the header byte (bit 0).

This property's getter will return whether or not bit 0 is set in the header. The setter will set bit 0 when passed `true` and reset bit 0 when passed `false`.