using System;

namespace ModuleArchitecture
{
	public class ModuleArchitectureCode
	{
		public static void Run()
		{
#if X64
			Console.WriteLine("64-bit version (in {0} bit code)", IntPtr.Size*8);
#elif X86
			Console.WriteLine("32-bit version (in {0} bit code)", IntPtr.Size * 8);
#else
			Console.WriteLine("AnyCPU version (in {0} bit code)", IntPtr.Size*8);
#endif
		}
	}
}
