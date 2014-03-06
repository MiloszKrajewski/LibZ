using System.Collections.Generic;
using System.IO;
using LibZ.Msil;
using Mono.Cecil;
using NLog;

namespace LibZ.Tool.Tasks
{
	/// <summary>Injects LibZ container into assembly.</summary>
	public class InjectLibZTask: TaskBase
	{
		#region consts

		/// <summary>Logger for this class.</summary>
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		#endregion

		/// <summary>Executes the task.</summary>
		/// <param name="mainFileName">Name of the main file.</param>
		/// <param name="libzFileNames">The libz file names.</param>
		/// <param name="keyFileName">Name of the key file.</param>
		/// <param name="keyFilePassword">The key file password.</param>
		/// <param name="move">
		///     if set to <c>true</c> moves injected file (deletes the source file).
		/// </param>
		public virtual void Execute(
			string mainFileName, string[] libzFileNames,
			string keyFileName, string keyFilePassword,
			bool move)
		{
			var keyPair = MsilUtilities.LoadKeyPair(keyFileName, keyFilePassword);
			var assembly = MsilUtilities.LoadAssembly(mainFileName);
			var injectedFileNames = new List<string>();

			// TODO:MAK exclude?
			foreach (var libzFileName in FindFiles(libzFileNames))
			{
				if (libzFileName == null)
					throw ArgumentNull("libzFileName");
				if (!File.Exists(libzFileName))
					throw FileNotFound(libzFileName);
				if (!File.Exists(mainFileName))
					throw FileNotFound(mainFileName);

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
				MsilUtilities.SaveAssembly(assembly, mainFileName, keyPair);
				if (move)
					foreach (var fn in injectedFileNames)
						DeleteFile(fn);
			}
		}
	}
}