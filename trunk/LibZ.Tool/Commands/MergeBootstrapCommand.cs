using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibZ.Tool.Tasks;
using ManyConsole;
using Mono.Cecil;

namespace LibZ.Tool.Commands
{
	public class MergeBootstrapCommand: ConsoleCommand
	{
		private string _exeFileName;
		private bool _move = false;
		private string _bootstrapFileName;

		public MergeBootstrapCommand()
		{
			IsCommand("merge-bootstrap", "Merges LibZ.Bootstrap into main executable");
			HasRequiredOption("e|exe=", ".exe file name", s => _exeFileName = s);
			HasOption("b|bootstrap=", "LibZ.Bootstrap.dll path (optional)", s => _bootstrapFileName = s);
			HasOption("move", "deletes merged bootstrapper", _ => _move = true);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new MergeBootstrapTask();
			task.Execute(_exeFileName, _bootstrapFileName, _move);

			return 0;
		}
	}
}
