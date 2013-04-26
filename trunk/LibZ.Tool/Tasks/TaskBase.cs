using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
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

		#region signing utilities

		/// <summary>Loads the key pair.</summary>
		/// <param name="keyFileName">Name of the key file.</param>
		/// <param name="password">The password.</param>
		/// <returns>Key pair.</returns>
		protected static StrongNameKeyPair LoadKeyPair(string keyFileName, string password)
		{
			if (String.IsNullOrWhiteSpace(keyFileName)) return null;

			Log.Info("Loading singing key from '{0}'", keyFileName);
			// do not use constructor with filename it does not really load the key (?)

			var keyPair =
				String.IsNullOrWhiteSpace(password)
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

		/// <summary>Gets the strong name key pair from PFX.</summary>
		/// <param name="pfxFile">The PFX file.</param>
		/// <param name="password">The password.</param>
		/// <returns>Key pair.</returns>
		/// <exception cref="System.ArgumentException">pfxFile</exception>
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

		#endregion

		#region reflection utilities

		/// <summary>Loads the assembly.</summary>
		/// <param name="assemblyFileName">Name of the assembly file.</param>
		/// <returns>Loaded assembly.</returns>
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

		/// <summary>Loads the assembly.</summary>
		/// <param name="bytes">The bytes.</param>
		/// <returns>Loaded assembly.</returns>
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

		/// <summary>Saves the assembly.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <param name="assemblyFileName">Name of the assembly file.</param>
		/// <param name="keyPair">The key pair.</param>
		protected static void SaveAssembly(
			AssemblyDefinition assembly, string assemblyFileName,
			StrongNameKeyPair keyPair = null)
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
					assembly.Write(tempFileName, new WriterParameters {StrongNameKeyPair = keyPair});
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

		/// <summary>Compares assembly names.</summary>
		/// <param name="valueA">The value A.</param>
		/// <param name="valueB">The value B.</param>
		/// <returns></returns>
		protected static bool EqualAssemblyNames(string valueA, string valueB)
		{
			return String.Compare(valueA, valueB, StringComparison.InvariantCultureIgnoreCase) == 0;
		}

		/// <summary>Determines whether the specified assembly is managed.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns><c>true</c> if the specified assembly is managed; otherwise, <c>false</c>.</returns>
		protected static bool IsManaged(AssemblyDefinition assembly)
		{
			return assembly.Modules.All(m => (m.Attributes & ModuleAttributes.ILOnly) != 0);
		}

		/// <summary>Determines whether the specified assembly is signed.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns><c>true</c> if the specified assembly is signed; otherwise, <c>false</c>.</returns>
		protected static bool IsSigned(AssemblyDefinition assembly)
		{
			return assembly.Modules.Any(m => (m.Attributes & ModuleAttributes.StrongNameSigned) != 0);
		}

		/// <summary>Gets the assembly architecture.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns>Assembly architecture.</returns>
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

		private static readonly Regex ResourceNameRx = new Regex(
			@"asmz://(?<guid>[^/]*)/(?<size>[0-9]+)(/(?<flags>[a-zA-Z0-9]*))?",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

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
			if (!IsManaged(sourceAssembly)) flags += "u";

			var input = sourceAssemblyBytes;
			byte[] output = DefaultCodecs.DeflateEncoder(input);

			if (output.Length < input.Length)
			{
				flags += "z";
			}
			else
			{
				output = input;
			}

			var architecture = GetArchitecture(sourceAssembly);
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
