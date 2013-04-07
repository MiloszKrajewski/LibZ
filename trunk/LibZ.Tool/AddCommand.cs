namespace LibZ.Tool
{
	public class AddCommand: ManyConsole.ConsoleCommand
	{
		private string _libzFileName;
		private string _codecName;
		private bool _move;

		public AddCommand()
		{
			IsCommand("add", "Add .dll to .libz");
			HasRequiredOption("l|libz=", ".libz file name", s => _libzFileName = s);
			HasOption("c|codec=", "codec name (optional)", s => _codecName = s);
			HasOption("move", "move files (remove when added)", _ => _move = true);
			HasAdditionalArguments(1, "<dll file name...>");
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new AddLibraryTask();
			task.Execute(_libzFileName, remainingArguments, _codecName, _move);
			return 0;
		}
	}
}
