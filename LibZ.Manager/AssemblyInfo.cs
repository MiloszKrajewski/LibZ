using System.Reflection;

namespace LibZ.Manager
{
	/// <summary>Assembly information.</summary>
	public struct AssemblyInfo
	{
		/// <summary>The assembly name</summary>
		public AssemblyName AssemblyName;

		/// <summary>Content of the assembly.</summary>
		public byte[] Bytes;

		/// <summary>Is unmanaged.</summary>
		public bool Unmanaged;

		/// <summary>Is AnyCPU</summary>
		public bool AnyCPU;

		/// <summary>Is AMD64</summary>
		public bool AMD64;
	}
}
