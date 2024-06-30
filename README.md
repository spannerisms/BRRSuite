# BRR Suite

This is a C# library for converting modern, uncompressed audio files to the bit-rate reduction format (BRR) used by the Super Nintendo Entertainment System. For a user-friendly GUI employing this library, see my [BRR Suite GUI repository](https://github.com/spannerisms/BRRSuiteGUI).

# Acknowledgements
  * kan/spannerisms - me, the library author
  * Bregalad - original BRRtools developer
  * Kode54 - encoding algorithms
  * Optiric - C version: [BRRtools](https://github.com/Optiroc/BRRtools)
  * nyanpasu64 - C version
  * total - [original C# encoder](https://github.com/tewtal/mITroid/blob/master/mITroid/NSPC/BRR.cs)
  * _aitchFactor - whose concerns of, ideas for, and contributions to BRRtools gave me ideas and code to help future proof the implementation of this library
  * Drexxx - who found some better filtering that was relayed to me through _aitchFactor

# File formats

## .brr
In a raw BRR file (extension `.brr`), the entire contents of the file is the sample data.

## .brh
In a loop-headered BRR file (extension: `.brh`), the first 2 bytes constitute the 16-bit loop offset of the sample in bytes. The rest of the file is the sample data.

## .brs
In a BRR Suite Sample file (extension: `.brs`), the file contents are a well formed header followed by the data.

| Offset | Size | Type  | Value  | Contents |
| ------:|-----:|:-----:|:------:| -------- |
|      0 |    4 | ASCII | `BRRS` | BRR Suite data file signature[^1] |
|      4 |    2 | short | -      | Sample checksum[^1] |
|      6 |    2 | short | -      | Sample checksum complement |
|      8 |    4 | ASCII | `META` | Start of metadata block - signature[^1] |
|     12 |   24 | ASCII | -      | Sample name; padded with spaces (0x20) if fewer than 24 characters |
|     36 |    2 | short | -      | The VxPITCH value that corresponds to a C; 0x0000 if unknown; default 0x1000 |
|     40 |    4 | int   | -      | The frequency in hertz used for resampling during encoding |
|     44 |    7 | null  | 0      | Unused padding (possible expansion)
|     51 |    4 | ASCII | `DATA` | Start of data block - signature[^1] |
|     55 |    1 | byte  | -      | Whether this file loops (0: Non-looping, 1: Looping, 2: Loops to different sample[^2]) |
|     56 |    2 | short | -      | Loop point offset of the sample in blocks (0x0000 preferred for non-looping samples) |
|     58 |    2 | short | -      | Loop point offset of the sample in bytes[^3] |
|     60 |    2 | short | -      | Size of sample data in blocks |
|     62 |    2 | short | -      | Size of sample data in bytes[^3] |
|     64 |    * | bytes | -      | Sample data[^4] |

[^1]: If this parameter is invalid, the entire file should be considered invalid.
[^2]: This is technically a valid thing to do, but please don't.
[^3]: Provided as a redundancy for file validation. Should be `9*blocks`; otherwise this parameter&mdash;and the thus entire file&mdash;is invalid.
[^4]: Should be a multiple of 9 bytes in length; otherwise this parameter&mdash;and thus the entire file&mdash;is invalid.

### Checksum and complement
The checksum and its complement should have their bits flipped with respect to each other. In other words: `checksum XOR complement = 0x0000`.

The checksum is calculated as such:

1. Begin with a sum accumulator of 0
2. For each block:
	1. Reset the block accumulator
	2. Add the 8 bytes of the block, each shifted left by their index within the block minus 1
	3. Shift the header byte 4 bits left
	4. Exclusive OR the shifted header with the block accumulator
	5. Add the block accumulator to the sum accumulator
3. Truncate the sum accumulator to 16-bits


An implementation of this algorithm may be found in `BRRSample.GetChecksum(byte[])`.
