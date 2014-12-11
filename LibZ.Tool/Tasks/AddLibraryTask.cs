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
using System.Reflection;
using LibZ.Manager;
using LibZ.Msil;
using NLog;

namespace LibZ.Tool.Tasks
{
	/// <summary>
	///     Adds assemblies to LibZ library.
	/// </summary>
	public class AddLibraryTask: TaskBase
	{
		#region consts

		/// <summary>Logger for this class.</summary>
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		#endregion

		/// <summary>Executes the task.</summary>
		/// <param name="libzFileName">Name of the libz file.</param>
		/// <param name="includePatterns">The include patterns.</param>
		/// <param name="excludePatterns">The exclude patterns.</param>
		/// <param name="codecName">Name of the codec.</param>
		/// <param name="safeLoad">if set to <c>true</c> 'safe load' if requested.</param>
		/// <param name="move">if set to <c>true</c> moves files (deletes soure files).</param>
		/// <param name="overwrite">if set to <c>true</c> overwrites existing resources.</param>
		public virtual void Execute(
			string libzFileName,
			string[] includePatterns, string[] excludePatterns,
			string codecName, bool safeLoad, bool move, bool overwrite)
		{
			var injectedFileNames = new List<string>();
			if (string.IsNullOrEmpty(codecName))
				codecName = "deflate";

			using (var container = new LibZContainer(libzFileName, true))
			{
				foreach (var fileName in FindFiles(includePatterns, excludePatterns))
				{
					var assembly = MsilUtilities.LoadAssembly(fileName);
					if (assembly == null)
					{
						Log.Warn("Assembly from '{0}' could not be loaded", fileName);
						continue;
					}
					var assemblyName = assembly.Name;
					var managed = MsilUtilities.IsManaged(assembly);
					var architecture = MsilUtilities.GetArchitecture(assembly);
					var portable = MsilUtilities.IsPortable(assembly);

					var assemblyInfo = new AssemblyInfo {
						AssemblyName = new AssemblyName(assemblyName.FullName),
						AnyCPU = architecture == AssemblyArchitecture.AnyCPU,
						X64 = architecture == AssemblyArchitecture.X64,
						SafeLoad = safeLoad,
						Unmanaged = !managed,
						Portable = portable,
						Bytes = File.ReadAllBytes(fileName),
					};

					Log.Info("Appending '{0}' from '{1}'", assemblyInfo.AssemblyName, fileName);

					container.Append(
						assemblyInfo,
						new AppendOptions { CodecName = codecName, Overwrite = overwrite, });

					injectedFileNames.Add(fileName);
				}

				if (injectedFileNames.Count <= 0)
				{
					Log.Warn("No files injected: {0}", string.Join(", ", includePatterns));
				}
				else
				{
					if (move)
						foreach (var fn in injectedFileNames)
							DeleteFile(fn);
				}
			}
		}
	}
}