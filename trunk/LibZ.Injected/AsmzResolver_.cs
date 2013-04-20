using System;
using System.Collections.Generic;
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
		private static int _initialized;

		private static readonly MD5 MD5Service = MD5.Create();

		private static readonly Regex ResourceNamePattern = new Regex(
			@"asmz://(?<guid>[^/]*)/(?<size>[0-9]+)(/(?<flags>[a-zA-Z0-9]*))?", 
			RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

		private static readonly Assembly ThisAssembly = typeof (AsmZResolver).Assembly;
		private static readonly Guid ThisAssemblyGuid = Hash(ThisAssembly.FullName);

		private static readonly Dictionary<Guid, Match> ResourceNames 
			= new Dictionary<Guid, Match>();

		static AsmZResolver()
		{
			foreach (var rn in ThisAssembly.GetManifestResourceNames())
			{
				var m = ResourceNamePattern.Match(rn);
				if (!m.Success) continue;
				var guid = new Guid(m.Groups["guid"].Value);
				ResourceNames.Add(guid, m);
			}
			AppDomain.CurrentDomain.AssemblyResolve += AssemblyResolver;
		}

		static void Initialize()
		{
			Interlocked.CompareExchange(ref _initialized, 1, 0);
		}

		private static Assembly AssemblyResolver(object sender, ResolveEventArgs args)
		{
			var name = args.Name;
			return
				TryLoadAssembly((IntPtr.Size == 4 ? "x86:" : "x64:") + name) ??
				TryLoadAssembly(name) ??
				TryLoadAssembly((IntPtr.Size == 4 ? "x64:" : "x86:") + name);
		}

		private static Assembly TryLoadAssembly(string resourceName)
		{
			try
			{
				var guid = Hash(resourceName);
				Match match;
				if (!ResourceNames.TryGetValue(guid, out match)) return null;
				resourceName = match.Groups[0].Value;
				var flags = match.Groups["flags"].Value ?? string.Empty;
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
			catch
			{
				return null;
			}
		}

		private static Assembly LoadUnmanagedAssembly(Guid guid, byte[] buffer)
		{
			var folderPath = Path.Combine(Path.GetTempPath(), ThisAssemblyGuid.ToString("N"));
			Directory.CreateDirectory(folderPath);
			var filePath = Path.Combine(folderPath, guid.ToString("N") + ".dll");
			var fileInfo = new FileInfo(filePath);

			if (!fileInfo.Exists || fileInfo.Length != buffer.Length)
			{
				File.WriteAllBytes(filePath, buffer);
			}

			return Assembly.Load(filePath);
		}

		private static Guid Hash(string text)
		{
			return new Guid(
				MD5Service.ComputeHash(
					Encoding.UTF8.GetBytes(
						text.ToLowerInvariant())));
		}
	}
}
