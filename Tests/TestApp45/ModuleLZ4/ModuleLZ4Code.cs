using System;
using System.Text;
using LZ4;

namespace ModuleLZ4
{
	public class ModuleLZ4Code
	{
		public static void Run()
		{
			Console.WriteLine(LZ4Codec.CodecName);
			const string lorem =
				"Lorem ipsum dolor sit amet, consectetur adipisicing elit, sed do eiusmod tempor incididunt ut " +
					"labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud exercitation ullamco " +
					"laboris nisi ut aliquip ex ea commodo consequat. Duis aute irure dolor in reprehenderit in " +
					"voluptate velit esse cillum dolore eu fugiat nulla pariatur. Excepteur sint occaecat cupidatat " +
					"non proident, sunt in culpa qui officia deserunt mollit anim id est laborum. ";
			const string originalText = lorem + lorem + lorem + lorem + lorem + lorem + lorem;
			var originalBytes = Encoding.UTF8.GetBytes(originalText);
			var encodedBytes = LZ4Codec.Encode(originalBytes, 0, originalBytes.Length);
			if (encodedBytes.Length >= originalBytes.Length)
				throw new InvalidOperationException("Compression failed");
			var decodedBytes = LZ4Codec.Decode(encodedBytes, 0, encodedBytes.Length, originalBytes.Length);
			var decodedText = Encoding.UTF8.GetString(decodedBytes);
			if (decodedText != originalText)
				throw new InvalidOperationException("Compression/decompression failed");
		}
	}
}
