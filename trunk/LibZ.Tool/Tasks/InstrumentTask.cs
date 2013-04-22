using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
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
			ICollection<string> libzFolders,
			string keyFileName, string keyFilePassword)
		{
			if (!File.Exists(mainFileName)) throw FileNotFound(mainFileName);
			if (libzFiles == null) libzFiles = new string[0];
			if (libzFolders == null) libzFolders = new string[0];

			var targetAssembly = LoadAssembly(mainFileName);
			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			_instrumentHelper = new InstrumentHelper(
				targetAssembly, 
				() => FindBootstrapAssembly(targetAssembly, mainFileName));
			_instrumentHelper.InjectLibZInitializer();
			_instrumentHelper.InjectAsmZResolver(!allAsmZResources);
			_instrumentHelper.InjectLibZStartup(allLibZResources, libzFiles, libzFolders);

			SaveAssembly(targetAssembly, mainFileName, keyPair);
		}

		private static readonly Regex ResourceNameRx = new Regex(
			@"asmz://(?<guid>[^/]*)/(?<size>[0-9]+)(/(?<flags>[a-zA-Z0-9]*))?",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		private static AssemblyDefinition FindBootstrapAssembly(AssemblyDefinition targetAssembly, string mainFileName)
		{
			TypeDefinition typeLibZResolver = targetAssembly.MainModule.Types
				.SingleOrDefault(t => t.FullName == "LibZ.Bootstrap.LibZResolver");
			if (typeLibZResolver != null)
			{
				Log.Debug("LibZResolver has been found merged into main executable");
				return targetAssembly;
			}

			var refLibZBootstrap = targetAssembly.MainModule.AssemblyReferences
				.FirstOrDefault(r => r.Name == "LibZ.Bootstrap");

			if (refLibZBootstrap == null)
			{
				throw new InvalidOperationException(
					"Target assembly does not reference LibZ.Bootstrap, thus using .libz file is not allowed.");
			}

			var guid = HashString(refLibZBootstrap.FullName);

			var embedded = targetAssembly.MainModule.Resources
				.OfType<EmbeddedResource>()
				.Select(r => TryLoadAssembly(r, guid))
				.FirstOrDefault(b => b != null);

			// TODO:MAK allow merging even if does not reference LibZ.Bootstrap at all
			if (embedded != null)
			{
				Log.Debug("LibZResolver has been found embedded into main executable");
				return AssemblyDefinition.ReadAssembly(new MemoryStream(embedded));
			}

			var libzBootstrapFileName = Path.Combine(
				Path.GetDirectoryName(mainFileName) ?? ".", 
				refLibZBootstrap.Name + ".dll");

			if (File.Exists(libzBootstrapFileName))
			{
				Log.Debug("LibZResolver has been found in the same folder as main executable");
				return AssemblyDefinition.ReadAssembly(libzBootstrapFileName);
			}

			Log.Warn("LibZResolver has not been found.");
			return null;
		}

		private static byte[] TryLoadAssembly(EmbeddedResource resource, string guid)
		{
			var match = ResourceNameRx.Match(resource.Name);
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
