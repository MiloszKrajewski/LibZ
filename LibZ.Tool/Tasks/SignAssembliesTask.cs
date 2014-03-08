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

using LibZ.Msil;
using NLog;

namespace LibZ.Tool.Tasks
{
	/// <summary>
	///     Task to sign assemblies.
	/// </summary>
	public class SignAssembliesTask: TaskBase
	{
		#region consts

		/// <summary>Logger for this class.</summary>
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		#endregion

		/// <summary>Executes the task.</summary>
		/// <param name="includePatterns">The include patterns.</param>
		/// <param name="excludePatterns">The exclude patterns.</param>
		/// <param name="keyFileName">Name of the key file.</param>
		/// <param name="password">The password.</param>
		/// <param name="force">
		///     if set to <c>true</c> assemblies will be signed even if they are already signed.
		/// </param>
		public virtual void Execute(
			string[] includePatterns, string[] excludePatterns,
			string keyFileName, string password,
			bool force)
		{
			var keyPair = MsilUtilities.LoadKeyPair(keyFileName, password);

			foreach (var fileName in FindFiles(includePatterns, excludePatterns))
			{
				var assembly = MsilUtilities.LoadAssembly(fileName);

				if (!MsilUtilities.IsManaged(assembly))
				{
					Log.Warn("Assembly '{0}' is unmanaged, thus cannot be resigned", fileName);
					continue;
				}

				if (MsilUtilities.IsSigned(assembly))
				{
					if (force)
					{
						Log.Warn("Assembly '{0}' was previously signed, but it going to be resigned with new key", fileName);
					}
					else
					{
						Log.Debug("Assembly '{0}' is already signed so it does not need resigning", fileName);
						continue;
					}
				}

				MsilUtilities.SaveAssembly(assembly, fileName, keyPair);
			}
		}
	}
}