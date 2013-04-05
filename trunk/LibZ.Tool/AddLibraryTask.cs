using System;
using LibZ.Manager;

namespace LibZ.Tool
{
	public class AddLibraryTask: TaskBase
	{
		public void Execute(
			string libzFileName, string[] patterns, string codecName, bool move,
			Action<string> addingFile = null)
		{
			using (var container = new LibZContainer(libzFileName, true))
			{
				var count = 0;
				foreach (var fileName in FindFiles(patterns))
				{
					var resourceName = GetAssemblyName(fileName).ToLowerInvariant();
					Log.Info("Adding '{0}' from '{1}'", resourceName, fileName);
					container.Append(resourceName, fileName, codecName);
					if (move) DeleteFile(fileName);
					count++;
				}

				if (count == 0)
					Log.Warn("No files found: {0}", string.Join(", ", patterns));
			}
		}

	}
}
