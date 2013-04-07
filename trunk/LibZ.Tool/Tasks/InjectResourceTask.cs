using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class InjectResourceTask: TaskBase
	{
		public void Execute(
			string libzFileName, string exeFileName,
			string keyFileName, string keyFilePassword,
			bool move)
		{
			if (libzFileName == null) throw ArgumentNull("libzFileName");
			if (!File.Exists(libzFileName)) throw FileNotFound(libzFileName);
			if (!File.Exists(exeFileName)) throw FileNotFound(exeFileName);
			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			var fileName = Path.GetFileName(libzFileName);

			var assembly = LoadAssembly(exeFileName);

			var resourceName = "LibZ." + MD5(fileName);
			var resource = new EmbeddedResource(
				resourceName,
				ManifestResourceAttributes.Public,
				File.ReadAllBytes(libzFileName));
			assembly.MainModule.Resources.Add(resource);
			Log.Info("Injecting '{0}' into '{1}'", libzFileName, exeFileName);

			SaveAssembly(assembly, exeFileName, keyPair);

			if (move) DeleteFile(libzFileName);
		}
	}
}