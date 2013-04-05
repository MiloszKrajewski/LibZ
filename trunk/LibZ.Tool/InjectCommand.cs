#region Header

// --------------------------------------------------------------------------------------
// LibZ.Tool.InjectCommand.cs
// --------------------------------------------------------------------------------------
// 
// 
//
// Copyright (c) 2013 Sepura Plc 
//
// Sepura Confidential
//
// Created: 4/5/2013 1:18:33 PM : SEPURA/krajewskim on SEPURA1051 
// 
// --------------------------------------------------------------------------------------

#endregion

using System;
using System.Globalization;
using System.IO;
using ManyConsole;
using Mono.Cecil;

namespace LibZ.Tool
{
	public class InjectCommand: ConsoleCommand
	{
		private string _exeFileName;
		private string _libzFileName;
		private bool _move;

		public InjectCommand()
		{
			IsCommand("inject", "Inject .libz file into .exe as resource");
			HasRequiredOption("l|libz=", ".libz file name", s => _libzFileName = s);
			HasRequiredOption("e|exe=", ".exe file name", s => _exeFileName = s);
			HasOption("move", "move files (remove when added)", _ => _move = true);
		}

		public override int Run(string[] remainingArguments)
		{
			var task = new InjectResourceTask();
			task.Execute(_libzFileName, _exeFileName, _move);
			return 0;
		}
	}
}