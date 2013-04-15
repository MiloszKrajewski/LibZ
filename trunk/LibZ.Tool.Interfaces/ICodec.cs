namespace LibZ.Tool.Interfaces
{
	/// <summary>LibZ compression codec.</summary>
	public interface ICodec
	{
		/// <summary>Initializes the codec. 
		/// Leave it empty if initialization is not needed.</summary>
		void Initialize();

		/// <summary>Gets the name.</summary>
		/// <value>The name.</value>
		string Name { get; }

		/// <summary>Encodes the specified input data.</summary>
		/// <param name="inputData">The input data.</param>
		/// <returns>Encodec data.</returns>
		byte[] Encode(byte[] inputData);

		/// <summary>Decodes the specified input data.</summary>
		/// <param name="inputData">The input data.</param>
		/// <param name="outputLength">Length of the output.</param>
		/// <returns>Decoded data.</returns>
		byte[] Decode(byte[] inputData, int outputLength);
	}
}
