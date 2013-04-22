using System.IO;
using Mono.Cecil;
using System.Collections.Generic;

namespace LibZ.Tool.Tasks
{
	public class InjectLibZTask: TaskBase
	{
		public virtual void Execute(
			string mainFileName, string[] libzFileNames,
			string keyFileName, string keyFilePassword,
			bool move)
		{
			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);
			var assembly = LoadAssembly(mainFileName);
			var injectedFileNames = new List<string>();

			// TODO:MAK exclude?
			foreach (var libzFileName in FindFiles(libzFileNames, new string[0]))
			{
				if (libzFileName == null) throw ArgumentNull("libzFileName");
				if (!File.Exists(libzFileName)) throw FileNotFound(libzFileName);
				if (!File.Exists(mainFileName)) throw FileNotFound(mainFileName);

				var fileName = Path.GetFileName(libzFileName); // TODO:MAK relative path?

				var resourceName = "libz://" + HashString(fileName);
				var resource = new EmbeddedResource(
					resourceName,
					ManifestResourceAttributes.Public,
					File.ReadAllBytes(libzFileName));
				assembly.MainModule.Resources.Add(resource);
				Log.Info("Injecting '{0}' into '{1}'", libzFileName, mainFileName);
				injectedFileNames.Add(libzFileName);
			}

			if (injectedFileNames.Count <= 0)
			{
				Log.Warn("No files injected: {0}", string.Join(", ", libzFileNames));
			}
			else
			{
				SaveAssembly(assembly, mainFileName, keyPair);
				if (move) foreach (var fn in injectedFileNames) DeleteFile(fn);
			}
		}
	}
}