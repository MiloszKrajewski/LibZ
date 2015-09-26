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