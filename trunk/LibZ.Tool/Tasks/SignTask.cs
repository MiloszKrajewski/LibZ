using System;
using System.IO;
using System.Reflection;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class SignTask: TaskBase
	{
		internal void Execute(string keyFileName, bool force, string password, string[] patterns)
		{
			var keyPair = LoadKeyPair(keyFileName, password);

			foreach (var fileName in FindFiles(patterns))
			{
				var assembly = LoadAssembly(fileName);

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
