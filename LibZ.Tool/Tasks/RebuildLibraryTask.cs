using LibZ.Manager;

namespace LibZ.Tool.Tasks
{
	/// <summary>
	/// Rebuilds the .libz container.
	/// </summary>
	public class RebuildLibraryTask: TaskBase
	{
		/// <summary>Executes the task.</summary>
		/// <param name="libzFileName">Name of the libz file.</param>
		public virtual void Execute(string libzFileName)
		{
			Log.Info("Opening '{0}'", libzFileName);
			var tempFileName = libzFileName + ".temp";
			using (var container = new LibZContainer(libzFileName))
			{
				Log.Info("Saving '{0}'", libzFileName);
				container.SaveAs(tempFileName);
			}
			RenameFile(tempFileName, libzFileName);
		}
	}
}
