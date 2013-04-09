namespace LibZ.Tool.Interfaces
{
    public interface ICodec
    {
		string Name { get; }
	    byte[] Encode(byte[] inputData);
		byte[] Decode(byte[] inputData, int outputLength);

		void Initialize();
	}
}
