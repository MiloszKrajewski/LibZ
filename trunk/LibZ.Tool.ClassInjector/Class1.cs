using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibZ.Bootstrap;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace LibZ.Tool.ClassInjector
{
	[TestFixture]
	public class Class1
	{
		private AssemblyDefinition _into;

		[Test]
		public void Test()
		{
			var from = AssemblyDefinition.ReadAssembly("LibZ.Bootstrap.dll");
			_into = AssemblyDefinition.ReadAssembly("LibZ.Tool.ClassInjector.dll");

			var source = from.MainModule.Types.Single(t => t.FullName == "LibZ.Bootstrap.AsmZResolver");
			var copier = new TemplateCopy(from, _into, source);
			copier.Run();

			//_into.AddReference(from);
			//var moduleType = _into.GetTypeReference("<Module>");
			//if (moduleType == null) throw new ArgumentException("No <Module> class found");
			//var constructor = moduleType.GetStaticConstructor() ?? moduleType.CreateStaticConstructor();
			//ExtendConstructor(constructor);

			_into.Write("x.dll");
		}

		private void ExtendConstructor(MethodDefinition constructor)
		{
			var assembly = constructor.DeclaringType.Module.Assembly;
			var il = constructor.Body.GetILProcessor();

			il.Emit(OpCodes.Ldstr, "Module initialization started");
			il.Emit(OpCodes.Call, assembly.ImportMethod(typeof(Console), "WriteLine", typeof(string)));

			il.Emit(OpCodes.Ldtoken, constructor.DeclaringType);
			il.Emit(OpCodes.Call, assembly.ImportMethod<Type>("GetTypeFromHandle"));
			il.Emit(OpCodes.Call, assembly.ImportMethod<LibZResolver>("RegisterAllResourceContainers"));
			il.Emit(OpCodes.Pop);

			il.Emit(OpCodes.Ldstr, "containers.libz");
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Call, assembly.ImportMethod<LibZResolver>("RegisterFileContainer"));
			il.Emit(OpCodes.Pop);

			il.Emit(OpCodes.Call, assembly.ImportMethod<AppDomain>("get_CurrentDomain"));
			il.Emit(OpCodes.Callvirt, assembly.ImportMethod<AppDomain>("get_BaseDirectory"));
			il.Emit(OpCodes.Ldstr, "*.libz");
			il.Emit(OpCodes.Call, assembly.ImportMethod(typeof(Path), "Combine", typeof(string), typeof(string)));
			il.Emit(OpCodes.Call, assembly.ImportMethod<LibZResolver>("RegisterMultipleFileContainers"));
			il.Emit(OpCodes.Pop);

			il.Emit(OpCodes.Ldstr, "Module initialization done");
			il.Emit(OpCodes.Call, assembly.ImportMethod(typeof(Console), "WriteLine", typeof(string)));

			il.Emit(OpCodes.Ret);
		}
	}
}