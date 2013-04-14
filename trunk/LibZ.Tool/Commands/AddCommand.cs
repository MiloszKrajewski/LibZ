using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class AddCommand : ConsoleCommand
	{
		private string _libzFileName;
		private string _codecName;
		private bool _move;
		private bool _overwrite;
		private readonly List<string> _exclude = new List<string>();

		public AddCommand()
		{
			IsCommand("add", "Adds assemblies to container");
			HasRequiredOption("l|libz=", ".libz file name", s => _libzFileName = s);
			HasOption("c|codec=", "codec name (optional)", s => _codecName = s);
			HasOption("e|exclude=", "file name to exclude (wildcards allowed, optional)", s => _exclude.Add(s));
			HasOption("overwrite", "overwrite resources (optional, default: false)", _ => _overwrite = true);
			HasOption("move", "move files (optional, default: false)", _ => _move = true);
			HasAdditionalArguments(null, "<dll file names...>");
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new AddLibraryTask();
			task.Execute(_libzFileName, remainingArguments, _exclude.ToArray(), _codecName, _move, _overwrite);
			return 0;
		}
	}
}