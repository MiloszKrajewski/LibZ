using System;
using System.ComponentModel.Composition;
using LibZ.Tool.Interfaces;

namespace LibZ.Codec.LZ4
{
	/// <summary>Demo LZ4 codec.</summary>
	[Export(typeof(ICodec))]
	// ReSharper disable InconsistentNaming
	public class LZ4Codec: ICodec
	// ReSharper restore InconsistentNaming
	{
		#region ICodec Members

		/// <summary>Initializes the codec.
		/// Leave it empty if initialization is not needed.</summary>
		public void Initialize()
		{
			Console.WriteLine("LZ4 initialized as '{0}'", global::LZ4.LZ4Codec.CodecName);
		}

		/// <summary>Gets the name.</summary>
		/// <value>The name.</value>
		public string Name
		{
			get { return "lz4"; }
		}

		/// <summary>Encodes the specified input data.</summary>
		/// <param name="inputData">The input data.</param>
		/// <returns>Encodec data.</returns>
		public byte[] Encode(byte[] inputData)
		{
			return global::LZ4.LZ4Codec.EncodeHC(inputData, 0, inputData.Length);
		}

		/// <summary>Decodes the specified input data.</summary>
		/// <param name="inputData">The input data.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <returns>Decoded data.</returns>
		public byte[] Decode(byte[] inputData, int outputLength)
		{
			return global::LZ4.LZ4Codec.Decode(inputData, 0, inputData.Length, outputLength);
		}

		#endregion
	}
}
