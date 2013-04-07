using System;
using System.IO;

namespace LibZ.Tool.Tasks
{
	public class MergeBootstrapTask: TaskBase
	{
		public void Execute(string exeFileName, string bootstrapFileName = null, bool move = true)
		{
			// TODO:MAK check if targetPlatform is set properly
			// TODO:MAK check signing

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

			if (ILMerging.ILMerge.Main(args) != 0)
				throw new InvalidOperationException("ILMerge failed");

			File.Move(exeFileName, tempFileName);
			File.Move(outputFileName, exeFileName);
			File.Delete(tempFileName);

			if (move)
			{
				DeleteFile(bootstrapFileName);
			}

		}
	}
}
