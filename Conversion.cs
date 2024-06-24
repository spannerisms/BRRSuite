namespace BRRSuite;

/// <summary>
/// Encapsulates a method for finding the optimal parameters to encoding a set of samples to BRR.
/// </summary>
/// <param name="samples">The samples of the waveform to encode.</param>
/// <param name="loopBlock">Starting point of loop in BRR blocks.</param>
/// <returns>A new <see cref="BRRSample"/> object containing the data of the converted sample.</returns>
public delegate BRRSample EncodingAlgorithm(int[] samples, int loopBlock);

/// <summary>
/// Encapsulates one of the four sampling filters used by the BRR format.
/// </summary>
/// <param name="p1">Amplitude of the sample 1 backwards.</param>
/// <param name="p2">Amplitude of the sample 2 backwards.</param>
/// <returns>The amplitude of the next sample.</returns>
public delegate int PredictionFilter(int p1, int p2);

/// <summary>
/// Provides methods for encoding and decoding BRR sample files to and from Wave Sound files.
/// </summary>
public static class Conversion {
	/// <summary>
	/// The original BRRtools brute force algorithm with silent start enabled, and no disabled filters.
	/// Disable wrapping is not an option in BRR Suite.
	/// </summary>
	public static readonly EncodingAlgorithm BruteForce = GetBRRtoolsBruteForce(true, false, false, false, false);

	// a = 0
	// b = 0
	private static int PredictionFilter0(int p1, int p2) => 0;

	// a = 0.9375    (15/16)
	// b = 0
	private static int PredictionFilter1(int p1, int p2) => p1 - (p1 >> 4);

	// a =  1.90625  (61/32)
	// b = -0.9375   (-15/16)
	// formula from fullsnes.txt
	private static int PredictionFilter2(int p1, int p2) => p1 * 2 + ((p1 * -3) >> 5) - p2 + (p2 >> 4);

	// a =  1.796875 (115/64)
	// b = -0.8125   (-13/16)
	// formula from fullsnes.txt
	private static int PredictionFilter3(int p1, int p2) => p1 * 2 + ((p1 * -13) >> 6) - p2 + ((p2 * 3) >> 4);

	/// <summary>
	/// Gets a delegate encapsulating a filter.
	/// </summary>
	/// <param name="filter">The ID of the filter to use.</param>
	/// <returns>A <see cref="PredictionFilter"/> delegate for the filter.</returns>
	/// <exception cref="ArgumentOutOfRangeException">The input value cannot be interpreted as a filter</exception>
	public static PredictionFilter GetPredictionFilter(int filter) => filter switch {
		0x00 => PredictionFilter0,
		0x01 => PredictionFilter1,
		0x02 => PredictionFilter2,
		0x03 => PredictionFilter3,

		_ => throw new ArgumentOutOfRangeException($"Not a valid filter: 0x{filter:X2}", nameof(filter))
	};

	/// <summary>
	/// Clamps a signed value to 15 bits.
	/// </summary>
	/// <param name="v">The value to clamp.</param>
	/// <returns>A new 15-bit value, sign extended into bits 15..24 if necessary.</returns>
	public static int Clamp(int v) {
		if ((short) v != v) {
			v >>= 31;
			v ^= 0x7FFF;
		}

		return (short) v;
	}

	/// <summary>
	/// Clamps a signed value to 15 bits with emulation of the hardware glitches.
	/// </summary>
	/// <param name="v"></param>
	/// <returns>A new 15-bit value.</returns>
	public static int Clip(int v) => v switch {
		>  0x7FFF    => v - 2,      // equivalent to (p + 0x7FFF) & 0x7FFF
		< -0x8000    => 0,          // clipped to 0
		>  0x3FFF    => v - 0x8000, // [4000,7FFF] => [-4000,-1]
		< -0x4000    => v + 0x8000, // [-8000,-4001] => [0,-3FFF]
		_            => v,
	};

