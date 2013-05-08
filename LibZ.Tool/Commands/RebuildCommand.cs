using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	/// <summary>Command to rebiuild libz container.</summary>
	public class RebuildCommand: ConsoleCommand
	{
		#region fields

		private string _libzFileName;

		#endregion

		#region constructor

		/// <summary>Initializes a new instance of the <see cref="RebuildCommand"/> class.</summary>
		public RebuildCommand()
		{
			IsCommand("rebuild", "Rebuilds .libz container");
			HasRequiredOption("l|libz=", "container file name", s => _libzFileName = s);
		}

		#endregion

		#region public interface

		/// <summary>Runs the command.</summary>
		/// <param name="remainingArguments">The remaining arguments.</param>
		/// <returns>Return code.</returns>
		public override int Run(string[] remainingArguments)
		{
			var task = new RebuildLibraryTask();
			task.Execute(_libzFileName);

			return 0;
		}

		#endregion
	}
}
