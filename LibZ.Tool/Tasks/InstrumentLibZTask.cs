#region License

/*
 * Copyright (c) 2013-2014, Milosz Krajewski
 * 
 * Microsoft Public License (Ms-PL)
 * This license governs use of the accompanying software. 
 * If you use the software, you accept this license. 
 * If you do not accept the license, do not use the software.
 * 
 * 1. Definitions
 * The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same 
 * meaning here as under U.S. copyright law.
 * A "contribution" is the original software, or any additions or changes to the software.
 * A "contributor" is any person that distributes its contribution under this license.
 * "Licensed patents" are a contributor's patent claims that read directly on its contribution.
 * 
 * 2. Grant of Rights
 * (A) Copyright Grant- Subject to the terms of this license, including the license conditions 
 * and limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
 * royalty-free copyright license to reproduce its contribution, prepare derivative works of 
 * its contribution, and distribute its contribution or any derivative works that you create.
 * (B) Patent Grant- Subject to the terms of this license, including the license conditions and 
 * limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
 * royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, 
 * import, and/or otherwise dispose of its contribution in the software or derivative works of 
 * the contribution in the software.
 * 
 * 3. Conditions and Limitations
 * (A) No Trademark License- This license does not grant you rights to use any contributors' name, 
 * logo, or trademarks.
 * (B) If you bring a patent claim against any contributor over patents that you claim are infringed 
 * by the software, your patent license from such contributor to the software ends automatically.
 * (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, 
 * and attribution notices that are present in the software.
 * (D) If you distribute any portion of the software in source code form, you may do so only under this 
 * license by including a complete copy of this license with your distribution. If you distribute 
 * any portion of the software in compiled or object code form, you may only do so under a license 
 * that complies with this license.
 * (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express
 * warranties, guarantees or conditions. You may have additional consumer rights under your local 
 * laws which this license cannot change. To the extent permitted under your local laws, the 
 * contributors exclude the implied warranties of merchantability, fitness for a particular 
 * purpose and non-infringement.
 */

#endregion

using System;
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
			ValidateLibZInstrumentation(targetAssembly);
			
			var keyPair = MsilUtilities.LoadKeyPair(keyFileName, keyFilePassword);
			var requiresAsmZResolver = false;

			var bootstrapAssembly =
				FindBootstrapAssembly(targetAssembly, mainFileName);

			if (bootstrapAssembly == null)
			{
				var version = MsilUtilities.GetFrameworkVersion(targetAssembly);

				var bootstrapAssemblyImage = InstrumentHelper.GetBootstrapAssemblyImage(version);
				bootstrapAssembly = MsilUtilities.LoadAssembly(bootstrapAssemblyImage);

				if (bootstrapAssembly == null)
					throw new ArgumentException("LibZ.Bootstrap has not been found");

				Log.Info("Using built in LibZResolver");

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