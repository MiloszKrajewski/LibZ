using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LibZ.Manager;

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
			using (var container = new LibZContainer(_libzFileName, true))
			{
				foreach (var fileName in FindFiles(remainingArguments))
				{
					var resourceName = GetAssemblyName(fileName).ToLower(CultureInfo.InvariantCulture);
					Console.WriteLine("Adding '{0}' as '{1}'", fileName, resourceName);
					container.Append(resourceName, fileName, _codecName);
					if (_move) DeleteFile(fileName);
				}
			}

			return 0;
		}

		private string GetAssemblyName(string fileName)
		{
			var assembly = Mono.Cecil.AssemblyDefinition.ReadAssembly(fileName);
			return assembly.Name.FullName;
		}

		private static void DeleteFile(string fileName)
		{
			try
			{
				File.Delete(fileName);
			}
			// ReSharper disable EmptyGeneralCatchClause
			catch
			{
				Console.WriteLine("File '{0}' could not be deleted", fileName);
			}
			// ReSharper restore EmptyGeneralCatchClause
		}

		private static IEnumerable<string> FindFiles(IEnumerable<string> patterns)
		{
			return patterns.SelectMany(FindFiles);
		}

		private static IEnumerable<string> FindFiles(string pattern)
		{
			if (!Path.IsPathRooted(pattern)) pattern = ".\\" + pattern;
			var directoryName = Path.GetDirectoryName(pattern) ?? ".";
			var searchPattern = Path.GetFileName(pattern) ?? "*.dll";
			return Directory.GetFiles(directoryName, searchPattern);
		}
	}
}
