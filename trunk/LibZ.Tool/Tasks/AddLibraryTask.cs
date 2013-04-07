using System;
using System.IO;
using LibZ.Manager;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class AddLibraryTask: TaskBase
	{
		public void Execute(
			string libzFileName, string[] patterns, string codecName, bool move,
			Action<string> addingFile = null)
		{
			using (var container = new LibZContainer(libzFileName, true))
			{
				var count = 0;
				foreach (var fileName in FindFiles(patterns))
				{
					var assembly = AssemblyDefinition.ReadAssembly(fileName);
					var assemblyName = GetAssemblyName(assembly);
					var managed = IsManaged(assembly);
					var architecture = GetArchitecture(assembly);
					var resourceName =
						architecture == AssemblyArchitecture.X86 ? "x86:" :
						architecture == AssemblyArchitecture.X64 ? "x64:" :
						string.Empty;
					resourceName = resourceName + assemblyName;

					Log.Info("Appending '{0}' from '{1}", resourceName, fileName);

					container.Append(
						resourceName,
						assemblyName,
						File.ReadAllBytes(fileName),
						!managed, codecName);
					if (move) DeleteFile(fileName);
					count++;
				}

				if (count == 0)
					Log.Warn("No files found: {0}", string.Join(", ", patterns));
			}
		}
	}
}