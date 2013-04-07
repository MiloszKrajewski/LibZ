using System.IO;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class InjectResourceTask : TaskBase
	{
		public void Execute(
			string libzFileName, string exeFileName, bool move)
		{
			if (libzFileName == null) throw ArgumentNull("libzFileName");
			if (!File.Exists(libzFileName)) throw FileNotFound(libzFileName);
			if (!File.Exists(exeFileName)) throw FileNotFound(exeFileName);

			var fileName = Path.GetFileName(libzFileName);

			var resourceName = "LibZ." + MD5(fileName);
			var assembly = AssemblyDefinition.ReadAssembly(exeFileName);
			var resource = new EmbeddedResource(
				resourceName,
				ManifestResourceAttributes.Public,
				File.ReadAllBytes(libzFileName));
			assembly.MainModule.Resources.Add(resource);
			Log.Info("Injecting '{0}' into '{1}'", libzFileName, exeFileName);
			assembly.Write(exeFileName);

			if (move) DeleteFile(libzFileName);
		}
	}
}