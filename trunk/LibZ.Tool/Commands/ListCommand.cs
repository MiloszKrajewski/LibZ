using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ManyConsole;
using LibZ.Tool.Tasks;

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
			var task = new ListTask();
			task.Execute(_libzFileName);

			return 0;
		}
	}
}
