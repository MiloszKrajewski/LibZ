using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	/// <summary>'Sign assemblies and fix references' commands.</summary>
	public class SignAndFixCommand: ConsoleCommand
	{
		#region fields

		private string _keyFileName;
		private string _keyFilePassword;
		private readonly List<string> _include = new List<string>();
		private readonly List<string> _exclude = new List<string>();

		#endregion

		#region constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="SignAndFixCommand"/> class.
		/// </summary>
		public SignAndFixCommand()
		{
			IsCommand("sign-and-fix", "Signs not signed assemblies and fixes all references to them");
			HasRequiredOption("k|key=", "key file name", s => _keyFileName = s);
			HasRequiredOption("i|include=", "file name to include (wildcards allowed)", s => _include.Add(s));
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
			HasOption("e|exclude=", "file name to exclude (wildcards allowed, optional)", s => _exclude.Add(s));
		}

		#endregion

		#region public interface

		/// <summary>Runs the command.</summary>
		/// <param name="remainingArguments">The remaining arguments.</param>
		/// <returns>Return code.</returns>
		public override int Run(string[] remainingArguments)
		{
			var task = new SignAndFixAssembliesTask();
			task.Execute(_keyFileName, _keyFilePassword, _include.ToArray(), _exclude.ToArray());

			return 0;
		}

		#endregion
	}
}
