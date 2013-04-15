using LibZ.Manager;

namespace LibZ.Tool.Tasks
{
	public class RebuildLibraryTask: TaskBase
	{
		public void Execute(string libzFileName)
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
