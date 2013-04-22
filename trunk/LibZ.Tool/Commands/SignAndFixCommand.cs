using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class SignAndFixCommand: ConsoleCommand
	{
		private string _keyFileName;
		private string _keyFilePassword;
		private readonly List<string> _include = new List<string>();
		private readonly List<string> _exclude = new List<string>();

		public SignAndFixCommand()
		{
			IsCommand("sign-and-fix", "Signs not signed assemblies and fixes all references to them");
			HasRequiredOption("k|key=", "key file name", s => _keyFileName = s);
			HasRequiredOption("i|include=", "file name to include (wildcards allowed)", s => _include.Add(s));
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
			HasOption("e|exclude=", "file name to exclude (wildcards allowed, optional)", s => _exclude.Add(s));
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new SignAndFixAssembliesTask();
			task.Execute(_keyFileName, _keyFilePassword, _include.ToArray(), _exclude.ToArray());

			return 0;
		}
	}
}
