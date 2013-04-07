using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	public class SignCommand: ConsoleCommand
	{
		private string _keyFileName;
		private bool _force;

		public SignCommand()
		{
			IsCommand("sign", "Signs assembly with strong name");
			HasRequiredOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("force", "foces signing (signs assemblies which are already signed)", _ => _force = true);
			HasAdditionalArguments(1, "<assemblies to sign>");
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new SignTask();
			task.Execute(_keyFileName, _force, remainingArguments);

			return 0;
		}
	}
}
