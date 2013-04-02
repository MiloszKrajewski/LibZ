namespace Softpark.LibZ
{
	public class LibZOptions
	{
		public static readonly LibZOptions Default = new LibZOptions { Codec = 0, Deflate = true, Password = null };

		public bool Encode { get { return Codec != 0; } }
		public bool Deflate { get; set; }
		public bool Encrypt { get { return Password != null; } }

		public uint Codec { get; set; }
		public string Password { get; set; }
	}
}
