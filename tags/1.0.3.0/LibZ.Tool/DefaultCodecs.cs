using System.IO;
using Ionic.Zlib;

namespace LibZ.Tool
{
	/// <summary>Proxy class passing call to Ionic.ZLib.</summary>
	public class DefaultCodecs
	{
		/// <summary>Deflate encoder, compatible with .NET one but with bettern compression 
		/// ratio (in .NET 4.0, in .NET 4.5 they are identical).</summary>
		/// <param name="input">The input.</param>
		/// <returns>Compressed data.</returns>
		public static byte[] DeflateEncoder(byte[] input)
		{
			using (var ostream = new MemoryStream())
			{
				using (var zstream = new DeflateStream(ostream, CompressionMode.Compress))
				{
					zstream.Write(input, 0, input.Length);
					zstream.Flush();
				}
				return ostream.ToArray();
			}
		}

		/// <summary>ZLib encoder.</summary>
		/// <param name="input">The input.</param>
		/// <returns>Compressed data.</returns>
		public static byte[] ZLibEncoder(byte[] input)
		{
			return ZlibStream.CompressBuffer(input);
		}

		/// <summary>ZLib decoder.</summary>
		/// <param name="input">The input.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <returns>Decompressed data.</returns>
		public static byte[] ZLibDecoder(byte[] input, int outputLength)
		{
			return ZlibStream.UncompressBuffer(input);
		}
	}
}
