using System;
using System.Linq;
using System.Reflection;
using ModuleByReference;

namespace ModuleBetween
{
	public class ModuleBetweenCode
	{
		public static void Run()
		{
			Exec(ExecModuleByFull);
			Exec(ExecModuleByPartial);
			Exec(ExecModuleByReference);
		}

		private static void ExecModuleByReference()
		{
			ModuleByReferenceCode.Run();
		}

		private static void ExecModuleByPartial()
		{
			var assembly = Assembly.Load("ModuleByPartial");
			var type = assembly.GetTypes().Single(t => t.Name == "ModuleByPartialCode");
			var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
			method.Invoke(null, new object[0]);
		}

		private static void ExecModuleByFull()
		{
			var assemblyName = new AssemblyName("ModuleByFull, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
			var assembly = Assembly.Load(assemblyName);
			var type = assembly.GetTypes().Single(t => t.Name == "ModuleByFullCode");
			var method = type.GetMethod("Run", BindingFlags.Public | BindingFlags.Static);
			method.Invoke(null, new object[0]);
		}

		private static void Exec(Action action)
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
