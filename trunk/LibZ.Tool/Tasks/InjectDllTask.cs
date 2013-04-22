using System.IO;
using System.IO.Compression;
using Mono.Cecil;
using System.Collections.Generic;
using System.Linq;

namespace LibZ.Tool.Tasks
{
	public class InjectDllTask: TaskBase
	{
		public void Execute(
			string mainFileName, 
			IEnumerable<string> includePatterns,
			IEnumerable<string> excludePatterns,
			string keyFileName, string keyFilePassword,
			bool overwrite, bool move)
		{
			if (!File.Exists(mainFileName)) throw FileNotFound(mainFileName);

			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			var assembly = LoadAssembly(mainFileName);
			var injectedFileNames = new List<string>();

			foreach (var fileName in FindFiles(includePatterns, excludePatterns))
			{
				string flags = string.Empty;

				var inputAssembly = LoadAssembly(fileName);
				if (inputAssembly == null)
				{
					Log.Error("Assembly '{0}' could not bne loaded", fileName);
					continue;
				}

				if (!IsManaged(inputAssembly)) flags += "u";

				byte[] input = File.ReadAllBytes(fileName);
				byte[] output;

				using (var ostream = new MemoryStream())
				{
					using (var zstream = new DeflateStream(ostream, CompressionMode.Compress))
					{
						zstream.Write(input, 0, input.Length);
						zstream.Flush();
					}
					output = ostream.ToArray();
				}

				if (output.Length < input.Length) flags += "z"; else output = input;

				var resourceName = string.Format(
					"asmz://{0}/{1}/{2}", 
					HashString(inputAssembly.FullName), input.Length, flags);

				var existing = assembly.MainModule.Resources
					.Where(r => r.Name == resourceName)
					.ToArray();

				if (existing.Length > 0)
				{
					if (overwrite)
					{
						Log.Warn("Resource '{0}' already exists and is going to be replaced.", resourceName);
						foreach (var r in existing)
							assembly.MainModule.Resources.Remove(r);
					}
					else
					{
						Log.Warn("Resource '{0}' already exists and will be skipped.", resourceName);
						continue;
					}
				}

				var resource = new EmbeddedResource(
					resourceName,
					ManifestResourceAttributes.Public,
					output);
				assembly.MainModule.Resources.Add(resource);
				Log.Info("Injecting '{0}' into '{1}'", fileName, mainFileName);

				injectedFileNames.Add(fileName);
			}

			if (injectedFileNames.Count <= 0)
			{
				Log.Warn("No files injected: {0}", string.Join(", ", includePatterns));
			}
			else
			{
				SaveAssembly(assembly, mainFileName, keyPair);
				if (move) foreach (var fn in injectedFileNames) DeleteFile(fn);
			}
		}
	}
}