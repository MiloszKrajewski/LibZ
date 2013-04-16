using System;
using System.IO;
using System.Reflection;
using LibZ.Manager;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class AddLibraryTask: TaskBase
	{
		public void Execute(
			string libzFileName, 
			string[] patterns, string[] excludePatterns, 
			string codecName, bool move, bool overwrite)
		{
			using (var container = new LibZContainer(libzFileName, true))
			{
				var count = 0;
				foreach (var fileName in FindFiles(patterns, excludePatterns))
				{
					var assembly = LoadAssembly(fileName);
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

					Log.Info("Appending '{0}' from '{1}'", assemblyInfo, fileName);

					container.Append(
						assemblyInfo,
						new AppendOptions { CodecName = codecName, Overwrite = overwrite, });
					if (move) DeleteFile(fileName);

					count++;
				}

				if (count == 0)
					Log.Warn("No files found: {0}", string.Join(", ", patterns));
			}
		}
	}
}