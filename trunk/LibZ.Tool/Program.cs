using System;
using ManyConsole;

namespace LibZ.Tool
{
	public class Program
	{
		public int Run(string[] args)
		{
			try
			{
				var commands = ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(GetType());
				return ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);
			}
			catch (Exception e)
			{
				Log.Error("{0}: {1}", e.GetType().Name, e.Message);
				Log.Debug(e.StackTrace);
				return 1;
			}
		}
	}
}