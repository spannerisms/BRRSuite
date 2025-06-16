# BRR format
The Bit Rate Reduction (BRR) format is a 4-bit species of adaptive differential pulse-code modulation (ADPCM).

I strongly recommend watching [RGME's video on BRR samples](https://www.youtube.com/watch?v=bgh5_gxT2eg).

----

## Structure
BRR files are divided into blocks of 9 bytes, with a 1 byte header followed by 8 bytes of sample data&mdash;16 samples each encoded in a signed two's-complement 4 bit value.

### Block header
The header is a single byte that encodes four separate fields, as shown below:

<table style="text-align:center; margin:0 auto;">
	<tr>
		<th>Bit</th>
		<th>7</th>
		<th>6</th>
		<th>5</th>
		<th>4</th>
		<th>3</th>
		<th>2</th>
		<th>1</th>
		<th>0</th>
	</tr>
	<tr>
		<th>Field</th>
		<td colspan="4">Range</td>
		<td colspan="2">Filter</td>
		<td>Loop</td>
		<td>End</td>
	</td>
</table>

#### Range
The range of a block defines how much each sample should be shifted when it is decoded. Values 0x0 through 0xC are defined, while the higher ranges&mdash;0xD, 0xE, 0xF&mdash;are considered invalid. These undefined ranges are functional, but they result in `0x0000` for positive values and `-0x0800` for negative values.

#### Filter
The filter of a block defines which prediction filter should be used for encoding samples.

#### Loop
The loop flag defines the behavior of the end block. If this flag is set, the sample will return to its loop point if the end flag is also set. Without an end flag, the loop flag does nothing.

#### End
The end flags marks the end of a sample. Once read, the sample will either stop or loop.

----

### Sample layout
The sample bytes within a block are ordered from first to last&mdash;as one would expect for a little endian processor; however, within each byte, this order is flipped.

As an example, consider the following block:
<!---- Using U+2001 for spacing without formatting ---->
&#x2001;&#x2001;&#x2001;&#x2001;&#x2001;
<samp>98 51 09 13 6D F3 13 A2 23</samp>

The header byte is `98`, followed by these 16 samples:
<!---- Using U+2001 for spacing without formatting ---->
&#x2001;&#x2001;&#x2001;&#x2001;&#x2001;
<samp>5 1 0 9 1 3 6 D F 3 1 3 A 2 2 3</samp>

In text, this seems rather obvious and trivial. But, if you're like me, you may see this as somewhat backwards after all your experience manipulating data for little-endian processors.

----

## Decoding
The equation for decoding sample *<samp>x</samp>* of BRR data is:

<!---- Using U+2001 for spacing without formatting ---->
&#x2001;&#x2001;&#x2001;&#x2001;&#x2001;
<i><samp>x = s2<sup>r&minus;1</sup> + ax<sub>1</sub> + bx<sub>2</sub></samp></i>

Where:
<br/>  *<samp>s</samp>* is the 4-bit, two's-complement value of the encoded sample
<br/>  *<samp>r</samp>* is the range of the block
<br/>  *<samp>x<sub>1</sub></samp>* is the value of the previous sample
<br/>  *<samp>x<sub>2</sub></samp>* is the value of the sample before the previous
<br/>  *<samp>a</samp>* and *<samp>b</samp>* are constants defined by the filter, as documented below:

| ID | a | b |
|:--:|:-:|:-:|
|  0 | 0      | 0 |
|  1 | 15/16  | 0 |
|  2 | 61/32  | &minus;15/16 |
|  3 | 115/64 | &minus;13/16 |

The resulting value *<samp>x</samp>* should then be clipped to 15-bits to match the behavior of the DSP. A static function for this is provided in `Conversion.Clip(int)`.

----

## Best practices
When a sample begins playing, all registers are in an indeterminate state. To avoid clicks, pops, and other garbage, use filter 0 on your initial block with three or more 0 samples at the start. This will effectively act as an initialization of the buffers used for decoding and interpolation.

When looping, the previous and next previous samples will be coming from the end block. For consistent behavior, use filter 0 on the loop block or take its values into account when encoding the end block.