	/// <summary>
	/// <para>
	///     Encodes a single block of BRR data in-place from a given set of samples.
	/// </para>
	/// <para>
	///     The arguments <paramref name="pcmBlock"/> and <paramref name="brrBlock"/> should be 
	///     <see cref="Span{T}"/> of the respective type over existing data.
	/// </para>
	/// <para>
	///     This method is designed to be chained with itself by using the <see langword="ref"/> parameters
	///     to communicate samples from one block to the next, where:<br />
	///     • <paramref name="p1"/> is the previous sample<br/>
	///     • <paramref name="p2"/> is the sample preceding <paramref name="p1"/><br/>
	///     These previous samples should generally be initialized to 0 at the start of conversion.
	/// </para>
	/// <example>
	/// Example encoding with pre-chosen filters and ranges:
	/// <code>
	///    int x1 = 0;
	///    int x2 = 0;
	///    for (int i = 0; i &lt; sample.Length; i += 16) {
	///        var pcmSamples = new Span&lt;int&gt;(sample, i * 16, 16);
	///        var brrSamples = brr.GetBlockAt(i);
	///        Conversion.EncodeBlock(pcmSamples, brrSamples, range[i], filter[i], ref x1, ref x2);
	///    }
	/// </code>
	/// </example>
	/// </summary>
	/// <param name="pcmBlock">
	///     A span of length 15 over the waveform block to encode.
	///     See also: <seealso cref="WaveContainer.GetBlockAt(int[], int)"/>.
	/// </param>
	/// <param name="brrBlock">
	///     A span of length 9 over the BRR block that data should be written to.
	///     See also: <seealso cref="BRRSample.GetBlockAt(int)"/>.
	/// </param>
	/// <param name="range">The number of shifts performed on the 4-bit value of the encoded sample. <c>[1,12]</c></param>
	/// <param name="filter">The ID of the filter to encode with. <c>[0,1,2,3]</c></param>
	/// <param name="p1">
	///     A reference to the 15-bit value of the most-recently encoded sample.
	///     When this method returns, <paramref name="p1"/> will contain the value of the newly encoded sample.
	/// </param>
	/// <param name="p2">
	///     A reference to the 16-bit value of the second-most-recenently encoded sample.
	///     When this method returns, <paramref name="p2"/> will contain the value that <paramref name="p1"/> held when this method was called.
	/// </param>
	/// <exception cref="ArgumentException"></exception>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static void EncodeBlock(Span<int> pcmBlock, Span<byte> brrBlock, int range, int filter, ref int p1, ref int p2) {
		if (pcmBlock.Length is not PcmBlockSize) {
			throw new ArgumentException("The length of the input block must be 16 PCM samples.", nameof(pcmBlock));
		}

		if (brrBlock.Length is not BrrBlockSize) {
			throw new ArgumentException("The length of the input block must be 16 PCM samples.", nameof(pcmBlock));
		}

		if (range is < 1 or > 12) {
			throw new ArgumentOutOfRangeException("Range should be between 1 and 12, inclusive.", nameof(range));
		}

		if (filter is < 0 or > 4) {
			throw new ArgumentOutOfRangeException("Filter should be between 0 and 4, inclusive.", nameof(filter));
		}

		// actual algorithm
		bool odd = false;

		int brrX = 1; // start at 1 because header

		int accum = 0;
		foreach (var sample in pcmBlock) {
			// add in sample
			accum |= EncodeSample(sample, out int _, range, filter, ref p1, ref p2);

			if (odd) { // write every other sample
				brrBlock[brrX++] = (byte) accum; // write latest 2 samples (2 nibbles)
			}

			odd = !odd;

			// shift everything 4 bits over
			// the more significant bytes being old data is fine
			// as this is cast to a byte when written
			accum <<= 4;
		}

