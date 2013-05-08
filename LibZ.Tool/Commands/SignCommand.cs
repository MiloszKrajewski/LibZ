using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	/// <summary>Command to sign assembly.</summary>
	public class SignCommand: ConsoleCommand
	{
		#region fields

		private string _keyFileName;
		private bool _force;
		private string _password;
		private readonly List<string> _include = new List<string>();
		private readonly List<string> _exclude = new List<string>();

		#endregion

		#region constructor

		/// <summary>Initializes a new instance of the <see cref="SignCommand"/> class.</summary>
		public SignCommand()
		{
			IsCommand("sign", "Signs assembly with strong name");
			HasRequiredOption("k|key=", "key file name", s => _keyFileName = s);
			HasRequiredOption("i|include=", "file name to include (wildcards allowed)", s => _include.Add(s));
			HasOption("force", "foces signing (signs assemblies which are already signed)", _ => _force = true);
			HasOption("p|password=", "password for password protected key file", s => _password = s);
			HasOption("e|exclude=", "file name to exclude (wildcards allowed, optional)", s => _exclude.Add(s));
		}

		#endregion

		#region public interface

		/// <summary>Runs the command.</summary>
		/// <param name="remainingArguments">The remaining arguments.</param>
		/// <returns>Return code.</returns>
		public override int Run(string[] remainingArguments)
		{
			var task = new SignAssembliesTask();
			task.Execute(_include.ToArray(), _exclude.ToArray(), _keyFileName, _password, _force);

			return 0;
		}

		#endregion
	}
}
