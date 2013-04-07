using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class SignAndFixCommand: ConsoleCommand
	{
		private string _keyFileName;
		private string _keyFilePassword;

		public SignAndFixCommand()
		{
			IsCommand("sign-and-fix", "Signs not signed assemblies and fixes all references to them");
			HasRequiredOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
			HasAdditionalArguments(null, "<assemblies to sign and/or fix>");
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new SignAndFixTask();
			task.Execute(_keyFileName, _keyFilePassword, remainingArguments);

			return 0;
		}
	}
}
