using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class InjectCommand : ConsoleCommand
	{
		private string _exeFileName;
		private string _libzFileName;
		private bool _move;
		private string _keyFileName;
		private string _keyFilePassword;

		public InjectCommand()
		{
			IsCommand("inject", "Injects .libz file into .exe as resource");
			HasRequiredOption("l|libz=", ".libz file name", s => _libzFileName = s);
			HasRequiredOption("e|exe=", ".exe file name", s => _exeFileName = s);
			HasOption("move", "move files (remove when added)", _ => _move = true);
			HasOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new InjectResourceTask();
			task.Execute(_libzFileName, _exeFileName, _keyFileName, _keyFilePassword, _move);
			return 0;
		}
	}
}