using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using LibZ.Msil;
using LibZ.Tool.InjectIL;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	/// <summary>
	/// Base class for all tasks.
	/// Contains some utilities potentially used by all of them.
	/// </summary>
	public class TaskBase
	{
		#region consts

		/// <summary>Hash calculator.</summary>
		private static readonly MD5 MD5Service = MD5.Create();

		/// <summary>The regular expression to parse resource name</summary>
		protected static readonly Regex ResourceNameRx = new Regex(
			@"asmz://(?<guid>[0-9a-fA-F]{32})/(?<size>[0-9]+)(/(?<flags>[a-zA-Z0-9]*))?",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		/// <summary>The regular expression to detect portable assemblies.</summary>
		protected static readonly Regex PortableAssemblyRx = new Regex(
			@"(^|,)\s*Retargetable\s*\=\s*Yes\s*(,|$)",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		#endregion

		#region static fields

		/// <summary>The wildcard cache.</summary>
		private static readonly Dictionary<string, Regex> WildcardCacheRx = new Dictionary<string, Regex>();

		#endregion

		#region file utilities

		/// <summary>Renames the file.</summary>
		/// <param name="sourceFileName">Name of the source file.</param>
		/// <param name="targetFileName">Name of the target file.</param>
		protected static void RenameFile(string sourceFileName, string targetFileName)
		{
			try
			{
				var tempFileName = String.Format("{0}.{1:N}", targetFileName, Guid.NewGuid());
				File.Move(targetFileName, tempFileName);
				File.Move(sourceFileName, targetFileName);
				File.Delete(tempFileName);
			}
			catch
			{
				Log.Error("Renaming to '{0}' failed", targetFileName);
				throw;
			}
		}

		/// <summary>Deletes the file.</summary>
		/// <param name="fileName">Name of the file.</param>
		protected static void DeleteFile(string fileName)
		{
			if (!File.Exists(fileName)) return;

			try
			{
				File.Delete(fileName);
			}
			// ReSharper disable EmptyGeneralCatchClause
			catch
			{
				Log.Warn("File '{0}' could not be deleted", fileName);
			}
			// ReSharper restore EmptyGeneralCatchClause
		}

		/// <summary>Finds the files.</summary>
		/// <param name="includePatterns">The include patterns.</param>
		/// <param name="excludePatterns">The exclude patterns.</param>
		/// <returns>Collection of file names.</returns>
		protected static IEnumerable<string> FindFiles(
			IEnumerable<string> includePatterns,
			IEnumerable<string> excludePatterns = null)
		{
			if (excludePatterns == null) excludePatterns = new string[0];
			var result = includePatterns.SelectMany(p => FindFiles(p, excludePatterns))
				.Distinct()
				.ToList();
			result.Sort((l, r) => string.Compare(l, r, StringComparison.InvariantCultureIgnoreCase));
			return result;
		}

		/// <summary>Finds the files.</summary>
		/// <param name="pattern">The pattern.</param>
		/// <param name="excludePatterns">The exclude patterns.</param>
		/// <returns>Collection of file names.</returns>
		private static IEnumerable<string> FindFiles(string pattern, IEnumerable<string> excludePatterns)
		{
			if (!Path.IsPathRooted(pattern)) pattern = ".\\" + pattern;
			var directoryName = Path.GetDirectoryName(pattern) ?? ".";
			var searchPattern = Path.GetFileName(pattern) ?? "*.dll";

			return Directory.GetFiles(directoryName, searchPattern)
				.Where(fn => !excludePatterns.Any(
					ep => WildcardToRegex(ep).IsMatch(Path.GetFileName(fn) ?? String.Empty)));
		}

		/// <summary>Converts wildcard to regex.</summary>
		/// <param name="pattern">The pattern.</param>
		/// <returns>Regex.</returns>
		private static Regex WildcardToRegex(string pattern)
		{
			Regex rx;
			if (!WildcardCacheRx.TryGetValue(pattern, out rx))
			{
				var p = String.Format("^{0}$", Regex.Escape(pattern).Replace("\\*", ".*").Replace("\\?", "."));
				WildcardCacheRx[pattern] = rx = new Regex(p, RegexOptions.IgnoreCase);
			}
			return rx;
		}

		#endregion

		#region exceptions

		/// <summary>Returns ArgumentNullException.</summary>
		/// <param name="argumentName">Name of the argument.</param>
		/// <returns>ArgumentNullException</returns>
		protected static ArgumentNullException ArgumentNull(string argumentName)
		{
			return new ArgumentNullException(argumentName);
		}

		/// <summary>Returns FileNotFoundException.</summary>
		/// <param name="fileName">Name of the file.</param>
		/// <returns>FileNotFoundException</returns>
		protected static FileNotFoundException FileNotFound(string fileName)
		{
			return new FileNotFoundException(String.Format("File '{0}' could not be found", fileName));
		}

		#endregion

		#region utilities

		/// <summary>Returns a hash of given resource.</summary>
		/// <param name="resource">The resource.</param>
		/// <returns>Hash already in resource name.</returns>
		protected static Guid? Hash(Resource resource)
		{
			var m = ResourceNameRx.Match(resource.Name);
			if (!m.Success) return null;
			return new Guid(m.Groups["guid"].Value);
		}

		/// <summary>Hashes the specified text.</summary>
		/// <param name="text">The text.</param>
		/// <returns>Hash of given text.</returns>
		protected static Guid Hash(string text)
		{
			return new Guid(
				MD5Service.ComputeHash(
					Encoding.UTF8.GetBytes(text.ToLowerInvariant())));
		}

		/// <summary>Hashes the specified text.</summary>
		/// <param name="text">The text.</param>
		/// <returns>String representation of the hash.</returns>
		protected static string HashString(string text)
		{
			return Hash(text).ToString("N").ToLowerInvariant();
		}

		#endregion

		#region assembly manipulation

		/// <summary>Injects the DLL.</summary>
		/// <param name="targetAssembly">The target assembly.</param>
		/// <param name="sourceAssembly">The source assembly.</param>
		/// <param name="sourceAssemblyBytes">The source assembly bytes.</param>
		/// <param name="overwrite">if set to <c>true</c> overwrites existing resource.</param>
		/// <returns><c>true</c> if assembly has been injected.</returns>
		protected static bool InjectDll(
			AssemblyDefinition targetAssembly,
			AssemblyDefinition sourceAssembly, byte[] sourceAssemblyBytes,
			bool overwrite)
		{
			var flags = String.Empty;
			if (!MsilUtilities.IsManaged(sourceAssembly)) flags += "u";
			if (MsilUtilities.IsPortable(sourceAssembly)) flags += "p";

			var input = sourceAssemblyBytes;
			var output = DefaultCodecs.DeflateEncoder(input);

			if (output.Length < input.Length)
			{
				flags += "z";
			}
			else
			{
				output = input;
			}

			var architecture = MsilUtilities.GetArchitecture(sourceAssembly);
			var architecturePrefix =
				architecture == AssemblyArchitecture.X64 ? "x64:" :
					architecture == AssemblyArchitecture.X86 ? "x86:" :
						string.Empty;
			var guid = Hash(architecturePrefix + sourceAssembly.FullName);

			var resourceName = String.Format(
				"asmz://{0:N}/{1}/{2}",
				guid, input.Length, flags);

			var existing = targetAssembly.MainModule.Resources
				.Where(r => Hash(r) == guid)
				.ToArray();

			if (existing.Length > 0)
			{
				if (overwrite)
				{
					Log.Warn("Resource '{0}' already exists and is going to be replaced.", resourceName);
					foreach (var r in existing)
						targetAssembly.MainModule.Resources.Remove(r);
				}
				else
				{
					Log.Warn("Resource '{0}' already exists and will be skipped.", resourceName);
					return false;
				}
			}

			var resource = new EmbeddedResource(
				resourceName,
				ManifestResourceAttributes.Public,
				output);

			targetAssembly.MainModule.Resources.Add(resource);

			return true;
		}

		/// <summary>Instruments assembly with AsmZ resolver.</summary>
		/// <param name="targetAssembly">The target assembly.</param>
		protected static void InstrumentAsmZ(AssemblyDefinition targetAssembly)
		{
			var helper = new InstrumentHelper(targetAssembly);
			helper.InjectLibZInitializer();
			helper.InjectAsmZResolver();
		}

		#endregion
	}
}
