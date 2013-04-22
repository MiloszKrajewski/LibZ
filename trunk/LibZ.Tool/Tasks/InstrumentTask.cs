using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using LibZ.Tool.InjectIL;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class InstrumentTask: TaskBase
	{
		private InstrumentHelper _instrumentHelper;

		public void Execute(
			string mainFileName,
			bool allAsmZResources,
			bool allLibZResources,
			ICollection<string> libzFiles,
			ICollection<string> libzFolders)
		{
			var targetAssembly = LoadAssembly(mainFileName);

			var bootstrapAssembly = FindBootstrapAssembly(targetAssembly, mainFileName);

			_instrumentHelper = new InstrumentHelper(targetAssembly, bootstrapAssembly);
			_instrumentHelper.InjectLibZInitializer();
			_instrumentHelper.InjectAsmZResolver(!allAsmZResources);
			_instrumentHelper.InjectLibZStartup(allLibZResources, libzFiles, libzFolders);

			targetAssembly.Write(mainFileName + ".temp" + Path.GetExtension(mainFileName));
		}

		private static readonly Regex ResourceNamePattern = new Regex(
			@"asmz://(?<guid>[^/]*)/(?<size>[0-9]+)(/(?<flags>[a-zA-Z0-9]*))?",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		private AssemblyDefinition FindBootstrapAssembly(AssemblyDefinition targetAssembly, string mainFileName)
		{
			TypeDefinition typeLibZResolver;

			typeLibZResolver = targetAssembly.MainModule.Types.Single(t => t.FullName == "LibZ.Bootstrap.LibZResolver");
			if (typeLibZResolver != null)
			{
				// LibZResolver is merged
				return targetAssembly;
			}

			var refLibZBootstrap = targetAssembly.MainModule.AssemblyReferences.Single(r => r.Name == "LibZ.Bootstrap");
			var guid = MD5(refLibZBootstrap.FullName);

			var embedded = targetAssembly.MainModule.Resources
				.OfType<EmbeddedResource>()
				.Select(r => TryLoadAssembly(r, guid))
				.FirstOrDefault(b => b != null);

			if (embedded != null)
			{
				// LibZResolver is embedded
				return AssemblyDefinition.ReadAssembly(new MemoryStream(embedded));
			}

			var libzBootstrapFileName = Path.Combine(
				Path.GetDirectoryName(mainFileName) ?? ".", 
				refLibZBootstrap.Name + ".dll");

			if (File.Exists(libzBootstrapFileName))
			{
				return AssemblyDefinition.ReadAssembly(libzBootstrapFileName);
			}

			return null;
		}

		private static byte[] TryLoadAssembly(EmbeddedResource resource, string guid)
		{
			var match = ResourceNamePattern.Match(resource.Name);
			if (!match.Success || match.Groups["guid"].Value != guid) return null;

			try
			{
				var flags = match.Groups["flags"].Value;
				var size = int.Parse(match.Groups["size"].Value);
				var compressed = flags.Contains("z");

				var buffer = new byte[size];

				using (var rstream = resource.GetResourceStream())
				{
					if (rstream == null) return null;
					using (var zstream = compressed ? new DeflateStream(rstream, CompressionMode.Decompress) : rstream)
					{
						zstream.Read(buffer, 0, size);
					}
				}

				return buffer;
			}
			catch
			{
				return null;
			}
		}

	}
}
