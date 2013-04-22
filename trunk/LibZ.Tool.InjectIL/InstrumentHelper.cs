using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LibZ.Tool.InjectIL
{
	public class InstrumentHelper
	{
		private readonly AssemblyDefinition _sourceAssembly;
		private readonly AssemblyDefinition _targetAssembly;
		private readonly AssemblyDefinition _bootstrapAssembly;

		public InstrumentHelper(AssemblyDefinition targetAssembly, AssemblyDefinition bootstrapAssembly)
		{
			_sourceAssembly = AssemblyDefinition.ReadAssembly(
				new MemoryStream(
					Precompiled.LibZInjectedAssembly));
			_targetAssembly = targetAssembly;
			_bootstrapAssembly = bootstrapAssembly;
		}

		public void InjectLibZInitializer()
		{
			const string typeName = "LibZ.Injected.LibZInitializer";
			var targetType = _targetAssembly.MainModule.Types.SingleOrDefault(t => t.FullName == typeName);

			if (targetType == null)
			{
				CloneLibZInitializer();
			}
			else
			{
				CleanupLibZInitializer(targetType);
			}
		}

		private void CloneLibZInitializer()
		{
			const string typeName = "LibZ.Injected.LibZInitializer";
			var sourceType = _sourceAssembly.MainModule.Types.Single(t => t.FullName == typeName);

			new TemplateCopy(_sourceAssembly, _targetAssembly, sourceType).Run();

			var targetType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeName);
			var targetMethod = targetType.Methods.Single(m => m.Name == "Initialize");

			// find 'module' static constructor
			var moduleType = _targetAssembly.MainModule.Types.Single(t => t.FullName == "<Module>");
			var moduleCtor = moduleType.Methods.SingleOrDefault(m => m.Name == ".cctor");

			// if module static constructor has not been found create it
			if (moduleCtor == null)
			{
				// private hidebysig specialname rtspecialname static - at least that's what other static constructors have
				const MethodAttributes attributes =
					MethodAttributes.Private | MethodAttributes.Static | 
					MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
					MethodAttributes.HideBySig;
				moduleCtor = new MethodDefinition(".cctor", attributes, _targetAssembly.MainModule.TypeSystem.Void);
				moduleCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
				moduleType.Methods.Add(moduleCtor);
			}

			// whenever it was just created or existed before - inject LibZ initialization into it
			moduleCtor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, targetMethod));
		}

		private static void CleanupLibZInitializer(TypeDefinition targetType)
		{
			CleanupMethod(targetType, "InitializeAsmZ");
			CleanupMethod(targetType, "InitializeLibZ");
		}

		private static void CleanupMethod(TypeDefinition targetType, string methodName)
		{
			var method = targetType.Methods.Single(m => m.Name == methodName);
			method.Body.Instructions.Clear();
			method.Body.Variables.Clear();
			var il = method.Body.GetILProcessor();
			il.Emit(OpCodes.Ret);
		}

		public void InjectAsmZResolver(bool remove)
		{
			const string typeLibZInitializer = "LibZ.Injected.LibZInitializer";
			var initializerType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeLibZInitializer);
			var initializerMethod = initializerType.Methods.Single(m => m.Name == "InitializeAsmZ");

			initializerMethod.Body.Instructions.Clear();

			if (!remove)
			{
				const string typeAsmZResolver = "LibZ.Injected.AsmZResolver";
				var sourceType = _sourceAssembly.MainModule.Types.Single(t => t.FullName == typeAsmZResolver);
				new TemplateCopy(_sourceAssembly, _targetAssembly, sourceType).Run();
				var targetType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeAsmZResolver);
				var targetMethod = targetType.Methods.Single(m => m.Name == "Initialize");
				initializerMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Call, targetMethod));
			}

			initializerMethod.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
		}

		public void InjectLibZStartup(bool allResources, ICollection<string> libzFiles, ICollection<string> libzFolders)
		{
			var remove = !allResources && libzFiles.Count <= 0 && libzFolders.Count <= 0;

			const string typeLibZInitializer = "LibZ.Injected.LibZInitializer";
			var initializerType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeLibZInitializer);
			var initializerMethod = initializerType.Methods.Single(m => m.Name == "InitializeLibZ");

			var body = initializerMethod.Body.Instructions;

			body.Clear();

			if (!remove)
			{
				if (_bootstrapAssembly == null)
					throw new InvalidOperationException(
						"LibZ.Bootstrap could not be found so no LibZ initialization can be performed");

				const string typeLibZResolver = "LibZ.Bootstrap.LibZResolver";
				var targetType = _bootstrapAssembly.MainModule.Types.Single(t => t.FullName == typeLibZResolver);

				if (allResources)
				{
					var targetMethod = targetType.Methods.Single(m => m.Name == "RegisterAllResourceContainers");
					body.Add(Instruction.Create(OpCodes.Ldtoken, initializerType));
					body.Add(Instruction.Create(OpCodes.Call, _targetAssembly.ImportMethod<Type>("GetTypeFromHandle")));
					body.Add(Instruction.Create(OpCodes.Call, targetMethod));
					body.Add(Instruction.Create(OpCodes.Pop));
				}

				if (libzFiles.Count > 0)
				{
					var targetMethod = targetType.Methods.Single(m => m.Name == "RegisterFileContainer");
					foreach (var libzFile in libzFiles)
					{
						body.Add(Instruction.Create(OpCodes.Ldstr, libzFile));
						body.Add(Instruction.Create(OpCodes.Ldc_I4_1));
						body.Add(Instruction.Create(OpCodes.Call, targetMethod));
						body.Add(Instruction.Create(OpCodes.Pop));
					}
				}

				if (libzFolders.Count > 0)
				{
					var targetMethod = targetType.Methods.Single(m => m.Name == "RegisterMultipleFileContainers");
					foreach (var libzFolder in libzFolders)
					{
						body.Add(Instruction.Create(OpCodes.Ldstr, libzFolder));
						body.Add(Instruction.Create(OpCodes.Call, targetMethod));
						body.Add(Instruction.Create(OpCodes.Pop));
					}
				}
			}

			body.Add(Instruction.Create(OpCodes.Ret));
		}
	}
}
