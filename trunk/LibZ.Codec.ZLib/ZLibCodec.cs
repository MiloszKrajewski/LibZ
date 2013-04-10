using System.ComponentModel.Composition;
using LibZ.Tool.Interfaces;

namespace LibZ.Codec.ZLib
{
	[Export(typeof(ICodec))]
	public class ZLibCodec: ICodec
	{
		#region ICodec Members

		public void Initialize()
		{
			// no need for it
		}

		public string Name
		{
			get { return "zlib"; }
		}

		public byte[] Encode(byte[] inputData)
		{
			return Ionic.Zlib.ZlibStream.CompressBuffer(inputData);
		}

		public byte[] Decode(byte[] inputData, int outputLength)
		{
			// apparently 'outputLength' is not needed in this case
			return Ionic.Zlib.ZlibStream.UncompressBuffer(inputData);
		}

		#endregion
	}
}
