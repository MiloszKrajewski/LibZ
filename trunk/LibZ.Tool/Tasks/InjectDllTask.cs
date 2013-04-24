using System.Collections.Generic;
using System.IO;

namespace LibZ.Tool.Tasks
{
	public class InjectDllTask: TaskBase
	{
		public void Execute(
			string mainFileName,
			string[] includePatterns,
			string[] excludePatterns,
			string keyFileName, string keyFilePassword,
			bool overwrite, bool move)
		{
			if (!File.Exists(mainFileName)) throw FileNotFound(mainFileName);

			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			var assembly = LoadAssembly(mainFileName);
			var injectedFileNames = new List<string>();

			foreach (var fileName in FindFiles(includePatterns, excludePatterns))
			{
				var sourceAssembly = LoadAssembly(fileName);
				if (sourceAssembly == null)
				{
					Log.Error("Assembly '{0}' could not be loaded", fileName);
					continue;
				}

				Log.Info("Injecting '{0}' into '{1}'", fileName, mainFileName);
				if (!InjectDll(assembly, sourceAssembly, File.ReadAllBytes(fileName), overwrite)) continue;

				injectedFileNames.Add(fileName);
			}

			if (injectedFileNames.Count <= 0)
			{
				Log.Warn("No files injected: {0}", string.Join(", ", includePatterns));
			}
			else
			{
				Log.Info("Instrumenting assembly with initialization code"); 
				InstrumentAsmZ(assembly);

				SaveAssembly(assembly, mainFileName, keyPair);

				if (move) foreach (var fn in injectedFileNames) DeleteFile(fn);
			}
		}
	}
}
