using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	/// <summary>Command to merge LibZ.Bootstrap.</summary>
	public class MergeBootstrapCommand: ConsoleCommand
	{
		#region fields

		/// <summary>The assembly to merged into.</summary>
		private string _mainFileName;

		/// <summary>The bootstrap assembly file name.</summary>
		private string _bootstrapFileName;

		/// <summary>The key file name</summary>
		private string _keyFileName;

		/// <summary>The key file password</summary>
		private string _keyFilePassword;

		/// <summary>Flag indicating the bootstrap assembly will be moved.</summary>
		private bool _move;

		#endregion

		#region constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="MergeBootstrapCommand"/> class.
		/// </summary>
		public MergeBootstrapCommand()
		{
			IsCommand("merge-bootstrap", "(obsolete) Merges LibZ.Bootstrap into main executable");
			HasRequiredOption("a|assembly=", "main file name (.exe or .dll)", s => _mainFileName = s);
			HasOption("b|bootstrap=", "LibZ.Bootstrap.dll path (optional)", s => _bootstrapFileName = s);
			HasOption("move", "deletes merged bootstrapper", _ => _move = true);
			HasOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
		}

		#endregion

		#region public interface

		/// <summary>Runs the command.</summary>
		/// <param name="remainingArguments">The remaining arguments.</param>
		/// <returns>Return code.</returns>
		public override int Run(string[] remainingArguments)
		{
			var task = new MergeBootstrapTask();
			task.Execute(_mainFileName, _bootstrapFileName, _move, _keyFileName, _keyFilePassword);

			return 0;
		}

		#endregion
	}
}
