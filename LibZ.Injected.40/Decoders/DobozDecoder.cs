#region Licenses

#region Doboz license

/*
 * Doboz Data Compression Library
 * Copyright (C) 2010-2011 Attila T. Afra <attila.afra@gmail.com>
 * 
 * This software is provided 'as-is', without any express or implied warranty. In no event will
 * the authors be held liable for any damages arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose, including commercial
 * applications, and to alter it and redistribute it freely, subject to the following restrictions:
 *
 * 1. The origin of this software must not be misrepresented; you must not claim that you wrote the
 *    original software. If you use this software in a product, an acknowledgment in the product
 *    documentation would be appreciated but is not required.
 * 2. Altered source versions must be plainly marked as such, and must not be misrepresented as
 *    being the original software.
 * 3. This notice may not be removed or altered from any source distribution.
 */

#endregion

#region Doboz for .NET license

/*
 * Copyright (c) 2013, Milosz Krajewski
 * 
 * Microsoft Public License (Ms-PL)
 * This license governs use of the accompanying software. 
 * If you use the software, you accept this license. 
 * If you do not accept the license, do not use the software.
 * 
 * 1. Definitions
 * The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same 
 * meaning here as under U.S. copyright law.
 * A "contribution" is the original software, or any additions or changes to the software.
 * A "contributor" is any person that distributes its contribution under this license.
 * "Licensed patents" are a contributor's patent claims that read directly on its contribution.
 * 
 * 2. Grant of Rights
 * (A) Copyright Grant- Subject to the terms of this license, including the license conditions 
 * and limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
 * royalty-free copyright license to reproduce its contribution, prepare derivative works of 
 * its contribution, and distribute its contribution or any derivative works that you create.
 * (B) Patent Grant- Subject to the terms of this license, including the license conditions and 
 * limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
 * royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, 
 * import, and/or otherwise dispose of its contribution in the software or derivative works of 
 * the contribution in the software.
 * 
 * 3. Conditions and Limitations
 * (A) No Trademark License- This license does not grant you rights to use any contributors' name, 
 * logo, or trademarks.
 * (B) If you bring a patent claim against any contributor over patents that you claim are infringed 
 * by the software, your patent license from such contributor to the software ends automatically.
 * (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, 
 * and attribution notices that are present in the software.
 * (D) If you distribute any portion of the software in source code form, you may do so only under this 
 * license by including a complete copy of this license with your distribution. If you distribute 
 * any portion of the software in compiled or object code form, you may only do so under a license 
 * that complies with this license.
 * (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express
 * warranties, guarantees or conditions. You may have additional consumer rights under your local 
 * laws which this license cannot change. To the extent permitted under your local laws, the 
 * contributors exclude the implied warranties of merchantability, fitness for a particular 
 * purpose and non-infringement.
 */

#endregion

#endregion

// ReSharper disable InconsistentNaming

#if !DOBOZ_UNSAFE
// by default it is always safe (it's... safer?) :-)
#define DOBOZ_SAFE
#endif

using System;
using System.Diagnostics;

