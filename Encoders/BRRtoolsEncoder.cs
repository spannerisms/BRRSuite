// BRR Suite is licensed under the MIT license.
// Algorithm ported from mITroid which was ported from BRRtools
// Copyright (C) 2009 Bregalad, Kode54              BRRtools
// Copyright (C) 2013 Optiroc, nyanpasu64           BRRtools C port
// Copyright (C) 2018 tewtal/total                  BRRtools C# port

using System.Diagnostics.CodeAnalysis;

namespace BRRSuite;

/// <summary>
/// Implements the encoding algorithm used by BRRtools.
/// </summary>
public sealed class BRRtoolsEncoder : BRREncoder {
	/// <summary>
	/// Gets or sets whether filter 0 can be used during encoding. This does not apply to block 0.
	/// </summary>
	public bool EnableFilter0 {
		get => enableFilter0;
		set => enableFilter0 = value;
	}
	private bool enableFilter0 = true;

	/// <summary>
	/// Gets or sets whether filter 1 can be used during encoding.
	/// </summary>
	public bool EnableFilter1 {
		get => enableFilter1;
		set => enableFilter1 = value;
	}
	private bool enableFilter1 = true;

	/// <summary>
	/// Gets or sets whether filter 2 can be used during encoding.
	/// </summary>
	public bool EnableFilter2 {
		get => enableFilter2;
		set => enableFilter2 = value;
	}
	private bool enableFilter2 = true;

	/// <summary>
	/// Gets or sets whether filter 3 can be used during encoding.
	/// </summary>
	public bool EnableFilter3 {
		get => enableFilter3;
		set => enableFilter3 = value;
	}
	private bool enableFilter3 = true;

	/// <summary>
	/// Gets or sets whether the loop block will be forced to use filter 0.
	/// </summary>
	public bool UseFilter0ForLoop {
		get => filter0Loop;
		set => filter0Loop = value;
	}

	private bool filter0Loop = false;

	/// <inheritdoc cref="BRREncoder.BRREncoder()"/>
	public BRRtoolsEncoder() : base() { }

	/// <inheritdoc cref="BRREncoder.BRREncoder(ResamplingAlgorithm)"/>
	[SetsRequiredMembers]
	public BRRtoolsEncoder(ResamplingAlgorithm resampler) : base(resampler) { }

	/// <inheritdoc cref="BRREncoder.BRREncoder(ResamplingAlgorithm, PreEncodingFilter)"/>
	[SetsRequiredMembers]
	public BRRtoolsEncoder(ResamplingAlgorithm resampler, PreEncodingFilter filter) : base(resampler, filter) { }


