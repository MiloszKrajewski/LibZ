using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class InstrumentAsmZCommand: ConsoleCommand
	{
		private string _mainFileName;
		private string _keyFileName;
		private string _keyFilePassword;

		public InstrumentAsmZCommand()
		{
			IsCommand("instrument-asmz", "Instruments assembly with initialization code");
			HasRequiredOption("a|assembly=", "assembly to be instrumented", s => _mainFileName = s);
			HasOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new InstrumentAsmZTask();
			task.Execute(_mainFileName, _keyFileName, _keyFilePassword);

			return 0;
		}
	}
}
