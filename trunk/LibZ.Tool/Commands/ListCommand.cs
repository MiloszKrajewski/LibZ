using System;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class ListCommand: ConsoleCommand
	{
		private string _libzFileName = String.Empty;

		public ListCommand()
		{
			IsCommand("list", "Lists the content of .libz container");
			HasRequiredOption("l|libz=", "Container file name", s => _libzFileName = s);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new ListLibraryContentTask();
			task.Execute(_libzFileName);

			return 0;
		}
	}
}
