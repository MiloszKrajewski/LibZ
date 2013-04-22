using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class InjectDllCommand : ConsoleCommand
	{
		private string _mainFileName;
		private string _keyFileName;
		private string _keyFilePassword;
		private readonly List<string> _include = new List<string>();
		private readonly List<string> _exclude = new List<string>();
		private bool _overwrite;
		private bool _move;
  
		public InjectDllCommand()
		{
			IsCommand("inject-dll", "Injects .dll file into assembly as resource");
			HasRequiredOption("a|assembly=", "main file name (.exe or .dll)", s => _mainFileName = s);
			HasRequiredOption("i|include=", "assembly file name to include (wildcards allowed)", s => _include.Add(s));
			HasOption("e|exclude=", "assembly file name to exclude (wildcards allowed, optional)", s => _exclude.Add(s));
			HasOption("overwrite", "Overwrites existing resources", _ => _overwrite = true);
			HasOption("move", "move files (remove when added)", _ => _move = true);
			HasOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new InjectDllTask();
			task.Execute(_mainFileName, _include, _exclude, _keyFileName, _keyFilePassword, _overwrite, _move);
			return 0;
		}
	}
}