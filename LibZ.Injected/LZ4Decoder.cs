using System;
using System.Diagnostics;

// ReSharper disable InconsistentNaming

namespace LibZ.Injected
{
	class LZ4Decoder
	{
		#region consts

		/// <summary>Buffer length when Buffer.BlockCopy becomes faster than straight loop.
		/// Please note that safe implementation REQUIRES it to be greater (not even equal) than 8.</summary>
		private const int BLOCK_COPY_LIMIT = 16;

		private const int COPYLENGTH = 8;
		private const int LASTLITERALS = 5;
		private const int ML_BITS = 4;
		private const int ML_MASK = (1 << ML_BITS) - 1;
		private const int RUN_BITS = 8 - ML_BITS;
		private const int RUN_MASK = (1 << RUN_BITS) - 1;
		private const int STEPSIZE_64 = 8;

		private static readonly int[] DECODER_TABLE_32 = new[] { 0, 3, 2, 3, 0, 0, 0, 0 };
		private static readonly int[] DECODER_TABLE_64 = new[] { 0, 0, 0, -1, 0, 1, 2, 3 };

		#endregion

		#region Byte manipulation

		// ReSharper disable RedundantCast

		internal static void Poke2(byte[] buffer, int offset, ushort value)
		{
			buffer[offset] = (byte)value;
			buffer[offset + 1] = (byte)(value >> 8);
		}

		internal static ushort Peek2(byte[] buffer, int offset)
		{
			// NOTE: It's faster than BitConverter.ToUInt16 (suprised? me too)
			return (ushort)(((uint)buffer[offset]) | ((uint)buffer[offset + 1] << 8));
		}

		internal static uint Peek4(byte[] buffer, int offset)
		{
			// NOTE: It's faster than BitConverter.ToUInt32 (suprised? me too)
			return
				((uint)buffer[offset]) |
					((uint)buffer[offset + 1] << 8) |
					((uint)buffer[offset + 2] << 16) |
					((uint)buffer[offset + 3] << 24);
		}

		private static void Copy4(byte[] buf, int src, int dst)
		{
			Assert(dst > src, "Copying backwards is not implemented");
			buf[dst + 3] = buf[src + 3];
			buf[dst + 2] = buf[src + 2];
			buf[dst + 1] = buf[src + 1];
			buf[dst] = buf[src];
		}

		private static void Copy8(byte[] buf, int src, int dst)
		{
			Assert(dst > src, "Copying backwards is not implemented");
			buf[dst + 7] = buf[src + 7];
			buf[dst + 6] = buf[src + 6];
			buf[dst + 5] = buf[src + 5];
			buf[dst + 4] = buf[src + 4];
			buf[dst + 3] = buf[src + 3];
			buf[dst + 2] = buf[src + 2];
			buf[dst + 1] = buf[src + 1];
			buf[dst] = buf[src];
		}

		// ReSharper restore RedundantCast

