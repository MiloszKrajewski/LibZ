using System.IO;
using System.Linq;
using LibZ.Tool.ClassInjector;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LibZ.Tool.InjectIL
{
	public class InstrumentHelper
	{
		private readonly AssemblyDefinition _sourceAssembly;
		private readonly AssemblyDefinition _targetAssembly;

		public InstrumentHelper(AssemblyDefinition targetAssembly)
		{
			_sourceAssembly = AssemblyDefinition.ReadAssembly(
				new MemoryStream(
					Precompiled.LibZInjectedAssembly));
			_targetAssembly = targetAssembly;
		}

		public void InjectLibZInitializer()
		{
			var targetType = _targetAssembly.MainModule.Types.SingleOrDefault(
				t => t.FullName == "LibZ.Injected.LibZInitializer");

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
			var sourceType = _targetAssembly.MainModule.Types.SingleOrDefault(
				t => t.FullName == "LibZ.Injected.LibZInitializer");

			var helper = new TemplateCopy(_sourceAssembly, _targetAssembly, sourceType);
			helper.Run();
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

		public void InjectAsmZResolver()
		{
			var sourceType = _targetAssembly.MainModule.Types.SingleOrDefault(
				t => t.FullName == "LibZ.Injected.AsmZResolver");

			var helper = new TemplateCopy(_sourceAssembly, _targetAssembly, sourceType);
			helper.Run();
		}
	}
}
