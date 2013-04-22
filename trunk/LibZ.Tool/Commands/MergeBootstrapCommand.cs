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
		private string _mainFileName;
		private bool _move;
		private string _bootstrapFileName;
		private string _keyFileName;
		private string _keyFilePassword;

		public MergeBootstrapCommand()
		{
			IsCommand("merge-bootstrap", "Merges LibZ.Bootstrap into main executable");
			HasRequiredOption("a|assembly=", "main file name (.exe or .dll)", s => _mainFileName = s);
			HasOption("b|bootstrap=", "LibZ.Bootstrap.dll path (optional)", s => _bootstrapFileName = s);
			HasOption("move", "deletes merged bootstrapper", _ => _move = true);
			HasOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new MergeBootstrapTask();
			task.Execute(_mainFileName, _bootstrapFileName, _move, _keyFileName, _keyFilePassword);

			return 0;
		}
	}
}
