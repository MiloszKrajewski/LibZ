using System.Reflection;

namespace LibZ.Manager
{
	public struct AssemblyInfo
	{
		public AssemblyName AssemblyName;
		public byte[] Bytes;
		public bool Unmanaged;
		public bool AnyCPU;
		public bool AMD64;

		public override string ToString()
		{
			return string.Format("{0}{1}",
			    AnyCPU ? string.Empty : AMD64 ? "x64:" : "x86:",
			    AssemblyName.FullName);
		}
	}
}