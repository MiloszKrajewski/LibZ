namespace LibZ.Tool.Tasks
{
	/// <summary>
	/// Task to sign assemblies.
	/// </summary>
	public class SignAssembliesTask: TaskBase
	{
		/// <summary>Executes the task.</summary>
		/// <param name="includePatterns">The include patterns.</param>
		/// <param name="excludePatterns">The exclude patterns.</param>
		/// <param name="keyFileName">Name of the key file.</param>
		/// <param name="password">The password.</param>
		/// <param name="force">if set to <c>true</c> assemblies will be signed even if they are already signed.</param>
		public virtual void Execute(
			string[] includePatterns, string[] excludePatterns,
			string keyFileName, string password,
			bool force)
		{
			var keyPair = LoadKeyPair(keyFileName, password);

			foreach (var fileName in FindFiles(includePatterns, excludePatterns))
			{
				var assembly = LoadAssembly(fileName);

				if (IsManaged(assembly))
				{
					Log.Warn("Assembly '{0}' is unmanaged ones, thus cannot be resigned", fileName);
					continue;
				}

				if (IsSigned(assembly))
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

				SaveAssembly(assembly, fileName, keyPair);
			}
		}
	}
}