namespace LibZ.Injected.Decoders
{
	/// <summary>
	/// Doboz decoder.
	/// </summary>
#if DOBOZ_SAFE
	internal class DobozDecoder
#else
	internal unsafe class DobozDecoder
#endif
	{
		#region struct LUTEntry

		// Use a decoding lookup table in order to avoid expensive branches
		private struct LUTEntry
		{
			public uint mask; // the mask for the entire encoded match
			public byte offsetShift;
			public byte lengthMask;
			public byte lengthShift;
			public sbyte size; // the size of the encoded match in bytes
		}

		#endregion

		#region enum Result

		internal enum Result
		{
			RESULT_OK,
			RESULT_ERROR_BUFFER_TOO_SMALL,
			RESULT_ERROR_CORRUPTED_DATA,
			RESULT_ERROR_UNSUPPORTED_VERSION,
		};

		#endregion

		#region struct Match

		internal struct Match
		{
			public int length;
			public int offset;
		};

		#endregion

		#region struct Header

		/// <summary>HEader structure.</summary>
		internal struct Header
		{
			public int uncompressedSize;
			public int compressedSize;
			public int version;
			public bool isStored;
		};

		#endregion

		#region struct CompressionInfo

		/// <summary>Compression info.</summary>
		internal struct CompressionInfo
		{
			public int uncompressedSize;
			public int compressedSize;
			public int version;
		};

		#endregion

		#region consts

		internal const int VERSION = 0; // encoding format

		internal const int WORD_SIZE = 4; // uint32_t
		internal const int MIN_MATCH_LENGTH = 3;
		internal const int TAIL_LENGTH = 2 * WORD_SIZE; // prevents fast write operations from writing beyond the end of the buffer during decoding

		private static readonly sbyte[] LITERAL_RUN_LENGTH_TABLE
			= new sbyte[] { 4, 0, 1, 0, 2, 0, 1, 0, 3, 0, 1, 0, 2, 0, 1, 0 };

		private static readonly LUTEntry[] LUT = {
			new LUTEntry {mask = 0xff, offsetShift = 2, lengthMask = 0, lengthShift = 0, size = 1}, // (0)00
			new LUTEntry {mask = 0xffff, offsetShift = 2, lengthMask = 0, lengthShift = 0, size = 2}, // (0)01
			new LUTEntry {mask = 0xffff, offsetShift = 6, lengthMask = 15, lengthShift = 2, size = 2}, // (0)10
			new LUTEntry {mask = 0xffffff, offsetShift = 8, lengthMask = 31, lengthShift = 3, size = 3}, // (0)11
			new LUTEntry {mask = 0xff, offsetShift = 2, lengthMask = 0, lengthShift = 0, size = 1}, // (1)00 = (0)00
			new LUTEntry {mask = 0xffff, offsetShift = 2, lengthMask = 0, lengthShift = 0, size = 2}, // (1)01 = (0)01
			new LUTEntry {mask = 0xffff, offsetShift = 6, lengthMask = 15, lengthShift = 2, size = 2}, // (1)10 = (0)10
			new LUTEntry {mask = 0xffffffff, offsetShift = 11, lengthMask = 255, lengthShift = 3, size = 4} // 111
		};

#if DOBOZ_SAFE
		/// <summary>Buffer length when Buffer.BlockCopy becomes faster than straight loop.</summary>
		private const int BLOCK_COPY_LIMIT = 16;
#endif

		#endregion

		#region public interface

		/// <summary>Gets the maximum length of the output.</summary>
		/// <param name="size">The uncompressed length.</param>
		/// <returns>Maximum compressed length.</returns>
		public static int _MaximumOutputLength(int size)
		{
			// The header + the original uncompressed data
			return GetHeaderSize(size) + size;
		}

		/// <summary>Gets the uncompressed length.</summary>
		/// <param name="input">The buffer.</param>
		/// <param name="inputOffset">The buffer offset.</param>
		/// <param name="inputLength">Length of the buffer.</param>
		/// <returns>Length of uncompressed data.</returns>
		public static int _UncompressedLength(byte[] input, int inputOffset, int inputLength)
		{
			CheckArguments(
				input, inputOffset, ref inputLength);

#if DOBOZ_SAFE
			var src = input;
#else
			fixed (byte* src = input)
#endif
			{
				var info = new CompressionInfo();
				if (GetCompressionInfo(src, inputOffset, inputLength, ref info) != Result.RESULT_OK)
					throw new ArgumentException("Corrupted input data");

				return info.uncompressedSize;
			}
		}

		/// <summary>Decodes the specified input.</summary>
		/// <param name="input">The input.</param>
		/// <param name="inputOffset">The input offset.</param>
		/// <param name="inputLength">Length of the input.</param>
		/// <returns>Decoded buffer.</returns>
		public static byte[] Decode(
			byte[] input, int inputOffset, int inputLength)
		{
			CheckArguments(
				input, inputOffset, ref inputLength);

#if DOBOZ_SAFE
			var src = input;
#else
			fixed (byte* src = input)
#endif
			{
				var info = new CompressionInfo();
				if (GetCompressionInfo(src, inputOffset, inputLength, ref info) != Result.RESULT_OK)
					throw new ArgumentException("Corrupted input data");

				var outputLength = info.uncompressedSize;
				var output = new byte[outputLength];

#if DOBOZ_SAFE
				var dst = output;
#else
				fixed (byte* dst = output)
#endif
				{
					if (Decompress(src, inputOffset, inputLength, dst, 0, outputLength) != Result.RESULT_OK)
						throw new ArgumentException("Corrupted data");

					return output;
				}
			}
		}

		/// <summary>Decodes the specified input.</summary>
		/// <param name="input">The input.</param>
		/// <param name="inputOffset">The input offset.</param>
		/// <param name="inputLength">Length of the input.</param>
		/// <param name="output">The output.</param>
		/// <param name="outputOffset">The output offset.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <returns>Number of decoded bytes.</returns>
		public static int Decode(
			byte[] input, int inputOffset, int inputLength,
			byte[] output, int outputOffset, int outputLength)
		{
			CheckArguments(
				input, inputOffset, ref inputLength,
				output, outputOffset, ref outputLength);

#if DOBOZ_SAFE
			var src = input;
#else
			fixed (byte* src = input)
#endif
			{
				var info = new CompressionInfo();
				if (GetCompressionInfo(src, inputOffset, inputLength, ref info) != Result.RESULT_OK)
					throw new ArgumentException("Corrupted input data");

				if (outputLength < info.uncompressedSize)
					throw new ArgumentException("Output buffer is too small");

				outputLength = info.uncompressedSize;

#if DOBOZ_SAFE
				var dst = output;
#else
				fixed (byte* dst = output)
#endif
				{
					if (Decompress(src, inputOffset, inputLength, dst, outputOffset, outputLength) != Result.RESULT_OK)
						throw new ArgumentException("Corrupted data");
					return outputLength;
				}
			}
		}

		#endregion

		#region protected interface

		/// <summary>Gets the size of size field (1, 2 or 4 bytes).</summary>
		/// <param name="size">The size.</param>
		/// <returns>Number of bytes needed to store size.</returns>
		protected static int GetSizeCodedSize(int size)
		{
			return
				size <= byte.MaxValue ? sizeof(byte) :
					size <= ushort.MaxValue ? sizeof(ushort) :
						sizeof(uint);
		}

		/// <summary>Gets the size of the header.</summary>
		/// <param name="size">The size.</param>
		/// <returns>Size of header.</returns>
		protected static int GetHeaderSize(int size)
		{
			return 1 + 2 * GetSizeCodedSize(size);
		}

		/// <summary>Checks the arguments.</summary>
		/// <param name="input">The input.</param>
		/// <param name="inputOffset">The input offset.</param>
		/// <param name="inputLength">Length of the input.</param>
		/// <exception cref="System.ArgumentNullException">input</exception>
		/// <exception cref="System.ArgumentException">inputOffset and inputLength are invalid for given input</exception>
		protected static void CheckArguments(
			byte[] input, int inputOffset, ref int inputLength)
		{
			if (input == null)
				throw new ArgumentNullException("input");
			if (inputLength < 0)
				inputLength = input.Length - inputOffset;
			if (inputOffset < 0 || inputOffset + inputLength > input.Length)
				throw new ArgumentException("inputOffset and inputLength are invalid for given input");
		}

		/// <summary>Checks the arguments.</summary>
		/// <param name="input">The input.</param>
		/// <param name="inputOffset">The input offset.</param>
		/// <param name="inputLength">Length of the input.</param>
		/// <param name="output">The output.</param>
		/// <param name="outputOffset">The output offset.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <exception cref="System.ArgumentNullException">input or output</exception>
		/// <exception cref="System.ArgumentException">
		/// inputOffset and inputLength are invalid for given input or outputOffset and outputLength are invalid for given output
		/// </exception>
		protected static void CheckArguments(
			byte[] input, int inputOffset, ref int inputLength,
			byte[] output, int outputOffset, ref int outputLength)
		{
			if (input == null)
				throw new ArgumentNullException("input");
			if (output == null)
				throw new ArgumentNullException("output");

			if (inputLength < 0)
				inputLength = input.Length - inputOffset;
			if (inputOffset < 0 || inputOffset + inputLength > input.Length)
				throw new ArgumentException("inputOffset and inputLength are invalid for given input");

			if (outputLength < 0)
				outputLength = output.Length - outputOffset;
			if (outputOffset < 0 || outputOffset + outputLength > output.Length)
				throw new ArgumentException("outputOffset and outputLength are invalid for given output");
		}

#if DOBOZ_SAFE
		// ReSharper disable RedundantCast

		/// <summary>Peeks ushort from specified buffer.</summary>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The offset.</param>
		/// <returns>ushort value.</returns>
		protected static ushort Peek2(byte[] buffer, int offset)
		{
			// NOTE: It's faster than BitConverter.ToUInt16 (suprised? me too)
			return (ushort)(
				((uint)buffer[offset]) | ((uint)buffer[offset + 1] << 8));
		}

		/// <summary>Peeks uint from specified buffer.</summary>
		/// <param name="buffer">The buffer.</param>
		/// <param name="offset">The offset.</param>
		/// <returns>uint value.</returns>
		protected static uint Peek4(byte[] buffer, int offset)
		{
			// NOTE: It's faster than BitConverter.ToUInt32 (suprised? me too)
			return
				((uint)buffer[offset]) |
				((uint)buffer[offset + 1] << 8) |
				((uint)buffer[offset + 2] << 16) |
				((uint)buffer[offset + 3] << 24);
		}

		// ReSharper restore RedundantCast

		/// <summary>Copies block of memory in safe mode.</summary>
		/// <param name="src">The source array.</param>
		/// <param name="src_0">The source starting offset.</param>
		/// <param name="dst">The destination array.</param>
		/// <param name="dst_0">The destination starting offset.</param>
		/// <param name="len">The length.</param>
		protected static void BlockCopy(byte[] src, int src_0, byte[] dst, int dst_0, int len)
		{
			Debug.Assert(
				src != dst || src_0 + len <= dst_0 || dst_0 + len <= src_0,
				"BlockCopy does not handle overlapping buffers");

			if (len >= BLOCK_COPY_LIMIT)
			{
				Buffer.BlockCopy(src, src_0, dst, dst_0, len);
			}
			else
			{
				while (len >= 8)
				{
					dst[dst_0] = src[src_0];
					dst[dst_0 + 1] = src[src_0 + 1];
					dst[dst_0 + 2] = src[src_0 + 2];
					dst[dst_0 + 3] = src[src_0 + 3];
					dst[dst_0 + 4] = src[src_0 + 4];
					dst[dst_0 + 5] = src[src_0 + 5];
					dst[dst_0 + 6] = src[src_0 + 6];
					dst[dst_0 + 7] = src[src_0 + 7];
					len -= 8;
					src_0 += 8;
					dst_0 += 8;
				}

				while (len >= 4)
				{
					dst[dst_0] = src[src_0];
					dst[dst_0 + 1] = src[src_0 + 1];
					dst[dst_0 + 2] = src[src_0 + 2];
					dst[dst_0 + 3] = src[src_0 + 3];
					len -= 4;
					src_0 += 4;
					dst_0 += 4;
				}

				while (len-- > 0)
				{
					dst[dst_0++] = src[src_0++];
				}
			}
		}
#else
	/// <summary>Copies block of memory.</summary>
	/// <param name="src">The source.</param>
	/// <param name="dst">The destination.</param>
	/// <param name="len">The length (in bytes).</param>
		internal static void BlockCopy(byte* src, byte* dst, int len)
		{
			while (len >= 8)
			{
				*(ulong*)dst = *(ulong*)src;
				dst += 8;
				src += 8;
				len -= 8;
			}
			if (len >= 4)
			{
				*(uint*)dst = *(uint*)src;
				dst += 4;
				src += 4;
				len -= 4;
			}
			if (len >= 2)
			{
				*(ushort*)dst = *(ushort*)src;
				dst += 2;
				src += 2;
				len -= 2;
			}
			if (len >= 1)
			{
				*dst = *src; /* d++; s++; l--; */
			}
		}
#endif

		#endregion

		#region private implementation

#if DOBOZ_SAFE
		private static Result GetCompressionInfo(byte[] source, int sourceOffset, int sourceSize, ref CompressionInfo compressionInfo)
#else
		private static Result GetCompressionInfo(byte* source, int sourceOffset, int sourceSize, ref CompressionInfo compressionInfo)
#endif
		{
			Debug.Assert(source != null);

			// Decode the header
			var header = new Header();
			var headerSize = 0;
			var decodeHeaderResult = DecodeHeader(ref header, source, sourceOffset, sourceSize, ref headerSize);

			if (decodeHeaderResult != Result.RESULT_OK)
			{
				return decodeHeaderResult;
			}

			// Return the requested info
			compressionInfo.uncompressedSize = header.uncompressedSize;
			compressionInfo.compressedSize = header.compressedSize;
			compressionInfo.version = header.version;

			return Result.RESULT_OK;
		}

		// Decodes a header and returns its size in bytes
		// If the header is not valid, the function returns 0
#if DOBOZ_SAFE
		private static Result DecodeHeader(ref Header header, byte[] source, int sourceOffset, int sourceSize, ref int headerSize)
		{
			var src_p = sourceOffset;
#else
		private static Result DecodeHeader(ref Header header, byte* source, int sourceOffset, int sourceSize, ref int headerSize)
		{
			var src_p = source + sourceOffset;
#endif

			// Decode the attribute bytes
			if (sourceSize < 1)
			{
				return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
			}

#if DOBOZ_SAFE
			uint attributes = source[src_p++];
#else
			uint attributes = *src_p++;
#endif

			header.version = (int)(attributes & 7);
			var sizeCodedSize = (int)(((attributes >> 3) & 7) + 1);

			// Compute the size of the header
			headerSize = 1 + 2 * sizeCodedSize;

			if (sourceSize < headerSize)
			{
				return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
			}

			header.isStored = (attributes & 128) != 0;

			// Decode the uncompressed and compressed sizes
			switch (sizeCodedSize)
			{
#if DOBOZ_SAFE
				case 1:
					header.uncompressedSize = source[src_p];
					header.compressedSize = source[src_p + sizeCodedSize];
					break;

				case 2:
					header.uncompressedSize = Peek2(source, src_p);
					header.compressedSize = Peek2(source, src_p + sizeCodedSize);
					break;

				case 4:
					header.uncompressedSize = (int)Peek4(source, src_p);
					header.compressedSize = (int)Peek4(source, src_p + sizeCodedSize);
					break;
#else
				case 1:
					header.uncompressedSize = *src_p;
					header.compressedSize = *(src_p + sizeCodedSize);
					break;

				case 2:
					header.uncompressedSize = *((ushort*)(src_p));
					header.compressedSize = *((ushort*)(src_p + sizeCodedSize));
					break;

				case 4:
					header.uncompressedSize = *((int*)(src_p));
					header.compressedSize = *((int*)(src_p + sizeCodedSize));
					break;
#endif

				default:
					return Result.RESULT_ERROR_CORRUPTED_DATA;
			}

			return Result.RESULT_OK;
		}

#if DOBOZ_SAFE
		private static Result Decompress(byte[] source, int sourceOffset, int sourceSize, byte[] destination, int destrinationOffset, int destinationSize)
#else
		private static Result Decompress(byte* source, int sourceOffset, int sourceSize, byte* destination, int destinationOffset, int destinationSize)
#endif
		{
			Debug.Assert(source != null);
			Debug.Assert(destination != null);

#if DOBOZ_SAFE
			var lut = LUT;
			{
				var src_0 = sourceOffset;
				var dst_0 = destrinationOffset;

				Debug.Assert(
					(source != destination) ||
						(src_0 + sourceSize <= dst_0 || src_0 >= dst_0 + destinationSize),
					"The source and destination buffers must not overlap.");

#else
			fixed (LUTEntry* lut = LUT)
			{
				var src_0 = source + sourceOffset;
				var dst_0 = destination + destinationOffset;

				Debug.Assert(
					(src_0 + sourceSize <= dst_0 || src_0 >= dst_0 + destinationSize),
					"The source and destination buffers must not overlap.");
#endif

				var src_p = src_0;
				var dst_p = dst_0;

				// Decode the header
				var header = new Header();
				var headerSize = 0;
				var decodeHeaderResult = DecodeHeader(ref header, source, sourceOffset, sourceSize, ref headerSize);

				if (decodeHeaderResult != Result.RESULT_OK)
				{
					return decodeHeaderResult;
				}

				src_p += headerSize;

				if (header.version != VERSION)
				{
					return Result.RESULT_ERROR_UNSUPPORTED_VERSION;
				}

				// Check whether the supplied buffers are large enough
				if (sourceSize < header.compressedSize || destinationSize < header.uncompressedSize)
				{
					return Result.RESULT_ERROR_BUFFER_TOO_SMALL;
				}

				var uncompressedSize = header.uncompressedSize;

				// If the data is simply stored, copy it to the destination buffer and we're done
				if (header.isStored)
				{
#if DOBOZ_SAFE
					BlockCopy(source, src_p, destination, dst_0, uncompressedSize);
#else
					BlockCopy(src_p, dst_0, uncompressedSize);
#endif
					return Result.RESULT_OK;
				}

				var src_end = src_0 + header.compressedSize;
				var dst_end = dst_0 + uncompressedSize;

				// Compute pointer to the first byte of the output 'tail'
				// Fast write operations can be used only before the tail, because those may write beyond the end of the output buffer
				var outputTail = (uncompressedSize > TAIL_LENGTH) ? (dst_end - TAIL_LENGTH) : dst_0;

				// Initialize the control word to 'empty'
				uint controlWord = 1;

				// Decoding loop
				while (true)
				{
					// Check whether there is enough data left in the input buffer
					// In order to decode the next literal/match, we have to read up to 8 bytes (2 words)
					// Thanks to the trailing dummy, there must be at least 8 remaining input bytes
					if (src_p + 2 * WORD_SIZE > src_end)
					{
						return Result.RESULT_ERROR_CORRUPTED_DATA;
					}

					// Check whether we must read a control word
					if (controlWord == 1)
					{
						Debug.Assert(src_p + WORD_SIZE <= src_end);
#if DOBOZ_SAFE
						controlWord = Peek4(source, src_p);
#else
						controlWord = *(uint*)src_p;
#endif
						src_p += WORD_SIZE;
					}

					// Detect whether it's a literal or a match
					if ((controlWord & 1) == 0)
					{
						// It's a literal

						// If we are before the tail, we can safely use fast writing operations
						if (dst_p < outputTail)
						{
							// We copy literals in runs of up to 4 because it's faster than copying one by one

							// Copy implicitly 4 literals regardless of the run length
							Debug.Assert(src_p + WORD_SIZE <= src_end);
							Debug.Assert(dst_p + WORD_SIZE <= dst_end);
#if DOBOZ_SAFE
							// Copy4(source, src_p, destination, dst_p);
							destination[dst_p + 0] = source[src_p + 0];
							destination[dst_p + 1] = source[src_p + 1];
							destination[dst_p + 2] = source[src_p + 2];
							destination[dst_p + 3] = source[src_p + 3];
#else
							*(uint*)dst_p = *(uint*)src_p;
#endif

							// Get the run length using a lookup table
							int runLength = LITERAL_RUN_LENGTH_TABLE[controlWord & 0xf];

							// Advance the src and dst pointers with the run length
							src_p += runLength;
							dst_p += runLength;

							// Consume as much control word bits as the run length
							controlWord >>= runLength;
						}
						else
						{
							// We have reached the tail, we cannot output literals in runs anymore
							// Output all remaining literals
							while (dst_p < dst_end)
							{
								// Check whether there is enough data left in the input buffer
								// In order to decode the next literal, we have to read up to 5 bytes
								if (src_p + WORD_SIZE + 1 > src_end)
								{
									return Result.RESULT_ERROR_CORRUPTED_DATA;
								}

								// Check whether we must read a control word
								if (controlWord == 1)
								{
									Debug.Assert(src_p + WORD_SIZE <= src_end);
#if DOBOZ_SAFE
									controlWord = Peek4(source, src_p);
#else
									controlWord = *(uint*)src_p;
#endif
									src_p += WORD_SIZE;
								}

								// Output one literal
								// We cannot use fast read/write functions
								Debug.Assert(src_p + 1 <= src_end);
								Debug.Assert(dst_p + 1 <= dst_end);
#if DOBOZ_SAFE
								destination[dst_p++] = source[src_p++];
#else
								*dst_p++ = *src_p++;
#endif

								// Next control word bit
								controlWord >>= 1;
							}

							// Done
							return Result.RESULT_OK;
						}
					}
					else
					{
						// It's a match

						// Decode the match
						Debug.Assert(src_p + WORD_SIZE <= src_end);
						Match match;

						// src_p += decodeMatch(ref match, src_p);
						{
							// Read the maximum number of bytes a match is coded in (4)
#if DOBOZ_SAFE
							var w = Peek4(source, src_p);
#else
							var w = *(uint*)src_p;
#endif

							// Compute the decoding lookup table entry index: the lowest 3 bits of the encoded match
							var u = w & 7;

							// Compute the match offset and length using the lookup table entry
							match.offset = (int)((w & lut[u].mask) >> lut[u].offsetShift);
							match.length = (int)(((w >> lut[u].lengthShift) & lut[u].lengthMask) + MIN_MATCH_LENGTH);

							src_p += lut[u].size;
						}

						// Copy the matched string
						// In order to achieve high performance, we copy characters in groups of machine words
						// Overlapping matches require special care
						var matchString = dst_p - match.offset;

						// Check whether the match is out of range
						if (matchString < dst_0 || dst_p + match.length > outputTail)
						{
							return Result.RESULT_ERROR_CORRUPTED_DATA;
						}

						var i = 0;

						if (match.offset < WORD_SIZE)
						{
							// The match offset is less than the word size
							// In order to correctly handle the overlap, we have to copy the first three bytes one by one
							do
							{
								Debug.Assert(matchString + i >= dst_0);
								Debug.Assert(matchString + i + WORD_SIZE <= dst_end);
								Debug.Assert(dst_p + i + WORD_SIZE <= dst_end);
#if DOBOZ_SAFE
								destination[dst_p + i] = destination[matchString + i];
#else
								*(dst_p + i) = *(matchString + i);
#endif
								++i;
							} while (i < 3);

							// With this trick, we increase the distance between the source and destination pointers
							// This enables us to use fast copying for the rest of the match
							matchString -= 2 + (match.offset & 1);
						}

						// Fast copying
						// There must be no overlap between the source and destination words
						do
						{
							Debug.Assert(matchString + i >= dst_0);
							Debug.Assert(matchString + i + WORD_SIZE <= dst_end);
							Debug.Assert(dst_p + i + WORD_SIZE <= dst_end);
#if DOBOZ_SAFE
							// Copy4(destination, matchString + i, dst_p + i);
							destination[dst_p + i + 0] = destination[matchString + i + 0];
							destination[dst_p + i + 1] = destination[matchString + i + 1];
							destination[dst_p + i + 2] = destination[matchString + i + 2];
							destination[dst_p + i + 3] = destination[matchString + i + 3];
#else
							*(uint*)(dst_p + i) = *(uint*)(matchString + i);
#endif
							i += WORD_SIZE;
						} while (i < match.length);

						dst_p += match.length;

						// Next control word bit
						controlWord >>= 1;
					}
				}
			}
		}

		#endregion
	}
}

// ReSharper restore InconsistentNaming
