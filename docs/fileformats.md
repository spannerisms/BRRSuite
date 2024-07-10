# File formats

----

## Raw binary
In a raw BRR file (extension `.brr`), the entire contents of the file is the sample data.

----

## Loop-headered binary
In a loop-headered BRR file (extension: `.brh`), the first 2 bytes constitute the 16-bit loop offset of the sample in bytes. The rest of the file is the sample data.

----

## BRR Suite Sample
In a BRR Suite Sample file (extension: `.brs`), the file contents are a well formed header followed by the data.

A container for this file type can be made with the [SuiteSample type](./API/SuiteSample.md).

Constants for implementing the header can be found in `BRRSuite.SuiteSampleConstants`.

| Offset | Size | Type  | Value  | Contents |
| ------:|-----:|:-----:|:------:| -------- |
|      0 |    4 | ASCII | `BRRS` | BRR Suite Sample file signature[^1] |
|      4 |    2 | short | &ast;  | Sample checksum[^1] |
|      6 |    2 | short | &ast;  | Sample checksum complement[^1] |
|      8 |    4 | ASCII | `META` | Start of metadata block - signature[^1] |
|     12 |   24 | Latin&#8259;1[^0] | &ast;  | Sample name; padded with spaces (0x20) if fewer than 24 characters |
|     36 |    2 | short | &ast;  | The VxPITCH value that corresponds to a C; 0x0000 if unknown; default 0x1000 |
|     40 |    4 | int   | &ast;  | The frequency in hertz used for resampling during encoding |
|     44 |    7 | null  | `0`    | Reserved
|     51 |    4 | ASCII | `DATA` | Start of data block - signature[^1] |
|     55 |    1 | byte  | &ast;  | Loop behavior of the sample[^2] |
|     56 |    2 | short | &ast;  | Loop point offset of the sample in blocks[^3] |
|     58 |    2 | short | &ast;  | Loop point offset of the sample in bytes[^4] |
|     60 |    2 | short | &ast;  | Size of sample data in blocks |
|     62 |    2 | short | &ast;  | Size of sample data in bytes[^5] |
|     64 |   \* | bytes | &ast;  | Sample data[^6] |

[^0]: Also known as [ISO/IEC 8859-1](https://www.unicode.org/Public/MAPPINGS/ISO8859/8859-1.TXT). Latin-1 is an 8-bit encoding scheme that covers the Unicode blocks Basic Latin and Latin-1 Supplement (`\x0000–\x00FF`). The instrument name field supports the printable characters in these blocks; codepoints: `\x20–\x7E`, `\xA1—\xAC`, `\xAE–\xFF`. Invalid characters should be removed or converted to a space (`\x20`). The non-breaking space (`\xA0`) is preferably converted to a normal space.
[^1]: If this parameter is invalid, the entire file should be considered invalid.
[^2]: Whether this file loops <br/>0: Non-looping<br/>1: Looping<br/>2: Loops to different sample[^999]<br/>3: Loops to misaligned data[^999]
[^3]: 0x0000 is preferred for loop types 0, 2, 3.
[^4]: Should be `9*loopBlock` for loop type 1.
[^5]: Provided as a redundancy for file validation. Should be `9*blocks`; otherwise this parameter&mdash;and the thus entire file&mdash;is invalid.
[^6]: Should be a multiple of 9 bytes in length; otherwise this field&mdash;and thus the entire file&mdash;is invalid.

[^999]: This is technically a valid thing to do, but please don't.

----

### Checksum and complement
The checksum and its complement should have their bits flipped with respect to each other. In other words: `checksum XOR complement = 0xFFFF`.

The checksum is calculated as such:

1. Begin with a sum accumulator of 0
2. For each block:
	1. Reset the block accumulator
	2. Add the 8 bytes of the block, each shifted left by their index within the block minus 1
	3. Shift the header byte 4 bits left
	4. Exclusive OR the shifted header with the block accumulator
	5. Add the block accumulator to the sum accumulator
3. Truncate the sum accumulator to 16-bits

An implementation of this algorithm may be found in `SuiteSample.GetChecksum(Span<byte>)`.
