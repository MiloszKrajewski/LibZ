using System.IO;
using System.IO.Compression;
using System.Linq;
using LibZ.Msil;
using LibZ.Tool.InjectIL;
using Mono.Cecil;
using NLog;

namespace LibZ.Tool.Tasks
{
	/// <summary>
	///     Instruments the assembly with LibZ initialization.
	/// </summary>
	public class InstrumentLibZTask: TaskBase
	{
		#region consts

		/// <summary>Logger for this class.</summary>
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		#endregion

		#region fields

		/// <summary>The instrumentation helper</summary>
		private InstrumentHelper _instrumentHelper;

		#endregion

		#region public interface

		/// <summary>Executes the task.</summary>
		/// <param name="mainFileName">Name of the main file.</param>
		/// <param name="allLibZResources">
		///     if set to <c>true</c> loads all LibZ files in resources on startup.
		/// </param>
		/// <param name="libzFiles">The LibZ files to be loaded on startup.</param>
		/// <param name="libzPatterns">The libz file patterns to be loaded on startup.</param>
		/// <param name="keyFileName">Name of the key file.</param>
		/// <param name="keyFilePassword">The key file password.</param>
		public virtual void Execute(
			string mainFileName,
			bool allLibZResources,
			string[] libzFiles,
			string[] libzPatterns,
			string keyFileName, string keyFilePassword)
		{
			if (!File.Exists(mainFileName))
				throw FileNotFound(mainFileName);
			if (libzFiles == null)
				libzFiles = new string[0];
			if (libzPatterns == null)
				libzPatterns = new string[0];

			var targetAssembly = MsilUtilities.LoadAssembly(mainFileName);

			var keyPair = MsilUtilities.LoadKeyPair(keyFileName, keyFilePassword);
			var requiresAsmZResolver = false;

			var bootstrapAssembly =
				FindBootstrapAssembly(targetAssembly, mainFileName);

			if (bootstrapAssembly == null)
			{
				var version = MsilUtilities.GetFrameworkVersion(targetAssembly);

				var bootstrapAssemblyImage = InstrumentHelper.GetBootstrapAssemblyImage(version);
				bootstrapAssembly = MsilUtilities.LoadAssembly(bootstrapAssemblyImage);

				InjectDll(
					targetAssembly,
					bootstrapAssembly,
					bootstrapAssemblyImage,
					true);
				requiresAsmZResolver = true;
			}

			_instrumentHelper = new InstrumentHelper(
				targetAssembly,
				bootstrapAssembly);

			_instrumentHelper.InjectLibZInitializer();
			if (requiresAsmZResolver)
				_instrumentHelper.InjectAsmZResolver();
			_instrumentHelper.InjectLibZStartup(allLibZResources, libzFiles, libzPatterns);

			MsilUtilities.SaveAssembly(targetAssembly, mainFileName, keyPair);
		}

		#endregion

		#region private implementation

		/// <summary>Finds the bootstrap assembly.</summary>
		/// <param name="targetAssembly">The target assembly.</param>
		/// <param name="mainFileName">Name of the main file.</param>
		/// <returns>Loaded bootstrap assembly.</returns>
		private static AssemblyDefinition FindBootstrapAssembly(AssemblyDefinition targetAssembly, string mainFileName)
		{
			var typeLibZResolver = targetAssembly.MainModule.Types
				.SingleOrDefault(t => t.FullName == "LibZ.Bootstrap.LibZResolver");
			if (typeLibZResolver != null)
			{
				Log.Debug("LibZResolver has been found merged into main executable already");
				return targetAssembly;
			}

			var refLibZBootstrap = targetAssembly.MainModule.AssemblyReferences
				.FirstOrDefault(r => r.Name == "LibZ.Bootstrap");

			if (refLibZBootstrap != null)
			{
				var guid = HashString(refLibZBootstrap.FullName);

				var embedded = targetAssembly.MainModule.Resources
					.OfType<EmbeddedResource>()
					.Select(r => TryLoadAssembly(r, guid))
					.FirstOrDefault(b => b != null);

				if (embedded != null)
				{
					Log.Debug("LibZResolver has been found embedded into main executable");
					return AssemblyDefinition.ReadAssembly(new MemoryStream(embedded));
				}
			}

			var libzBootstrapFileName = Path.Combine(
				Path.GetDirectoryName(mainFileName) ?? ".",
				"LibZ.Bootstrap.dll");

			if (File.Exists(libzBootstrapFileName))
			{
				Log.Debug("LibZResolver has been found in the same folder as main executable");
				return AssemblyDefinition.ReadAssembly(libzBootstrapFileName);
			}

			Log.Warn("LibZResolver has not been found.");
			return null;
		}

		/// <summary>Tries to load assembly for other assembly resources.</summary>
		/// <param name="resource">The resource.</param>
		/// <param name="guid">The GUID.</param>
		/// <returns>Loaded assembly image.</returns>
		private static byte[] TryLoadAssembly(EmbeddedResource resource, string guid)
		{
			var match = ResourceNameRx.Match(resource.Name);
			if (!match.Success || match.Groups["guid"].Value != guid)
				return null;

			try
			{
				var flags = match.Groups["flags"].Value;
				var size = int.Parse(match.Groups["size"].Value);
				var compressed = flags.Contains("z");

				var buffer = new byte[size];

				using (var rstream = resource.GetResourceStream())
				{
					if (rstream == null)
						return null;
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

		#endregion
	}
}