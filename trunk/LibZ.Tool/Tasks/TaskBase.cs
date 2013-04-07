using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
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

		protected static string GetAssemblyName(AssemblyDefinition assembly)
		{
			return assembly.Name.FullName;
		}

		protected static bool IsManaged(AssemblyDefinition assembly)
		{
			return assembly.Modules.All(m => (m.Attributes & ModuleAttributes.ILOnly) != 0);
		}

		protected static AssemblyArchitecture GetArchitecture(AssemblyDefinition assembly)
		{
			if (assembly.Modules.Any(m => m.Architecture == TargetArchitecture.AMD64))
				return AssemblyArchitecture.X64;
			// experimental: if there is a unmanaged code and it is not X64 it has to be X86
			if (assembly.Modules.Any(m => (m.Attributes & ModuleAttributes.ILOnly) == 0))
				return AssemblyArchitecture.X86;
			if (assembly.Modules.Any(m => (m.Attributes & ModuleAttributes.Required32Bit) != 0))
				return AssemblyArchitecture.X86;
			return AssemblyArchitecture.AnyCPU;
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
			return
				new Guid(MD5Provider.ComputeHash(Encoding.UTF8.GetBytes(text.ToLowerInvariant()))).ToString("N").ToLowerInvariant();
		}

		#endregion
	}
}