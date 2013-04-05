using System;

namespace libz
{
	class Program
	{
		static int Main(string[] args)
		{
			try
			{
				LibZ.Bootstrap.LibZResolver.RegisterContainer("libz.libz");
				return Run(args);
			}
			catch (Exception e)
			{
				Console.WriteLine("Fatal initialization error");
				Console.WriteLine("{0}: {1}", e.GetType().Name, e.Message);
				Console.WriteLine(e.StackTrace);
				Console.ReadLine();
				return -1;
			}
		}

		static int Run(string[] args)
		{
			var program = new LibZ.Tool.Program();
			return program.Run(args);
		}
	}
}
