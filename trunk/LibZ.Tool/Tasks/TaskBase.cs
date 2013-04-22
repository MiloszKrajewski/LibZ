using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class TaskBase
	{
		#region consts

		private static readonly MD5 MD5Service = MD5.Create();

		#endregion

		#region static fields

		private static readonly Dictionary<string, Regex> WildcardCacheRx = new Dictionary<string, Regex>();

		#endregion

		#region file utilities

		protected static void RenameFile(string sourceFileName, string targetFileName)
		{
			try
			{
				var tempFileName = string.Format("{0}.{1:N}", targetFileName, Guid.NewGuid());
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

		protected static IEnumerable<string> FindFiles(
			IEnumerable<string> includePatterns,
			IEnumerable<string> excludePatterns)
		{
			return includePatterns.SelectMany(p => FindFiles(p, excludePatterns))
				.Distinct()
				.OrderBy(s => s);
		}

		private static IEnumerable<string> FindFiles(string pattern, IEnumerable<string> excludePatterns)
		{
			if (!Path.IsPathRooted(pattern)) pattern = ".\\" + pattern;
			var directoryName = Path.GetDirectoryName(pattern) ?? ".";
			var searchPattern = Path.GetFileName(pattern) ?? "*.dll";

			return Directory
				.GetFiles(directoryName, searchPattern)
				.Where(fn => !excludePatterns.Any(
					ep => WildcardToRegex(ep).IsMatch(Path.GetFileName(fn) ?? string.Empty)));
		}

		private static Regex WildcardToRegex(string pattern)
		{
			Regex rx;
			if (!WildcardCacheRx.TryGetValue(pattern, out rx))
			{
				WildcardCacheRx[pattern] = rx = new Regex(
					string.Format("^{0}$", Regex.Escape(pattern).Replace("*", ".*").Replace("?", ".")),
					RegexOptions.IgnoreCase);
			}
			return rx;
		}

		private static Regex[] WildcardToRegex(IEnumerable<string> patterns)
		{
			return patterns == null 
				? new Regex[0] 
				: patterns.Select(WildcardToRegex).ToArray();
		}

		#endregion

		#region reflection utilities

		protected static bool EqualAssemblyNames(string valueA, string valueB)
		{
			return string.Compare(valueA, valueB, StringComparison.InvariantCultureIgnoreCase) == 0;
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

		protected static bool IsSigned(AssemblyDefinition assembly)
		{
			return assembly.Modules.Any(m => (m.Attributes & ModuleAttributes.StrongNameSigned) != 0);
		}

		protected static StrongNameKeyPair LoadKeyPair(string keyFileName, string password)
		{
			if (String.IsNullOrWhiteSpace(keyFileName)) return null;

			Log.Info("Loading singing key from '{0}'", keyFileName);
			// do not use constructor with filename it does not really load the key (?)

			var keyPair =
				string.IsNullOrWhiteSpace(password)
				? new StrongNameKeyPair(File.ReadAllBytes(keyFileName))
				: GetStrongNameKeyPairFromPfx(keyFileName, password);

			try
			{
				var publicKey = keyPair.PublicKey;
				// this is not important, just wanted to clutter screen a little bit
				// there is no built-in ToHexString, and as it is not really needed 
				// I won't write it so I use ToBase64String
				Log.Debug("Public key is '{0}'", Convert.ToBase64String(publicKey));
			}
			catch
			{
				Log.Error("There is a chance this is kind of well-known problem");
				Log.Error(
					"See 'http://stackoverflow.com/questions/5659740/unable-to-obtain-public-key-for-strongnamekeypair' for details");
				throw;
			}
			return keyPair;
		}

		protected static StrongNameKeyPair GetStrongNameKeyPairFromPfx(string pfxFile, string password)
		{
			// http://stackoverflow.com/questions/7556846/how-to-use-strongnamekeypair-with-a-password-protected-keyfile-pfx

			var certs = new X509Certificate2Collection();
			certs.Import(pfxFile, password, X509KeyStorageFlags.Exportable);
			if (certs.Count == 0)
				throw new ArgumentException(null, "pfxFile");

			var provider = certs[0].PrivateKey as RSACryptoServiceProvider;
			if (provider == null) // not a good pfx file
				throw new ArgumentException(null, "pfxFile");

			return new StrongNameKeyPair(provider.ExportCspBlob(false));
		}

		protected static AssemblyDefinition LoadAssembly(string assemblyFileName)
		{
			try
			{
				Log.Debug("Loading '{0}'", assemblyFileName);
				var result = AssemblyDefinition.ReadAssembly(assemblyFileName);
				Log.Debug("Loaded '{0}'", result.FullName);
				return result;
			}
			catch
			{
				Log.Warn("Failed to load assembly from '{0}'", assemblyFileName);
				return null;
			}
		}

		protected static AssemblyDefinition LoadAssembly(byte[] bytes)
		{
			try
			{
				Log.Debug("Loading assmbly from resources");
				var result = AssemblyDefinition.ReadAssembly(new MemoryStream(bytes));
				Log.Debug("Loaded '{0}'", result.FullName);
				return result;
			}
			catch
			{
				Log.Warn("Failed to load assembly from byte buffer");
				return null;
			}
		}

		protected static void SaveAssembly(AssemblyDefinition assembly, string assemblyFileName, StrongNameKeyPair keyPair = null)
		{
			var tempFileName = String.Format("{0}.{1:N}", assemblyFileName, Guid.NewGuid());

			try
			{
				if (keyPair == null)
				{
					Log.Debug("Saving '{0}'", assemblyFileName);
					assembly.Write(tempFileName);
				}
				else
				{
					Log.Debug("Saving and signing '{0}'", assemblyFileName);
					assembly.Write(tempFileName, new WriterParameters { StrongNameKeyPair = keyPair });
				}

				File.Delete(assemblyFileName);
				File.Move(tempFileName, assemblyFileName);
				// TODO:MAK delete .pdb it is no longer valid
			}
			catch
			{
				if (File.Exists(tempFileName)) DeleteFile(tempFileName);
				throw;
			}
		}

		#endregion

		#region exceptions

		protected static Exception ArgumentNull(string argumentName)
		{
			return new ArgumentNullException(argumentName);
		}

		protected static Exception FileNotFound(string fileName)
		{
			return new FileNotFoundException(String.Format("File '{0}' could not be found", fileName));
		}

		#endregion

		#region utilities

		protected static Guid Hash(string text)
		{
			return new Guid(
				MD5Service.ComputeHash(
					Encoding.UTF8.GetBytes(text.ToLowerInvariant())));
		}

		protected static string HashString(string text)
		{
			return Hash(text).ToString("N").ToLowerInvariant();
		}

		#endregion
	}
}