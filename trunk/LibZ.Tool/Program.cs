using System;
using ManyConsole;

namespace LibZ.Tool
{
	public class Program
	{
		public int Run(string[] args)
		{
			var commands = ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(GetType());
			return ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);
		}
	}
}
