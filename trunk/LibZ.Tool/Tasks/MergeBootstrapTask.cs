using System;
using System.IO;
using ILMerging;

namespace LibZ.Tool.Tasks
{
	public class MergeBootstrapTask: TaskBase
	{
		public void Execute(
			string mainFileName, string bootstrapFileName = null, bool move = true,
			string keyFileName = null, string keyFilePassword = null)
		{
			// TODO:MAK check if targetPlatform is set properly

			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			if (string.IsNullOrWhiteSpace(bootstrapFileName))
				bootstrapFileName = Path.Combine(Path.GetDirectoryName(mainFileName) ?? ".", "LibZ.Bootstrap.dll");
			var outputFolderPath = Path.Combine(
				Path.GetTempPath(),
				Guid.NewGuid().ToString("N"));
			var outputFileName = Path.Combine(
				outputFolderPath, Path.GetFileName(mainFileName) ?? "temp.exe");

			Log.Info("Merging '{0}' into '{1}'", bootstrapFileName, mainFileName);

			try
			{
				Directory.CreateDirectory(outputFolderPath);

				var engine = new ILMerge
				{
					OutputFile = outputFileName,
					Internalize = true,
				};
				engine.SetInputAssemblies(new[] { mainFileName, bootstrapFileName });
				engine.Merge();

				using (var inputFile = File.OpenRead(outputFileName))
				using (var outputFile = File.Create(mainFileName))
				{
					inputFile.CopyTo(outputFile);
				}
			}
			finally
			{
				Directory.Delete(outputFolderPath, true);
			}

			if (move)
			{
				DeleteFile(bootstrapFileName);
			}

			if (keyFileName != null)
			{
				Log.Info("Resigning '{0}'", mainFileName);
				SaveAssembly(LoadAssembly(mainFileName), mainFileName, keyPair);
			}
		}
	}
}
