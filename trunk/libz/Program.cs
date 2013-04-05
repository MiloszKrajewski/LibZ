using System;

namespace libz
{
	class Program
	{
		static int Main(string[] args)
		{
			try
			{
				LibZ.Bootstrap.LibZResolver.RegisterContainer("libzcli.libz");
				return Run(args);
			}
			catch (Exception e)
			{
				Console.WriteLine("Fatal initialization error");
				Console.WriteLine("{0}: {1}", e.GetType().Name, e.Message);
				Console.WriteLine(e.StackTrace);
				return -1;
			}
		}

		static int Run(string[] args)
		{
			return LibZ.Tool.Program.Run(args);
		}
	}
}
