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

				if (MsilUtilities.IsManaged(assembly))
				{
					Log.Warn("Assembly '{0}' is unmanaged ones, thus cannot be resigned", fileName);
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