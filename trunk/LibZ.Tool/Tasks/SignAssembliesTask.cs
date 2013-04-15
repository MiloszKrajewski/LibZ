namespace LibZ.Tool.Tasks
{
	public class SignAssembliesTask: TaskBase
	{
		internal void Execute(
			string keyFileName, bool force, string password, 
			string[] patterns, string[] excludePatterns)
		{
			var keyPair = LoadKeyPair(keyFileName, password);

			foreach (var fileName in FindFiles(patterns, excludePatterns))
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
