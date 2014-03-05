using System;
using System.IO;
using ILMerging;
using LibZ.Msil;

namespace LibZ.Tool.Tasks
{
	/// <summary>
	/// Task to merge LibZ.Bootstrap using ILMerge.
	/// NOTE: this method is obsolete. It seems to work better when using <see cref="InjectDllTask"/>.
	/// </summary>
	public class MergeBootstrapTask: TaskBase
	{
		/// <summary>Executes the task.</summary>
		/// <param name="mainFileName">Name of the main file.</param>
		/// <param name="bootstrapFileName">Name of the bootstrap file.</param>
		/// <param name="move">if set to <c>true</c> moves the LibZ.Bootstrap (deletes source file).</param>
		/// <param name="keyFileName">Name of the key file.</param>
		/// <param name="keyFilePassword">The key file password.</param>
		public virtual void Execute(
			string mainFileName, string bootstrapFileName = null, bool move = true,
			string keyFileName = null, string keyFilePassword = null)
		{
			// TODO:MAK check if targetPlatform is set properly

			var keyPair = MsilUtilities.LoadKeyPair(keyFileName, keyFilePassword);

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

				var engine = new ILMerge {
					OutputFile = outputFileName,
					Internalize = true,
				};
				engine.SetInputAssemblies(new[] {mainFileName, bootstrapFileName});
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
				MsilUtilities.SaveAssembly(
					MsilUtilities.LoadAssembly(mainFileName), mainFileName, keyPair);
			}
		}
	}
}