	/// <inheritdoc cref="BRREncoder.RunEncoder(int[], int)"/>
	protected override BRRSample RunEncoder(int[] pcmSamples, int loopBlock) {
		int samplesLength = pcmSamples.Length;
		int blockCount = samplesLength / PCMBlockSize;

		int blockPos = 0;
		bool hasLoop = loopBlock >= 0;

		byte endFlags =  hasLoop ? (byte) (EndFlag|LoopFlag) : EndFlag;

		int P1 = 0, P1Loop = 0;
		int P2 = 0, P2Loop = 0;
		int filterAtLoop = 0;

		var brrOut = new BRRSample(blockCount) {
			LoopBlock = loopBlock,
		};

		// value to test for n being
		int loopTest = hasLoop switch {
			true => loopBlock * 16,
			false => NoLoop // -1 will never match
		};

		int endTest = samplesLength - PCMBlockSize;
		bool isLoopPoint = false;


		// forces filter 0 on block 0
		bool force0 = true;

		for (int n = 0; n < samplesLength; n += PCMBlockSize, blockPos += BRRBlockSize) {
			// Encode BRR block, tell the encoder if we're at loop point (if loop is enabled), and if we're at end point
			if (n == loopTest) {
				force0 |= filter0Loop; // | because = would override the block0 behavior if the loop is block 0
				isLoopPoint = true;
			}
			bool isEndPoint = n == endTest;

			double bestError = double.PositiveInfinity;

			PredictionFilter filterFunc;
			int filter;
			int bestFilter = 0;
			int bestRange = 0;

			bool write = false;

			if (force0) {
				MashAll(0);
				force0 = false;
			} else {
				if (enableFilter0) {
					MashAll(0);
				}

				if (enableFilter1) {
					MashAll(1);
				}

				if (enableFilter2) {
					MashAll(2);
				}

				if (enableFilter3) {
					MashAll(3);
				}
			}

			if (isLoopPoint) {
				filterAtLoop = bestFilter;
				P1Loop = P1;
				P2Loop = P2;
			}

			write = true;
			filter = bestFilter;
			filterFunc = Conversion.GetPredictionFilter(filter);

			ADPCMMash(bestRange);

			// Local functions
			// gets ugly here
			void MashAll(int filteri) {
				filterFunc = Conversion.GetPredictionFilter(filteri);
				filter = filteri;

				// fullsnes.txt says shift 0 is useless, so let's not use it
				for (int sa = 1; sa < 13; sa++) {
					ADPCMMash(sa);
				}
			}

			void ADPCMMash(int range) {
				double blockError = 0.0;

				int l1 = P1;
				int l2 = P2;
				int step = (1 << (range + 2)) + ((1 << range) >> 2);
				int sampleError, dp;

				bool even = true;
				int writeAt = blockPos + 1;

				for (int i = 0; i < PCMBlockSize; i++) {
					int da;
					int thisSample = pcmSamples[n + i];
					int linearValue = filterFunc(l1, l2) >> 1;

					// difference between linear prediction and current sample
					sampleError = (thisSample >> 1) - linearValue;

					if (sampleError < 0) {
						da = -sampleError;
					} else {
						da = sampleError;
					}

					if (da is > 16384 and < 32768) {
						sampleError = (sampleError >> 9) & 0x07FF_8000;
					}

					dp = sampleError + step;

					int c = 0;

					if (dp > 0) {
						// not allowing shift 0 for now
						c = (dp << 1) >> range;

						if (c > 0xF) {
							c = 0xF;
						}
					}

					c -= 8;

					dp = (c << range) >> 1; // quantized estimate of samp

					l2 = l1; // shift history

					l1 = linearValue + dp;

					if (l1 is < short.MinValue or > short.MaxValue) {
						l1 = (short) (0x7FFF - (l1 >> 24));
					}

					l1 <<= 1;

					sampleError = thisSample - l1;

					blockError += (double) sampleError * sampleError;

					if (write) {
						// don't copy this garbage
						// use BRRBlocks
						if (even = !even) {
							// odd samples
							brrOut[writeAt++] |= (byte) (c & 0x0F);
						} else {
							// even samples
							brrOut[writeAt] = (byte) (c << 4);
						}
					}
				}

				if (write) {
					P1 = l1;
					P2 = l2;

					int header = (range << RangeShift) | (filter << FilterShift);

					if (isEndPoint) {
						header |= endFlags; // Set the last block flags if we're on the last block
					}

					brrOut[blockPos] = (byte) header;
				} else {
					if (isEndPoint) {
						// Account for history points when looping is enabled & filters used
						switch (filterAtLoop) {
							case 0:
								blockError /= 16.0;
								break;

							// Filter 1
							case 1:
								int temp1 = l1 - P1Loop;
								blockError += (double) temp1 * temp1;
								blockError /= 17.0;
								break;

							// Filters 2 & 3
							default:
								int temp2 = l1 - P1Loop;
								blockError += (double) temp2 * temp2;
								temp2 = l2 - P2Loop;
								blockError += (double) temp2 * temp2;
								blockError /= 18.0;
								break;
						}
					} else {
						blockError /= 16.0;
					}

					if (blockError < bestError) {
						bestError = blockError;
						bestFilter = filter;
						bestRange = range;
					}
				}
			}
		}

		return brrOut;
	}
}