		// write header
		brrBlock[0] = (byte) ((range << RangeShift) | (filter << FilterShift));
	}

	/// <summary>
	/// Encodes a signed 16-bit sample to a signed 4-bit value for a given set of block parameters.
	/// This method uses <see langword="ref"/> parameters to handle the filter history.
	/// </summary>
	/// <param name="sample">The signed 16-bit sample to encode.</param>
	/// <param name="error">When this method returns, this will contain the delta between the original sample and its encoded value.</param>
	/// <param name="range">
	///     <inheritdoc cref="EncodeBlock(Span{int}, Span{byte}, int, int, ref int, ref int)" path='/param[@name="range"]'/><br/>
	///     <u><b>Caution:</b></u> this method does not include special casing or error checking for invalid range values.
	///     The caller of this routine should ensure that only valid ranges are passed to this method before calling.
	/// </param>
	/// <param name="filter"><inheritdoc cref="EncodeBlock(Span{int}, Span{byte}, int, int, ref int, ref int)" path='/param[@name="filter"]'/></param>
	/// <param name="p1"><inheritdoc cref="EncodeBlock(Span{int}, Span{byte}, int, int, ref int, ref int)" path='/param[@name="p1"]'/></param>
	/// <param name="p2"><inheritdoc cref="EncodeBlock(Span{int}, Span{byte}, int, int, ref int, ref int)" path='/param[@name="p2"]'/></param>
	/// <returns>The signed 4-bit value this sample should be encoded as, given the input parameters.</returns>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	public static int EncodeSample(int sample, out int error, int range, int filter, ref int p1, ref int p2) {
		// this method is an implementation of encoding for the formula:
		//         s(n) = d * 2^(r-15) + a*s(n-1) + b*s(n-2)
		//     where:
		//         s(n) indicates the n-th sample of the BRR
		//         d is the 4-bit, two's complement value encoded into the BRR (this is the value returned by this method)
		//         v = d * 2^(r-15) is sample addend
		//         r is the range (or shift) of the block
		//         a and b are constant coefficients determined by the filter:
		//             filter      a         b
		//                  0      0         0
		//                  1    15/16       0
		//                  2    61/32    -15/16
		//                  3   115/64    -13/16
		//         a*s(n-1) + b*s(n-2) together constitute the "filter addend"

		// Get the filter addend from the previous two samples
		// inlined here for speed
		int linearValue = filter switch {
			0 => 0,
			1 => p1 - (p1 >> 4),
			2 => p1 * 2 + ((p1 * -3) >> 5) - p2 + (p2 >> 4),
			3 => p1 * 2 + ((p1 * -13) >> 6) - p2 + ((p2 * 3) >> 4),
			_ => throw new ArgumentOutOfRangeException("Filter should be between 0 and 4, inclusive.", nameof(filter))
		};

		// get the difference between the sample (shifted right to normalize to 15-bit) and the filter addend
		error = (sample >> 1) - linearValue;

		// some magic stuff I still don't understand
		error = Clip(error) + (1 << (range + 2)) + ((1 << range) >> 2);

		// default to the lowest value
		int ret = 0x8; // signed 4 bit, so this is -8, but without sign extension

		if (error > 0) {
			ret = (error << 1) >> range;

			// keep the value in range
			if (ret > 0xF) {
				ret = 0xF;
			}

			// change the domain of ret from [0,15] to [-8,7]
			ret ^= 8; // same as ret -= 8, but without a sign extension into bits 4 through 31
					  // this avoids an extra & 0xF
		}

		// what was the previous sample is now the next previous sample
		p2 = p1;

		// the previous sample is now what we just encoded
		p1 = Clip(linearValue + ((ret << range) >> 1)); // TODO should this be clip or clamp?

		// calculate the error of this sample (normalizing p1 to 16-bit)
		error = sample - (p1 << 1);

		// return the 4-bit encoded value
		return ret;
	}

	/// <summary>
	/// Encodes a Wave Sound audio file to a BRR file with the given settings.
	/// </summary>
	/// <param name="wavSamples">Input PCM samples.</param>
	/// <param name="encoder">An encoding algorithm that converts a raw waveform to a BRR sample.</param>
	/// <param name="resampleAlgorithm">A <see cref="ResamplingAlgorithm"/> that will convert the input wav to a resampled output</param>
	/// <param name="resampleFactor">Desired resampling ratio.</param>
	/// <param name="truncate">Point at which input wave will be truncated; if 0 or negative, the input is not truncated.</param>
	/// <param name="loopStart">Starting point of loop in samples</param>
	/// <param name="trimLeadingZeroes">Enables the encoder to remove leading zeros before adding an initial block</param>
	/// <param name="waveFilters">An array of filters to apply to the sample data after it is resized and resampled.</param>
	/// <returns>A new <see cref="BRRSample"/> object containing the data and metadata of the converted sample.</returns>
	/// <exception cref="BRRConversionException"></exception>
	public static BRRSample Encode(int[] wavSamples, EncodingAlgorithm encoder, ResamplingAlgorithm resampleAlgorithm, decimal resampleFactor,
		int truncate = -1, int loopStart = NoLoop, bool trimLeadingZeroes = false, PreEncodingFilter[]? waveFilters = null) {

		int samplesLength = wavSamples.Length;

		if (truncate < 1 || truncate > samplesLength) {
			truncate = samplesLength;
		} else {
			samplesLength = truncate;
		}

		bool hasLoop = true;

		if (loopStart < 0) {
			hasLoop = false;
			loopStart = truncate;
		}

		// Output buffer
		int targetLength;
		int loopSize = 0;

		if (!hasLoop) {
			targetLength = (int) Math.Round(samplesLength / resampleFactor);
		} else {
			decimal oldLoopSize = (samplesLength - loopStart) / resampleFactor;

			// New loopsize is the multiple of 16 that comes after loopsize
			loopSize = (int) (Math.Ceiling(oldLoopSize / PcmBlockSize) * PcmBlockSize);

			// Adjust resampling
			targetLength = (int) Math.Round(samplesLength / resampleFactor * (loopSize / oldLoopSize));
		}

		decimal bsResampleRatio = (decimal) samplesLength / targetLength;

		int[] samples = resampleAlgorithm(wavSamples, samplesLength, targetLength);

		// Apply any filters to the sample now
		if (waveFilters is not null) {
			Array.ForEach(waveFilters, filt => {
				if (filt is null) return;

				samples = filt(samples);

				if (samples.Length != targetLength) {
					throw new BRRConversionException("Something is wrong with a filter that changed the size of the sample data.");
				}
			});
		}

		if (trimLeadingZeroes) {
			int fzero = Array.FindIndex(samples, i => i is not 0);
			// if you get -1, wtf happened to your sample?
			if (fzero is > 0) {
				samples = samples[fzero..];
			}
		}

		samplesLength = samples.Length;

		if ((samplesLength % PcmBlockSize) is not 0) {
			int padding = PcmBlockSize - (samplesLength & 0xF);

			int[] padSamples = new int[samplesLength + padding];
			samples.CopyTo(padSamples, padding);
			samples = padSamples;
			samplesLength += padding;
		}

		int loopBlock = hasLoop switch {
			true => (samplesLength - loopSize) / PcmBlockSize,
			false => NoLoop
		};

		BRRSample ret = encoder(samples, loopBlock);

		ret.ResampleRatio = resampleFactor;

		return ret;
	}

	/// <summary>
	/// Creates a brute force algorithm with specific parameters based on the original BRRtools algorithm.
	/// </summary>
	/// <param name="silentStart">Enables the algorithm to encode a block of complete silence at the start.</param>
	/// <param name="disableFilter0">Requests that the algorithm not perform block encoding with filter 0. This does not apply to the initial block.</param>
	/// <param name="disableFilter1">Requests that the algorithm not perform block encoding with filter 1.</param>
	/// <param name="disableFilter2">Requests that the algorithm not perform block encoding with filter 2.</param>
	/// <param name="disableFilter3">Requests that the algorithm not perform block encoding with filter 3.</param>
	/// <returns>An anonymous function encoding the algorithm.</returns>
	public static EncodingAlgorithm GetBRRtoolsBruteForce(bool silentStart, bool disableFilter0, bool disableFilter1, bool disableFilter2, bool disableFilter3) =>
		(int[] samples, int loopBlock) => {
			int samplesLength = samples.Length;
			int blockCount = samplesLength / PcmBlockSize;

			int blockPos = 0;
			bool hasLoop = loopBlock >= 0;

			bool force0 = true;

			byte endFlags =  hasLoop ? (byte) (EndFlag|LoopFlag) : EndFlag;

			int P1 = 0, P1Loop = 0;
			int P2 = 0, P2Loop = 0;
			int filterAtLoop = 0;

			// add 16 silent samples at the start if necessary and requested
			if (silentStart) {
				for (int i = 0; i < 16; i++) {
					if (samples[i] is not 0) {
						force0 = false; // no more need to force block 0
						loopBlock++;
						blockCount++;
						blockPos += BrrBlockSize;
						// shouldn't need to tell the brr to initialize, since an array of bytes initializes to 0
						break;
					}
				}

			}

			var brrOut = new BRRSample(blockCount) {
				LoopBlock = loopBlock,
			};

			// value to test for n being
			int loopTest = hasLoop switch {
				true => loopBlock * 16,
				false => NoLoop
			};

			int endTest = samplesLength - PcmBlockSize;

			for (int n = 0; n < samplesLength; n += PcmBlockSize, blockPos += BrrBlockSize) {
				// Encode BRR block, tell the encoder if we're at loop point (if loop is enabled), and if we're at end point
				bool isLoopPoint = n == loopTest;
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
					if (!disableFilter0) {
						MashAll(0);
					}

					if (!disableFilter1) {
						MashAll(1);
					}

					if (!disableFilter2) {
						MashAll(2);
					}

					if (!disableFilter3) {
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
				filterFunc = GetPredictionFilter(filter);

				ADPCMMash(bestRange);

				// Local functions
				// gets ugly here
				void MashAll(int filteri) {
					filterFunc = GetPredictionFilter(filteri);
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

					for (int i = 0; i < PcmBlockSize; i++) {
						int da;
						int thisSample = samples[n + i];
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
		};














	//	public static byte[] EncodeBlock(int samples, int offset, int filter, int )

	/// <summary>
	/// Decodes a given stream of raw BRR data into a Wave Sound audio file.
	/// </summary>
	/// <param name="brrSample">The BRR data to decode. This should not include any loop header.</param>
	/// <param name="loopBlock">The loop point of this sample in blocks, or -1 if the sample does not loop.</param>
	/// <param name="sampleRate">The output sample rate the audio file should be played back at.</param>
	/// <param name="minimumLength">The minimum length of looped audio in seconds. Takes priority over <paramref name="loopCount"/>. Ignored on non-looping samples.</param>
	/// <param name="loopCount">The number of times this file should be looped. Defers to <paramref name="minimumLength"/>. Ignored on non-looping samples.</param>
	/// <param name="applyGaussian">Allows application of a filter to the final audio to simulate the SNES Gaussian filtering.</param>
	/// <returns>A new <see cref="WaveContainer"/> object containing the decoded audio.</returns>
	/// <exception cref="BRRConversionException"></exception>
	public static WaveContainer Decode(byte[] brrSample, int loopBlock = NoLoop, int sampleRate = 32000,
		decimal minimumLength = 0.0M, int loopCount = 1, bool applyGaussian = false) {

		const int GaussA = 372;
		const int GaussB = 1304;
		const int GaussShift = 11;

		int blockCount = brrSample.Length / BrrBlockSize;

		if ((brrSample.Length % BrrBlockSize) is not 0) {
			throw new BRRConversionException($"Data size is not a multiple of {BrrBlockSize}: {brrSample.Length} | {blockCount * BrrBlockSize}");
		}

		if (loopBlock >= blockCount) {
			loopBlock = NoLoop;
		}

		if (loopBlock < 0) {
			loopCount = 0;
			loopBlock = blockCount;
		} else {
			int minSamples = (int) decimal.Ceiling(minimumLength * sampleRate / PcmBlockSize);
			loopCount = Math.Max(loopCount, (minSamples - loopBlock) / (blockCount - loopBlock));
			loopCount = Math.Clamp(loopCount, 1, 777);
		}

		int outBlocks = loopCount * (blockCount - loopBlock) + loopBlock;
		int sampleCount = outBlocks * PcmBlockSize;

		var retWav = new WaveContainer(sampleRate, PreferredBitsPerSample, sampleCount);

		int brrPos = 0;
		int wavPos = 0;
		int loopAt = loopBlock * BrrBlockSize;

		int p1 = 0;
		int p2 = 0;

		for (int i = 0; i < loopBlock; i++) {
			DecodeNextBlock();
		}

		for (; loopCount > 0; loopCount--) {
			brrPos = loopAt;
			for (int i = loopBlock; i < blockCount; i++) {
				DecodeNextBlock();
			}
		}

		if (applyGaussian) {
			int prev = GaussA * ((GaussB * retWav[0]) + retWav[1]);
			int ln = blockCount * PcmBlockSize - 1;
			for (int i = 1; i < ln; i++) {
				int temp = (GaussB * retWav[i]) + (GaussA * (retWav[i - 1] + retWav[i + 1]));
				retWav[i - 1] = (short) (prev >> GaussShift);
				prev = temp;
			}
			int last = GaussA * ((GaussB * retWav[^2]) + retWav[^1]);
			retWav[^2] = (short) (prev >> GaussShift);
			retWav[^1] = (short) (last >> GaussShift);
		}

		return retWav;

		void DecodeNextBlock() {
			var predictor = GetPredictionFilter((brrSample[brrPos] & FilterMask) >> FilterShift);

			int shift = (brrSample[brrPos] & RangeMask) >> RangeShift;

			brrPos++;

			for (int i = 0; i < 8; i++) {
				retWav[wavPos++] = (short) DecodeNextSample((byte) (brrSample[brrPos] >> 4));
				retWav[wavPos++] = (short) DecodeNextSample((byte) (brrSample[brrPos] & 0x0F));

				brrPos++;
			}

			int DecodeNextSample(byte samp) {
				int a = (shift, samp) switch {
					(< 13, < 8) => (samp << shift) >> 1,
					(< 13, _  ) => ((samp - 16) << shift) >> 1,
					(_   , < 8) => 2048,
					(_   , _  ) => -2048,
				};

				a += predictor(p1, p2);

				p2 = p1;

				int ret = p1 = (a switch {
					> short.MaxValue => short.MaxValue - 0x8000,
					< short.MinValue => short.MinValue + 0x8000,
					> 0x3FFF         => a - 0x8000,
					< -0x4000        => a + 0x8000,
					_                => a
				});

				return ret * 2;
			}
		}
	}


	/// <summary>
	/// Returns a new array of integers with the Gaussian interpolation table values.
	/// </summary>
	public static int[] GetGaussTable() =>
	[ // lifted directly from fullsnes.txt
		0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000, 0x000,
		0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x001, 0x002, 0x002, 0x002, 0x002, 0x002,
		0x002, 0x002, 0x003, 0x003, 0x003, 0x003, 0x003, 0x004, 0x004, 0x004, 0x004, 0x004, 0x005, 0x005, 0x005, 0x005,
		0x006, 0x006, 0x006, 0x006, 0x007, 0x007, 0x007, 0x008, 0x008, 0x008, 0x009, 0x009, 0x009, 0x00A, 0x00A, 0x00A,
		0x00B, 0x00B, 0x00B, 0x00C, 0x00C, 0x00D, 0x00D, 0x00E, 0x00E, 0x00F, 0x00F, 0x00F, 0x010, 0x010, 0x011, 0x011,
		0x012, 0x013, 0x013, 0x014, 0x014, 0x015, 0x015, 0x016, 0x017, 0x017, 0x018, 0x018, 0x019, 0x01A, 0x01B, 0x01B,
		0x01C, 0x01D, 0x01D, 0x01E, 0x01F, 0x020, 0x020, 0x021, 0x022, 0x023, 0x024, 0x024, 0x025, 0x026, 0x027, 0x028,
		0x029, 0x02A, 0x02B, 0x02C, 0x02D, 0x02E, 0x02F, 0x030, 0x031, 0x032, 0x033, 0x034, 0x035, 0x036, 0x037, 0x038,
		0x03A, 0x03B, 0x03C, 0x03D, 0x03E, 0x040, 0x041, 0x042, 0x043, 0x045, 0x046, 0x047, 0x049, 0x04A, 0x04C, 0x04D,
		0x04E, 0x050, 0x051, 0x053, 0x054, 0x056, 0x057, 0x059, 0x05A, 0x05C, 0x05E, 0x05F, 0x061, 0x063, 0x064, 0x066,
		0x068, 0x06A, 0x06B, 0x06D, 0x06F, 0x071, 0x073, 0x075, 0x076, 0x078, 0x07A, 0x07C, 0x07E, 0x080, 0x082, 0x084,
		0x086, 0x089, 0x08B, 0x08D, 0x08F, 0x091, 0x093, 0x096, 0x098, 0x09A, 0x09C, 0x09F, 0x0A1, 0x0A3, 0x0A6, 0x0A8,
		0x0AB, 0x0AD, 0x0AF, 0x0B2, 0x0B4, 0x0B7, 0x0BA, 0x0BC, 0x0BF, 0x0C1, 0x0C4, 0x0C7, 0x0C9, 0x0CC, 0x0CF, 0x0D2,
		0x0D4, 0x0D7, 0x0DA, 0x0DD, 0x0E0, 0x0E3, 0x0E6, 0x0E9, 0x0EC, 0x0EF, 0x0F2, 0x0F5, 0x0F8, 0x0FB, 0x0FE, 0x101,
		0x104, 0x107, 0x10B, 0x10E, 0x111, 0x114, 0x118, 0x11B, 0x11E, 0x122, 0x125, 0x129, 0x12C, 0x130, 0x133, 0x137,
		0x13A, 0x13E, 0x141, 0x145, 0x148, 0x14C, 0x150, 0x153, 0x157, 0x15B, 0x15F, 0x162, 0x166, 0x16A, 0x16E, 0x172,
		0x176, 0x17A, 0x17D, 0x181, 0x185, 0x189, 0x18D, 0x191, 0x195, 0x19A, 0x19E, 0x1A2, 0x1A6, 0x1AA, 0x1AE, 0x1B2,
		0x1B7, 0x1BB, 0x1BF, 0x1C3, 0x1C8, 0x1CC, 0x1D0, 0x1D5, 0x1D9, 0x1DD, 0x1E2, 0x1E6, 0x1EB, 0x1EF, 0x1F3, 0x1F8,
		0x1FC, 0x201, 0x205, 0x20A, 0x20F, 0x213, 0x218, 0x21C, 0x221, 0x226, 0x22A, 0x22F, 0x233, 0x238, 0x23D, 0x241,
		0x246, 0x24B, 0x250, 0x254, 0x259, 0x25E, 0x263, 0x267, 0x26C, 0x271, 0x276, 0x27B, 0x280, 0x284, 0x289, 0x28E,
		0x293, 0x298, 0x29D, 0x2A2, 0x2A6, 0x2AB, 0x2B0, 0x2B5, 0x2BA, 0x2BF, 0x2C4, 0x2C9, 0x2CE, 0x2D3, 0x2D8, 0x2DC,
		0x2E1, 0x2E6, 0x2EB, 0x2F0, 0x2F5, 0x2FA, 0x2FF, 0x304, 0x309, 0x30E, 0x313, 0x318, 0x31D, 0x322, 0x326, 0x32B,
		0x330, 0x335, 0x33A, 0x33F, 0x344, 0x349, 0x34E, 0x353, 0x357, 0x35C, 0x361, 0x366, 0x36B, 0x370, 0x374, 0x379,
		0x37E, 0x383, 0x388, 0x38C, 0x391, 0x396, 0x39B, 0x39F, 0x3A4, 0x3A9, 0x3AD, 0x3B2, 0x3B7, 0x3BB, 0x3C0, 0x3C5,
		0x3C9, 0x3CE, 0x3D2, 0x3D7, 0x3DC, 0x3E0, 0x3E5, 0x3E9, 0x3ED, 0x3F2, 0x3F6, 0x3FB, 0x3FF, 0x403, 0x408, 0x40C,
		0x410, 0x415, 0x419, 0x41D, 0x421, 0x425, 0x42A, 0x42E, 0x432, 0x436, 0x43A, 0x43E, 0x442, 0x446, 0x44A, 0x44E,
		0x452, 0x455, 0x459, 0x45D, 0x461, 0x465, 0x468, 0x46C, 0x470, 0x473, 0x477, 0x47A, 0x47E, 0x481, 0x485, 0x488,
		0x48C, 0x48F, 0x492, 0x496, 0x499, 0x49C, 0x49F, 0x4A2, 0x4A6, 0x4A9, 0x4AC, 0x4AF, 0x4B2, 0x4B5, 0x4B7, 0x4BA,
		0x4BD, 0x4C0, 0x4C3, 0x4C5, 0x4C8, 0x4CB, 0x4CD, 0x4D0, 0x4D2, 0x4D5, 0x4D7, 0x4D9, 0x4DC, 0x4DE, 0x4E0, 0x4E3,
		0x4E5, 0x4E7, 0x4E9, 0x4EB, 0x4ED, 0x4EF, 0x4F1, 0x4F3, 0x4F5, 0x4F6, 0x4F8, 0x4FA, 0x4FB, 0x4FD, 0x4FF, 0x500,
		0x502, 0x503, 0x504, 0x506, 0x507, 0x508, 0x50A, 0x50B, 0x50C, 0x50D, 0x50E, 0x50F, 0x510, 0x511, 0x511, 0x512,
		0x513, 0x514, 0x514, 0x515, 0x516, 0x516, 0x517, 0x517, 0x517, 0x518, 0x518, 0x518, 0x518, 0x518, 0x519, 0x519
	];

}
