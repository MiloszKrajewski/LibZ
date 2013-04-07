using System;
using System.IO;

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
			var outputFileName = exeFileName + ".ilmerge" + Path.GetExtension(exeFileName);
			var tempFileName = exeFileName + ".temp";

			Log.Info("Merging '{0}' into '{1}'", bootstrapFileName, exeFileName);

			var args = new[] {
				string.Format("/out:{0}", outputFileName),
				exeFileName,
				bootstrapFileName
			};

			try
			{
				if (ILMerging.ILMerge.Main(args) != 0)
					throw new InvalidOperationException("ILMerge failed");
				File.Delete(exeFileName);
				File.Move(outputFileName, exeFileName);
			}
			catch
			{
				DeleteFile(outputFileName);
				throw;
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
