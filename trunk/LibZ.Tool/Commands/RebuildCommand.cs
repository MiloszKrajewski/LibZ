using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class RebuildCommand: ConsoleCommand
	{
		private string _libzFileName;

		public RebuildCommand()
		{
			IsCommand("rebuild", "Rebuilds .libz container");
			HasRequiredOption("l|libz=", "container file name", s => _libzFileName = s);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new RebuildLibraryTask();
			task.Execute(_libzFileName);

			return 0;
		}
	}
}
