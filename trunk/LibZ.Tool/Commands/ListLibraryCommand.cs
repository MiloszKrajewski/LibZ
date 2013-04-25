using System;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	/// <summary>
	/// Lists content of libz file.
	/// </summary>
	public class ListLibraryCommand: ConsoleCommand
	{
		#region fields

		private string _libzFileName = String.Empty;

		#endregion

		#region constructor

		/// <summary>Initializes a new instance of the <see cref="ListLibraryCommand"/> class.</summary>
		public ListLibraryCommand()
		{
			IsCommand("list", "Lists the content of .libz container");
			HasRequiredOption("l|libz=", "Container file name", s => _libzFileName = s);
		}

		#endregion

		#region public interface

		/// <summary>Runs the command.</summary>
		/// <param name="remainingArguments">The remaining arguments.</param>
		/// <returns>Return code.</returns>
		public override int Run(string[] remainingArguments)
		{
			var task = new ListLibraryContentTask();
			task.Execute(_libzFileName);

			return 0;
		}

		#endregion
	}
}
