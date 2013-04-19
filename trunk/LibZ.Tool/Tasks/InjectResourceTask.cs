using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class InjectResourceTask: TaskBase
	{
		public void Execute(
			string libzFileName, string mainFileName,
			string keyFileName, string keyFilePassword,
			bool move)
		{
			if (libzFileName == null) throw ArgumentNull("libzFileName");
			if (!File.Exists(libzFileName)) throw FileNotFound(libzFileName);
			if (!File.Exists(mainFileName)) throw FileNotFound(mainFileName);
			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			var fileName = Path.GetFileName(libzFileName);

			var assembly = LoadAssembly(mainFileName);

			var resourceName = "LibZ." + MD5(fileName);
			var resource = new EmbeddedResource(
				resourceName,
				ManifestResourceAttributes.Public,
				File.ReadAllBytes(libzFileName));
			assembly.MainModule.Resources.Add(resource);
			Log.Info("Injecting '{0}' into '{1}'", libzFileName, mainFileName);

			SaveAssembly(assembly, mainFileName, keyPair);

			if (move) DeleteFile(libzFileName);
		}
	}
}