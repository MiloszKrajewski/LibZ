using System;
using System.ComponentModel.Composition;
using LibZ.Tool.Interfaces;

namespace LibZ.Codec.Doboz
{
	/// <summary>Demo LZ4 codec.</summary>
	[Export(typeof(ICodec))]
	public class DobozCodec: ICodec
	{
		#region ICodec Members

		/// <summary>Initializes the codec.
		/// Leave it empty if initialization is not needed.</summary>
		public void Initialize()
		{
			Console.WriteLine("Doboz initialized as '{0}'", global::Doboz.DobozCodec.CodecName);
		}

		/// <summary>Gets the name.</summary>
		/// <value>The name.</value>
		public string Name
		{
			get { return "doboz"; }
		}

		/// <summary>Encodes the specified input data.</summary>
		/// <param name="inputData">The input data.</param>
		/// <returns>Encodec data.</returns>
		public byte[] Encode(byte[] inputData)
		{
			return global::Doboz.DobozCodec.Encode(inputData, 0, inputData.Length);
		}

		/// <summary>Decodes the specified input data.</summary>
		/// <param name="inputData">The input data.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <returns>Decoded data.</returns>
		public byte[] Decode(byte[] inputData, int outputLength)
		{
			return global::Doboz.DobozCodec.Decode(inputData, 0, inputData.Length);
		}

		#endregion
	}
}
