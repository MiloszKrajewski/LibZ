using System;

namespace LibZ.Tool
{
	class Program
	{
		static Program()
		{
			Bootstrap.LibZResolver.RegisterContainer("LibZ.Tool.libz");
		}

		static void Main(string[] args)
		{
			Core.Program.Run(args);
		}
	}
}
