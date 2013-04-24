using System;
using System.ComponentModel.Composition;
using LibZ.Tool.Interfaces;

namespace LibZ.Codec.LZ4
{
	[Export(typeof (ICodec))]
	public class LZ4Codec: ICodec
	{
		#region ICodec Members

		public void Initialize()
		{
			Console.WriteLine("LZ4 initialized as '{0}'", global::LZ4.LZ4Codec.CodecName);
		}

		public string Name
		{
			get { return "lz4"; }
		}

		public byte[] Encode(byte[] inputData)
		{
			return global::LZ4.LZ4Codec.Encode(inputData, 0, inputData.Length);
		}

		public byte[] Decode(byte[] inputData, int outputLength)
		{
			return global::LZ4.LZ4Codec.Decode(inputData, 0, inputData.Length, outputLength);
		}

		#endregion
	}
}
