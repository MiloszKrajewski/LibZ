using System;
using System.IO;
using ILMerging;

namespace LibZ.Tool.Tasks
{
	public class MergeBootstrapTask: TaskBase
	{
		public void Execute(
			string exeFileName, string bootstrapFileName = null, bool move = true,
			string keyFileName = null, string keyFilePassword = null)
		{
			// TODO:MAK check if targetPlatform is set properly

			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			if (string.IsNullOrWhiteSpace(bootstrapFileName))
				bootstrapFileName = Path.Combine(Path.GetDirectoryName(exeFileName) ?? ".", "LibZ.Bootstrap.dll");
			var outputFolderPath = Path.Combine(
				Path.GetTempPath(),
				Guid.NewGuid().ToString("N"));
			var outputFileName = Path.Combine(
				outputFolderPath, Path.GetFileName(exeFileName));

			Log.Info("Merging '{0}' into '{1}'", bootstrapFileName, exeFileName);

			var args = new[] {
				string.Format("/out:{0}", outputFileName),
				exeFileName,
				bootstrapFileName
			};

			try
			{
				Directory.CreateDirectory(outputFolderPath);

				if (ILMerge.Main(args) != 0)
					throw new InvalidOperationException("ILMerge failed");

				using (var inputFile = File.OpenRead(outputFileName))
				using (var outputFile = File.Create(exeFileName))
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
				Log.Info("Resigning '{0}'", exeFileName);
				SaveAssembly(LoadAssembly(exeFileName), exeFileName, keyPair);
			}
		}
	}
}
