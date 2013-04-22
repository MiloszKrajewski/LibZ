using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class InstrumentCommand: ConsoleCommand
	{
		private string _mainFileName;
		private bool _allAsmZResources;

		public InstrumentCommand()
		{
			IsCommand("instrument", "Instruments assembly with initialization code");
			HasRequiredOption("a|assembly=", "assembly to be instrumented", s => _mainFileName = s);
			HasOption("asmz", "adds embedded assembly resolver", _ => _allAsmZResources = true);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new InstrumentTask();
			task.Execute(_mainFileName, _allAsmZResources, false, new string[0], new string[0]);

			return 0;
		}
	}
}
