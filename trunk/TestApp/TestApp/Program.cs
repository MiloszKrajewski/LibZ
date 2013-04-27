using System;
using ModuleArchitecture;
using ModuleBetween;
using ModuleLZ4;

namespace TestApp
{
	internal class Program
	{
		private static void Main()
		{
			try
			{
				Exec(ModuleLZ4Code.Run);
				Exec(ModuleArchitectureCode.Run);
				Exec(ModuleBetweenCode.Run);
			}
			catch (Exception e)
			{
				Console.WriteLine("FAILURE {0}: {1}", e.GetType().Name, e.Message);
			}
			finally
			{
				Console.WriteLine("Press <enter>...");
				Console.ReadLine();
			}
		}

		public static void Exec(Action action)
		{
			try
			{
				action();
			}
			catch (Exception e)
			{
				Console.WriteLine("FAILURE {0}: {1}", e.GetType().Name, e.Message);
			}
		}
	}
}
