using System;
using Linq20;
using ModuleArchitecture;
using ModuleBetween;
namespace TestApp
{
	internal class Program
	{
		private static void Main()
		{
			try
			{
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
