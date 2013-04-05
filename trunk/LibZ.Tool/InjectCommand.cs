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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using System.IO;
using System.Globalization;

namespace LibZ.Tool
{
	class InjectCommand: ManyConsole.ConsoleCommand
	{
		private string _exeFileName;
		private string _libzFileName;

		public InjectCommand()
		{
			IsCommand("inject", "Inject .libz file into .exe as resource");
			HasRequiredOption(
				"l|libz=", ".libz file name", s => _libzFileName = s);
			HasRequiredOption(
				"e|exe=", ".exe file name", s => _exeFileName = s);
			HasOption(
				"move", "move files (")
		}

		public override int Run(string[] remainingArguments)
		{
			var resourceName = "LibZ." + Path.GetFileName(_libzFileName).ToLower(CultureInfo.InvariantCulture);
			var assembly = AssemblyDefinition.ReadAssembly(_exeFileName);
			var resource = new EmbeddedResource(
				resourceName, 
				ManifestResourceAttributes.Public, 
				File.ReadAllBytes(_libzFileName));
			assembly.MainModule.Resources.Add(resource);
			assembly.Write(_exeFileName);

			return 0;
		}
	}
}
