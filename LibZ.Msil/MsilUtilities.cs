using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Mono.Cecil;
using NLog;

namespace LibZ.Msil
{
    public static class MsilUtilities
    {
	    private static readonly Logger Log = LogManager.GetLogger("MsilUtilities");

		/// <summary>Ignore case constant.</summary>
		private const StringComparison IgnoreCase = StringComparison.OrdinalIgnoreCase;

		/// <summary>Deletes the file.</summary>
		/// <param name="fileName">Name of the file.</param>
		private static void DeleteFile(string fileName)
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

		/// <summary>Loads the key pair.</summary>
		/// <param name="keyFileName">Name of the key file.</param>
		/// <param name="password">The password.</param>
		/// <returns>Key pair.</returns>
		public static StrongNameKeyPair LoadKeyPair(string keyFileName, string password)
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
				Log.Error("See 'http://stackoverflow.com/questions/5659740/unable-to-obtain-public-key-for-strongnamekeypair' for details");
				throw;
			}
			return keyPair;
		}

		/// <summary>Gets the strong name key pair from PFX.</summary>
		/// <param name="pfxFile">The PFX file.</param>
		/// <param name="password">The password.</param>
		/// <returns>Key pair.</returns>
		/// <exception cref="System.ArgumentException">pfxFile</exception>
		public static StrongNameKeyPair GetStrongNameKeyPairFromPfx(string pfxFile, string password)
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

		/// <summary>Loads the assembly.</summary>
		/// <param name="assemblyFileName">Name of the assembly file.</param>
		/// <returns>Loaded assembly.</returns>
		public static AssemblyDefinition LoadAssembly(string assemblyFileName)
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
		public static AssemblyDefinition LoadAssembly(byte[] bytes)
		{
			try
			{
				Log.Debug("Loading assembly from resources");
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
		public static void SaveAssembly(
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
					assembly.Write(tempFileName, new WriterParameters { StrongNameKeyPair = keyPair });
				}

				File.Delete(assemblyFileName);
				File.Move(tempFileName, assemblyFileName);

				// TODO:MAK pdb may also be merged, but it's not a priority for me. 
				// I need to deleted in though as it no longer matches assembly
				var pdbFileName = Path.ChangeExtension(assemblyFileName, "pdb");
				if (File.Exists(pdbFileName)) 
					DeleteFile(pdbFileName);
			}
			catch
			{
				if (File.Exists(tempFileName)) 
					DeleteFile(tempFileName);
				throw;
			}
		}

		/// <summary>Compares assembly names.</summary>
		/// <param name="valueA">The value A.</param>
		/// <param name="valueB">The value B.</param>
		/// <returns></returns>
		public static bool EqualAssemblyNames(string valueA, string valueB)
		{
			return String.Compare(valueA, valueB, IgnoreCase) == 0;
		}

		/// <summary>Determines whether the specified assembly is managed.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns><c>true</c> if the specified assembly is managed; otherwise, <c>false</c>.</returns>
		public static bool IsManaged(AssemblyDefinition assembly)
		{
			return assembly.Modules.All(m => (m.Attributes & ModuleAttributes.ILOnly) != 0);
		}

		/// <summary>Determines whether the specified assembly is portable.
		/// It uses probably very simplified method of finding retargetable references.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns><c>true</c> if the specified assembly is portable; otherwise, <c>false</c>.</returns>
		public static bool IsPortable(AssemblyDefinition assembly)
		{
			return assembly.Modules
				.SelectMany(m => m.AssemblyReferences)
				.Any(r => r.IsRetargetable);
		}

		/// <summary>Determines whether the specified assembly is signed.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns><c>true</c> if the specified assembly is signed; otherwise, <c>false</c>.</returns>
		public static bool IsSigned(AssemblyDefinition assembly)
		{
			return assembly.Modules.Any(m => (m.Attributes & ModuleAttributes.StrongNameSigned) != 0);
		}

		/// <summary>Gets the assembly architecture.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns>Assembly architecture.</returns>
		public static AssemblyArchitecture GetArchitecture(AssemblyDefinition assembly)
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

		/// <summary>Gets the framework version.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <returns>Framework version (major part only).</returns>
		/// <exception cref="System.ArgumentException">Thrown if 'System' is not referenced.</exception>
		public static Version GetFrameworkVersion(AssemblyDefinition assembly)
		{
			var systemReference = assembly.MainModule.AssemblyReferences
				.FirstOrDefault(r => string.Compare(r.Name, "mscorlib", StringComparison.InvariantCultureIgnoreCase) == 0);

			if (systemReference == null)
				throw new ArgumentException(string.Format(
					"Assembly '{0}' does not reference 'System' assembly. Cannot determine .NET version.", assembly.Name));

			return systemReference.Version;
		}
    }
}
