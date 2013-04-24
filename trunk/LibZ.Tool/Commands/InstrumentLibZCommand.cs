using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class InstrumentLibZCommand: ConsoleCommand
	{
		private string _mainFileName;
		private bool _allLibZResources;
		private string _keyFileName;
		private string _keyFilePassword;
		private readonly List<string> _libzFiles = new List<string>();
		private readonly List<string> _libzPatterns = new List<string>();

		public InstrumentLibZCommand()
		{
			IsCommand("instrument", "Instruments assembly with initialization code");
			HasRequiredOption("a|assembly=", "assembly to be instrumented", s => _mainFileName = s);
			HasOption("libz-resources", "registers embedded LibZ container on startup", _ => _allLibZResources = true);
			HasOption("libz-file", "registers file on startup", s => _libzFiles.Add(s));
			HasOption("libz-pattern", "registers multiple files on startup (wildcards)", s => _libzPatterns.Add(s));
			HasOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new InstrumentLibZTask();
			task.Execute(
				_mainFileName,
				_allLibZResources, _libzFiles, _libzPatterns,
				_keyFileName, _keyFilePassword);

			return 0;
		}
	}
}
