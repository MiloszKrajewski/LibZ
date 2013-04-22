using System.IO;
using System.Reflection;
using LibZ.Manager;
using System.Collections.Generic;

namespace LibZ.Tool.Tasks
{
	public class AddLibraryTask: TaskBase
	{
		public void Execute(
			string libzFileName, 
			string[] includePatterns, string[] excludePatterns, 
			string codecName, bool move, bool overwrite)
		{
			var injectedFileNames = new List<string>();

			using (var container = new LibZContainer(libzFileName, true))
			{
				foreach (var fileName in FindFiles(includePatterns, excludePatterns))
				{
					var assembly = LoadAssembly(fileName);
					if (assembly == null)
					{
						Log.Warn("Assembly from '{0}' could not be loaded", fileName);
						continue;
					}
					var assemblyName = assembly.Name;
					var managed = IsManaged(assembly);
					var architecture = GetArchitecture(assembly);

					var assemblyInfo = new AssemblyInfo {
						AssemblyName = new AssemblyName(assemblyName.FullName),
						AnyCPU = architecture == AssemblyArchitecture.AnyCPU,
						AMD64 = architecture == AssemblyArchitecture.X64,
						Unmanaged = !managed,
						Bytes = File.ReadAllBytes(fileName),
					};

					Log.Info("Appending '{0}' from '{1}'", assemblyInfo.AssemblyName, fileName);

					container.Append(
						assemblyInfo,
						new AppendOptions { CodecName = codecName, Overwrite = overwrite, });

					injectedFileNames.Add(fileName);
				}

				if (injectedFileNames.Count <= 0)
				{
					Log.Warn("No files injected: {0}", string.Join(", ", includePatterns));
				}
				else
				{
					if (move) foreach (var fn in injectedFileNames) DeleteFile(fn);
				}
			}
		}
	}
}