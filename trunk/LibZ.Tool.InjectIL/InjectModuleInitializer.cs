using System;
using System.IO;
using LibZ.Bootstrap;
using LibZ.Tool.ClassInjector;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LibZ.Tool.InjectIL
{
	class InjectModuleInitializer
	{
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
