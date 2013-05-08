using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace LibZ.Injected
{
	/// <summary>
	/// AsmZResolver. Mini resolver getting assemblies straight from resources.
	/// </summary>
	public class AsmZResolver
	{
		#region consts

		/// <summary>The hash provider.</summary>
		private static readonly MD5 HashProvider = MD5.Create();

		/// <summary>The resource name regular expression.</summary>
		private static readonly Regex ResourceNameRx = new Regex(
			@"asmz://(?<guid>[0-9a-fA-F]{32})/(?<size>[0-9]+)(/(?<flags>[a-zA-Z0-9]*))?",
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		/// <summary>The 'this' assembly (please note, this type is going to be embedded into other assemblies)</summary>
		private static readonly Assembly ThisAssembly = typeof(AsmZResolver).Assembly;

		/// <summary>Hash of 'this' assembly name.</summary>
		private static readonly Guid ThisAssemblyGuid = Hash(ThisAssembly.FullName);

		#endregion

		#region static fields

		/// <summary>The initialized flag.</summary>
		private static int _initialized;

		/// <summary>The resource names found in 'this' assembly.</summary>
		private static readonly Dictionary<Guid, Match> ResourceNames
			= new Dictionary<Guid, Match>();

		#endregion

		#region public interface

		/// <summary>Initializes resolver.</summary>
		public static void Initialize()
		{
			if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0) return;

			foreach (var rn in ThisAssembly.GetManifestResourceNames())
			{
				var m = ResourceNameRx.Match(rn);
				if (!m.Success) continue;
				var guid = new Guid(m.Groups["guid"].Value);
				if (ResourceNames.ContainsKey(guid))
				{
					Trace.TraceError("Duplicated assembly name, ignoring.");
				}
				else
				{
					ResourceNames[guid] = m;
				}
			}

			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver;
		}

		#endregion

		#region private implementation

		/// <summary>Assembly resolver.</summary>
		/// <param name="sender">The sender.</param>
		/// <param name="args">The <see cref="ResolveEventArgs"/> instance containing the event data.</param>
		/// <returns>Loaded assembly or <c>null</c>.</returns>
		private static Assembly AssemblyResolver(object sender, ResolveEventArgs args)
		{
			Trace.TraceInformation("Resolving: '{0}'", args.Name);

			var name = args.Name;
			var result =
				TryLoadAssembly((IntPtr.Size == 4 ? "x86:" : "x64:") + name) ??
					TryLoadAssembly(name) ??
						TryLoadAssembly((IntPtr.Size == 4 ? "x64:" : "x86:") + name);

			if (result != null) Trace.TraceInformation("Found: '{0}'", args.Name);

			return result;
		}

		/// <summary>Tries the load assembly.</summary>
		/// <param name="resourceName">Name of the resource.</param>
		/// <returns>Loaded assembly or <c>null</c>.</returns>
		private static Assembly TryLoadAssembly(string resourceName)
		{
			try
			{
				var guid = Hash(resourceName);
				Match match;
				if (!ResourceNames.TryGetValue(guid, out match)) return null;
				Trace.TraceInformation("Trying to load '{0}'", resourceName);
				resourceName = match.Groups[0].Value;
				var flags = match.Groups["flags"].Value;
				var size = int.Parse(match.Groups["size"].Value);
				var compressed = flags.Contains("z");
				var unmanaged = flags.Contains("u");

				var buffer = new byte[size];

				using (var rstream = ThisAssembly.GetManifestResourceStream(resourceName))
				{
					if (rstream == null) return null;
					using (var zstream = compressed ? new DeflateStream(rstream, CompressionMode.Decompress) : rstream)
					{
						zstream.Read(buffer, 0, size);
					}
				}

				return unmanaged
					? LoadUnmanagedAssembly(guid, buffer)
					: Assembly.Load(buffer);
			}
			catch (Exception e)
			{
				Trace.TraceError("{0}: {1}", e.GetType().Name, e.Message);
				return null;
			}
		}

		/// <summary>Loads the unmanaged assembly.</summary>
		/// <param name="guid">The GUID.</param>
		/// <param name="assemblyImage">The assembly binary image.</param>
		/// <returns>Loaded assembly or <c>null</c>.</returns>
		private static Assembly LoadUnmanagedAssembly(Guid guid, byte[] assemblyImage)
		{
			Trace.TraceInformation("Trying to load as unmanaged assembly");

			var folderPath = Path.Combine(Path.GetTempPath(), ThisAssemblyGuid.ToString("N"));
			Directory.CreateDirectory(folderPath);
			var filePath = Path.Combine(folderPath, guid.ToString("N") + ".dll");
			var fileInfo = new FileInfo(filePath);

			if (!fileInfo.Exists || fileInfo.Length != assemblyImage.Length)
			{
				File.WriteAllBytes(filePath, assemblyImage);
			}

			return Assembly.LoadFile(filePath);
		}

		/// <summary>Calculates hash of given text (usually assembly name).</summary>
		/// <param name="text">The text.</param>
		/// <returns>A hash.</returns>
		private static Guid Hash(string text)
		{
			return new Guid(
				HashProvider.ComputeHash(
					Encoding.UTF8.GetBytes(
						text.ToLowerInvariant())));
		}

		#endregion
	}
}