		private static void BlockCopy(byte[] src, int src_0, byte[] dst, int dst_0, int len)
		{
			Assert(src != dst, "BlockCopy does not handle copying to the same buffer");

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

		private static int WildCopy(byte[] src, int src_0, byte[] dst, int dst_0, int dst_end)
		{
			var len = dst_end - dst_0;

			Assert(src != dst, "BlockCopy does not handle copying to the same buffer");
			Assert(len > 0, "Length have to be greater than 0");

			if (len >= BLOCK_COPY_LIMIT)
			{
				Buffer.BlockCopy(src, src_0, dst, dst_0, len);
			}
			else
			{
				// apparently (tested) this is an overkill
				// it seems to be faster without this 8-byte loop
				//while (len >= 8)
				//{
				//	dst[dst_0] = src[src_0];
				//	dst[dst_0 + 1] = src[src_0 + 1];
				//	dst[dst_0 + 2] = src[src_0 + 2];
				//	dst[dst_0 + 3] = src[src_0 + 3];
				//	dst[dst_0 + 4] = src[src_0 + 4];
				//	dst[dst_0 + 5] = src[src_0 + 5];
				//	dst[dst_0 + 6] = src[src_0 + 6];
				//	dst[dst_0 + 7] = src[src_0 + 7];
				//	len -= 8; src_0 += 8; dst_0 += 8;
				//}

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

			return len;
		}

		private static int SecureCopy(byte[] buffer, int src, int dst, int dst_end)
		{
			var diff = dst - src;
			var length = dst_end - dst;
			var len = length;

			Assert(diff >= 4, "Target must be at least 4 bytes further than source");
			Assert(BLOCK_COPY_LIMIT > 4, "This method requires BLOCK_COPY_LIMIT > 4");
			Assert(len > 0, "Length have to be greater than 0");

			if (diff >= BLOCK_COPY_LIMIT)
			{
				if (diff >= length)
				{
					Buffer.BlockCopy(buffer, src, buffer, dst, length);
					return length; // done
				}

				do
				{
					Buffer.BlockCopy(buffer, src, buffer, dst, diff);
					src += diff;
					dst += diff;
					len -= diff;
				} while (len >= diff);
			}

			// apparently (tested) this is an overkill
			// it seems to be faster without this 8-byte loop
			//while (len >= 8)
			//{
			//	buffer[dst] = buffer[src];
			//	buffer[dst + 1] = buffer[src + 1];
			//	buffer[dst + 2] = buffer[src + 2];
			//	buffer[dst + 3] = buffer[src + 3];
			//	buffer[dst + 4] = buffer[src + 4];
			//	buffer[dst + 5] = buffer[src + 5];
			//	buffer[dst + 6] = buffer[src + 6];
			//	buffer[dst + 7] = buffer[src + 7];
			//	dst += 8; src += 8; len -= 8;
			//}

			while (len >= 4)
			{
				buffer[dst] = buffer[src];
				buffer[dst + 1] = buffer[src + 1];
				buffer[dst + 2] = buffer[src + 2];
				buffer[dst + 3] = buffer[src + 3];
				dst += 4;
				src += 4;
				len -= 4;
			}

			while (len-- > 0)
			{
				buffer[dst++] = buffer[src++];
			}

			return length; // done
		}

		#endregion

		#region utilities

		// ReSharper disable UnusedParameter.Local

		[Conditional("DEBUG")]
		private static void Assert(bool condition, string errorMessage)
		{
			if (!condition) throw new ArgumentException(errorMessage);
			Debug.Assert(condition, errorMessage);
		}

		// ReSharper restore UnusedParameter.Local

		internal static void CheckArguments(
			byte[] input, int inputOffset, ref int inputLength,
			byte[] output, int outputOffset, ref int outputLength)
		{
			if (inputLength < 0) inputLength = input.Length - inputOffset;
			if (inputLength == 0)
			{
				outputLength = 0;
				return;
			}

			if (input == null) throw new ArgumentNullException("input");
			if (inputOffset < 0 || inputOffset + inputLength > input.Length)
				throw new ArgumentException("inputOffset and inputLength are invalid for given input");

			if (outputLength < 0) outputLength = output.Length - outputOffset;
			if (output == null) throw new ArgumentNullException("output");
			if (outputOffset < 0 || outputOffset + outputLength > output.Length)
				throw new ArgumentException("outputOffset and outputLength are invalid for given output");
		}

		#endregion

		#region uncompress

		// ReSharper disable TooWideLocalVariableScope

		private static int LZ4_uncompress_safe64(
			byte[] src,
			byte[] dst,
			int src_0,
			int dst_0,
			int dst_len)
		{
			var dec32table = DECODER_TABLE_32;
			var dec64table = DECODER_TABLE_64;
			int _i;

			// ---- preprocessed source start here ----
			// r93
			var src_p = src_0;
			int dst_ref;

			var dst_p = dst_0;
			var dst_end = dst_p + dst_len;
			int dst_cpy;

			var dst_LASTLITERALS = dst_end - LASTLITERALS;
			var dst_COPYLENGTH = dst_end - COPYLENGTH;
			var dst_COPYLENGTH_STEPSIZE_4 = dst_end - COPYLENGTH - (STEPSIZE_64 - 4);

			uint token;

			// Main Loop
			while (true)
			{
				int length;

				// get runlength
				token = src[src_p++];
				if ((length = (byte)(token >> ML_BITS)) == RUN_MASK)
				{
					int len;
					for (; (len = src[src_p++]) == 255; length += 255)
					{
						/* do nothing */
					}
					length += len;
				}

				// copy literals
				dst_cpy = dst_p + length;

				if (dst_cpy > dst_COPYLENGTH)
				{
					if (dst_cpy != dst_end) goto _output_error; // Error : not enough place for another match (min 4) + 5 literals
					BlockCopy(src, src_p, dst, dst_p, length);
					src_p += length;
					break; // EOF
				}
				if (dst_p < dst_cpy) /*?*/
				{
					_i = WildCopy(src, src_p, dst, dst_p, dst_cpy);
					src_p += _i;
					dst_p += _i;
				}
				src_p -= (dst_p - dst_cpy);
				dst_p = dst_cpy;

				// get offset
				dst_ref = (dst_cpy) - Peek2(src, src_p);
				src_p += 2;
				if (dst_ref < dst_0) goto _output_error; // Error : offset outside destination buffer

				// get matchlength
				if ((length = (byte)(token & ML_MASK)) == ML_MASK)
				{
					for (; src[src_p] == 255; length += 255) src_p++;
					length += src[src_p++];
				}

				// copy repeated sequence
				if ((dst_p - dst_ref) < STEPSIZE_64)
				{
					var dec64 = dec64table[dst_p - dst_ref];

					dst[dst_p + 0] = dst[dst_ref + 0];
					dst[dst_p + 1] = dst[dst_ref + 1];
					dst[dst_p + 2] = dst[dst_ref + 2];
					dst[dst_p + 3] = dst[dst_ref + 3];
					dst_p += 4;
					dst_ref += 4;
					dst_ref -= dec32table[dst_p - dst_ref];
					Copy4(dst, dst_ref, dst_p);
					dst_p += STEPSIZE_64 - 4;
					dst_ref -= dec64;
				}
				else
				{
					Copy8(dst, dst_ref, dst_p);
					dst_p += 8;
					dst_ref += 8;
				}
				dst_cpy = dst_p + length - (STEPSIZE_64 - 4);

				if (dst_cpy > dst_COPYLENGTH_STEPSIZE_4)
				{
					if (dst_cpy > dst_LASTLITERALS) goto _output_error; // Error : last 5 bytes must be literals
					if (dst_p < dst_COPYLENGTH)
					{
						_i = SecureCopy(dst, dst_ref, dst_p, dst_COPYLENGTH);
						dst_ref += _i;
						dst_p += _i;
					}

					while (dst_p < dst_cpy) dst[dst_p++] = dst[dst_ref++];
					dst_p = dst_cpy;
					continue;
				}

				if (dst_p < dst_cpy)
				{
					SecureCopy(dst, dst_ref, dst_p, dst_cpy);
				}
				dst_p = dst_cpy; // correction
			}

			// end of decoding
			return ((src_p) - src_0);

		_output_error:
			// write overflow error detected
			return (-((src_p) - src_0));
		}

		// ReSharper restore TooWideLocalVariableScope

		#endregion

		#region Decode64

		/// <summary>Decodes the specified input.</summary>
		/// <param name="input">The input.</param>
		/// <param name="inputOffset">The input offset.</param>
		/// <param name="inputLength">Length of the input.</param>
		/// <param name="output">The output.</param>
		/// <param name="outputOffset">The output offset.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <returns>Number of bytes written.</returns>
		public static int Decode64(
			byte[] input,
			int inputOffset,
			int inputLength,
			byte[] output,
			int outputOffset,
			int outputLength)
		{
			CheckArguments(
				input, inputOffset, ref inputLength,
				output, outputOffset, ref outputLength);

			if (outputLength == 0) return 0;

			var length = LZ4_uncompress_safe64(input, output, inputOffset, outputOffset, outputLength);
			if (length != inputLength)
				throw new ArgumentException("LZ4 block is corrupted, or invalid length has been given.");
			return outputLength;
		}

		/// <summary>Decodes the specified input.</summary>
		/// <param name="input">The input.</param>
		/// <param name="inputOffset">The input offset.</param>
		/// <param name="inputLength">Length of the input.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <returns>Decompressed buffer.</returns>
		public static byte[] Decode64(byte[] input, int inputOffset, int inputLength, int outputLength)
		{
			if (inputLength < 0) inputLength = input.Length - inputOffset;

			if (input == null) throw new ArgumentNullException("input");
			if (inputOffset < 0 || inputOffset + inputLength > input.Length)
				throw new ArgumentException("inputOffset and inputLength are invalid for given input");

			var result = new byte[outputLength];
			var length = Decode64(input, inputOffset, inputLength, result, 0, outputLength);
			if (length != outputLength)
				throw new ArgumentException("outputLength is not valid");
			return result;
		}

		#endregion
	}
}

// ReSharper restore InconsistentNaming
