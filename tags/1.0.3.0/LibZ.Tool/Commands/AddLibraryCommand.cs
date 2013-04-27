using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	/// <summary>
	/// Add dll to libz command.
	/// </summary>
	public class AddLibraryCommand: ConsoleCommand
	{
		#region fields

		private string _libzFileName;
		private string _codecName;
		private bool _move;
		private bool _overwrite;
		private readonly List<string> _include = new List<string>();
		private readonly List<string> _exclude = new List<string>();

		#endregion

		#region constructor

		/// <summary>Initializes a new instance of the <see cref="AddLibraryCommand"/> class.</summary>
		public AddLibraryCommand()
		{
			IsCommand("add", "Adds assemblies to container");
			HasRequiredOption("l|libz=", ".libz file name", s => _libzFileName = s);
			HasRequiredOption("i|include=", "assembly file name to include (wildcards allowed)", s => _include.Add(s));
			HasOption("c|codec=", "codec name (optional, default: deflate)", s => _codecName = s);
			HasOption("e|exclude=", "assembly file name to exclude (wildcards allowed, optional)", s => _exclude.Add(s));
			HasOption("overwrite", "overwrite resources (optional, default: false)", _ => _overwrite = true);
			HasOption("move", "move files (optional, default: false)", _ => _move = true);
		}

		#endregion

		#region public interface

		/// <summary>Runs the command.</summary>
		/// <param name="remainingArguments">The remaining arguments.</param>
		/// <returns>Return code.</returns>
		public override int Run(string[] remainingArguments)
		{
			var task = new AddLibraryTask();
			task.Execute(_libzFileName, _include.ToArray(), _exclude.ToArray(), _codecName, _move, _overwrite);
			return 0;
		}

		#endregion
	}
}
