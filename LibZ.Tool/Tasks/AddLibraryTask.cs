using System.Collections.Generic;
using System.IO;
using System.Reflection;
using LibZ.Manager;
using LibZ.Msil;

namespace LibZ.Tool.Tasks
{
	/// <summary>
	/// Adds assemblies to LibZ library.
	/// </summary>
	public class AddLibraryTask: TaskBase
	{
		/// <summary>Executes the task.</summary>
		/// <param name="libzFileName">Name of the libz file.</param>
		/// <param name="includePatterns">The include patterns.</param>
		/// <param name="excludePatterns">The exclude patterns.</param>
		/// <param name="codecName">Name of the codec.</param>
		/// <param name="move">if set to <c>true</c> moves files (deletes soure files).</param>
		/// <param name="overwrite">if set to <c>true</c> overwrites existing resources.</param>
		public virtual void Execute(
			string libzFileName,
			string[] includePatterns, string[] excludePatterns,
			string codecName, bool move, bool overwrite)
		{
			var injectedFileNames = new List<string>();
			if (string.IsNullOrEmpty(codecName)) codecName = "deflate";

			using (var container = new LibZContainer(libzFileName, true))
			{
				foreach (var fileName in FindFiles(includePatterns, excludePatterns))
				{
					var assembly = MsilUtilities.LoadAssembly(fileName);
					if (assembly == null)
					{
						Log.Warn("Assembly from '{0}' could not be loaded", fileName);
						continue;
					}
					var assemblyName = assembly.Name;
					var managed = MsilUtilities.IsManaged(assembly);
					var architecture = MsilUtilities.GetArchitecture(assembly);
					var portable = MsilUtilities.IsPortable(assembly);

					var assemblyInfo = new AssemblyInfo {
						AssemblyName = new AssemblyName(assemblyName.FullName),
						AnyCPU = architecture == AssemblyArchitecture.AnyCPU,
						AMD64 = architecture == AssemblyArchitecture.X64,
						Unmanaged = !managed,
						Portable = portable,
						Bytes = File.ReadAllBytes(fileName),
					};

					Log.Info("Appending '{0}' from '{1}'", assemblyInfo.AssemblyName, fileName);

					container.Append(
						assemblyInfo,
						new AppendOptions {CodecName = codecName, Overwrite = overwrite,});

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
