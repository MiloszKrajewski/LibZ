using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace LibZ.Tool
{
	public class TaskBase
	{
		#region consts

		private static readonly MD5 MD5Provider = System.Security.Cryptography.MD5.Create();

		#endregion

		#region file utilities

		protected static void DeleteFile(string fileName)
		{
			try
			{
				Log.Debug("Deleting '{0}'", fileName);
				File.Delete(fileName);
			}
			// ReSharper disable EmptyGeneralCatchClause
			catch
			{
				Log.Warn("File '{0}' could not be deleted", fileName);
			}
			// ReSharper restore EmptyGeneralCatchClause
		}

		protected static IEnumerable<string> FindFiles(IEnumerable<string> patterns)
		{
			return patterns.SelectMany(FindFiles);
		}

		protected static IEnumerable<string> FindFiles(string pattern)
		{
			if (!Path.IsPathRooted(pattern)) pattern = ".\\" + pattern;
			var directoryName = Path.GetDirectoryName(pattern) ?? ".";
			var searchPattern = Path.GetFileName(pattern) ?? "*.dll";
			return Directory.GetFiles(directoryName, searchPattern);
		}

		#endregion

		#region reflection utilities

		protected static string GetAssemblyName(string fileName)
		{
			var assembly = AssemblyDefinition.ReadAssembly(fileName);
			return assembly.Name.FullName;
		}

		#endregion

		#region exceptions

		protected static Exception ArgumentNull(string argumentName)
		{
			return new ArgumentNullException(argumentName);
		}

		protected static Exception FileNotFound(string fileName)
		{
			return new FileNotFoundException(string.Format("File '{0}' could not be found", fileName));
		}

		#endregion

		#region utilities

		protected static string MD5(string text)
		{
			return new Guid(MD5Provider.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()))).ToString("N").ToLowerInvariant();
		}

		#endregion
	}
}
