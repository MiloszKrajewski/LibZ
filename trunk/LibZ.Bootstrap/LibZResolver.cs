#region License

/*
 * Copyright (c) 2013, Milosz Krajewski
 * 
 * Microsoft Public License (Ms-PL)
 * This license governs use of the accompanying software. 
 * If you use the software, you accept this license. 
 * If you do not accept the license, do not use the software.
 * 
 * 1. Definitions
 * The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same 
 * meaning here as under U.S. copyright law.
 * A "contribution" is the original software, or any additions or changes to the software.
 * A "contributor" is any person that distributes its contribution under this license.
 * "Licensed patents" are a contributor's patent claims that read directly on its contribution.
 * 
 * 2. Grant of Rights
 * (A) Copyright Grant- Subject to the terms of this license, including the license conditions 
 * and limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
 * royalty-free copyright license to reproduce its contribution, prepare derivative works of 
 * its contribution, and distribute its contribution or any derivative works that you create.
 * (B) Patent Grant- Subject to the terms of this license, including the license conditions and 
 * limitations in section 3, each contributor grants you a non-exclusive, worldwide, 
 * royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, 
 * import, and/or otherwise dispose of its contribution in the software or derivative works of 
 * the contribution in the software.
 * 
 * 3. Conditions and Limitations
 * (A) No Trademark License- This license does not grant you rights to use any contributors' name, 
 * logo, or trademarks.
 * (B) If you bring a patent claim against any contributor over patents that you claim are infringed 
 * by the software, your patent license from such contributor to the software ends automatically.
 * (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, 
 * and attribution notices that are present in the software.
 * (D) If you distribute any portion of the software in source code form, you may do so only under this 
 * license by including a complete copy of this license with your distribution. If you distribute 
 * any portion of the software in compiled or object code form, you may only do so under a license 
 * that complies with this license.
 * (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express
 * warranties, guarantees or conditions. You may have additional consumer rights under your local 
 * laws which this license cannot change. To the extent permitted under your local laws, the 
 * contributors exclude the implied warranties of merchantability, fitness for a particular 
 * purpose and non-infringement.
 */

#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

/*
 * NOTE: This files contains multiple classes and namespaces for easy embedding into other assemblies.
 * It does not look nice, but makes embedding LibZResolver easy. Just drag this file into assembly 
 * and you will have access to fully functional LibZResolver.
 */

#if LIBZ_MANAGER
namespace LibZ.Manager
#else
namespace LibZ.Bootstrap
#endif
{
	using Internal;

	#region class LibZResolver

	public class LibZResolver
	{
		#region static fields

		/// <summary>The containers</summary>
		private static readonly List<LibZReader> Containers;

		#endregion

		#region shared static properies

		/// <summary>The shared dictionary.</summary>
		private static readonly GlobalDictionary SharedData = 
			new GlobalDictionary("LibZResolver.71c503c0c0824d9785f4994d5034c8a0");

		/// <summary>Gets or sets the register stream callback.</summary>
		/// <value>The register stream callback.</value>
		private static Action<Stream> RegisterStream
		{
			get { return SharedData.Get<Action<Stream>>(0); }
			set { SharedData.Set(0, value); }
		}

		/// <summary>Gets or sets the decoders dictionary.</summary>
		/// <value>The decoders dictionary.</value>
		private static Dictionary<uint, Func<byte[], int, byte[]>> Decoders
		{
			get { return SharedData.Get<Dictionary<uint, Func<byte[], int, byte[]>>>(1); }
			set { SharedData.Set(1, value); }
		}

		/// <summary>Gets or sets the executable folder.</summary>
		/// <value>The executable folder.</value>
		private static string ExecutableFolder
		{
			get { return SharedData.Get<string>(2); }
			set { SharedData.Set(2, value); }
		}

		/// <summary>Gets or sets the search path.</summary>
		/// <value>The search path.</value>
		private static List<string> SearchPath
		{
			get { return SharedData.Get<List<string>>(3); }
			set { SharedData.Set(3, value); }
		}

		#endregion

		#region static constructor

		/// <summary>Initializes the <see cref="LibZResolver"/> class.</summary>
		static LibZResolver()
		{
			// this is VERY bad, I know
			// there are potentially 2 classes, they have same name, they should
			// share same data, but they are not the SAME class, so to interlock them
			// I need something known to both of them
			lock (typeof(object))
			{
				if (!SharedData.IsOwner) return;

				Containers = new List<LibZReader>();

				// intialize paths
				var assembly = Assembly.GetEntryAssembly() ?? typeof(LibZResolver).Assembly;
				var executableFolder = Path.GetDirectoryName(assembly.Location);
				var searchPath = new List<string>();
				if (executableFolder != null) searchPath.Add(executableFolder);
				var systemPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
				searchPath.AddRange(systemPath.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)));

				RegisterStream = (stream) => {
					var container = new LibZReader(stream);
					if (Containers.Any(c => c.ContainerId == container.ContainerId)) return;
					Containers.Add(container);
				};
				Decoders = new Dictionary<uint, Func<byte[], int, byte[]>>();
				ExecutableFolder = executableFolder;
				SearchPath = searchPath;

				RegisterDecoder("deflate", LibZReader.DeflateDecoder);

				// initialize assembly resolver
				AppDomain.CurrentDomain.AssemblyResolve += (s, e) => Resolve(e);
			}
		}

		#endregion

		#region public interface

		/// <summary>Registers the container.</summary>
		/// <param name="stream">The stream.</param>
		/// <param name="optional">if set to <c>true</c> container is optional, 
		/// so failure to load does not cause exception.</param>
		public static void RegisterContainer(Stream stream, bool optional = true)
		{
			try
			{
				RegisterStream(stream);
			}
			catch
			{
				if (!optional) throw;
			}
		}

		/// <summary>Registers the container from file.</summary>
		/// <param name="libzFileName">Name of the libz file.</param>
		/// <param name="optional">if set to <c>true</c> container is optional, 
		/// so failure to load does not cause exception.</param>
		/// <exception cref="System.IO.FileNotFoundException"/>
		public static void RegisterContainer(string libzFileName, bool optional = true)
		{
			try
			{
				var fileName = FindFile(libzFileName);

				if (fileName == null)
					throw new FileNotFoundException(string.Format("LibZ library '{0}' cannot be found", libzFileName));

				var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

				RegisterContainer(stream);
			}
			catch
			{
				if (!optional) throw;
			}
		}

		/// <summary>Registers the containers from folder.</summary>
		/// <param name="folder">The folder.</param>
		/// <param name="libzFilePattern">The libz file pattern.</param>
		/// <param name="optional">if set to <c>true</c> containers are optional, 
		/// so failure to load does not cause exception.</param>
		public static void RegisterContainer(string folder, string libzFilePattern, bool optional = true)
		{
			var folderInfo = new DirectoryInfo(Path.Combine(ExecutableFolder, folder));
			if (!folderInfo.Exists) return;
			foreach (var file in folderInfo.GetFiles(libzFilePattern))
			{
				RegisterContainer(file.FullName, optional);
			}
		}

		public static void RegisterDecoder(string codecName, Func<byte[], int, byte[]> decoder, bool overwrite = false)
		{
			if (String.IsNullOrEmpty(codecName))
				throw new ArgumentException("codecName is null or empty.", "codecName");
			if (decoder == null)
				throw new ArgumentNullException("decoder", "decoder is null.");

			var codecId = HashProvider.CRC(codecName);

			if (overwrite)
			{
				lock (Decoders) Decoders[codecId] = decoder;
			}
			else
			{
				try
				{
					lock (Decoders) Decoders.Add(codecId, decoder);
				}
				catch (ArgumentException e)
				{
					throw new ArgumentException(
						string.Format("Codec '{0}' ({1}) already registered", codecName, codecId), e);
				}
			}
		}

		#endregion

		#region private implementation

		/// <summary>Tries to load missing assembly from LibZ containers.</summary>
		/// <param name="args">The <see cref="ResolveEventArgs"/> instance containing the event data.</param>
		/// <returns>Loaded assembly (or <c>null</c>)</returns>
		private static Assembly Resolve(ResolveEventArgs args)
		{
			var fullName = args.Name.ToLower();
			var guid = HashProvider.MD5(fullName);

			foreach (var container in Containers)
			{
				if (container.HasEntry(guid))
					return Assembly.Load(container.GetBytes(guid, Decoders));
			}

			return null;
		}

		/// <summary>Finds the file on search path.</summary>
		/// <param name="libzFileName">Name of the libz file.</param>
		/// <returns>Full path of found LibZ file, or <c>null</c>.</returns>
		private static string FindFile(string libzFileName)
		{
			if (Path.IsPathRooted(libzFileName))
			{
				return File.Exists(libzFileName) ? libzFileName : null;
			}

			foreach (var candidate in SearchPath)
			{
				var temp = Path.GetFullPath(Path.Combine(candidate, libzFileName));
				if (File.Exists(temp)) return temp;
			}

			return null;
		}

		#endregion
	}

	#endregion

#if !LIBZ_MANAGER
	namespace Internal
	{
#endif

		#region class LibZReader

		public class LibZReader : IDisposable
		{
			#region enum EntryFlags

			[Flags]
			protected enum EntryFlags
			{
				None = 0x00,
			}

			#endregion

			#region class Entry

			protected class Entry
			{
				public Guid Hash { get; set; }
				public EntryFlags Flags { get; set; }
				public long Offset { get; set; }
				public int OriginalLength { get; set; }
				public int StorageLength { get; set; }
				public uint Codec { get; set; }
			}

			#endregion

			#region consts

			protected static readonly Guid Magic = new Guid(Encoding.ASCII.GetBytes("LibZContainer103"));
			protected static readonly int GuidLength = Guid.Empty.ToByteArray().Length; // that's nasty, but reliable
			protected static readonly int CurrentVersion = 103;

			#endregion

			#region static fields

			private static readonly Dictionary<uint, Func<byte[], int, byte[]>> Decoders
				= new Dictionary<uint, Func<byte[], int, byte[]>>();

			#endregion

			#region fields

			protected Guid _containerId = Guid.Empty;
			protected long _magicOffset;
			protected int _version;
			protected Dictionary<Guid, Entry> _entries = new Dictionary<Guid, Entry>();

			protected Stream _stream;
			protected BinaryReader _reader;

			/// <summary>Indicates if object has been already disposed.</summary>
			protected bool _disposed;

			#endregion

			#region properties

			public Guid ContainerId
			{
				get { return _containerId; }
			}

			#endregion

			#region static constructor

			static LibZReader()
			{
				RegisterDecoder("deflate", DeflateDecoder);
			}

			#endregion

			#region constructor

			protected LibZReader() {}

			public LibZReader(Stream stream)
			{
				_stream = stream;
				_reader = new BinaryReader(_stream);
				OpenFile();
			}

			public LibZReader(string fileName)
				: this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {}

			#endregion

			#region initialization

			protected void OpenFile()
			{
				lock (_stream)
				{
					_stream.Position = 0;
					var guid = new Guid(_reader.ReadBytes(GuidLength));
					if (guid != Magic)
						throw new ArgumentException("Invalid LibZ file header");
					_containerId = new Guid(_reader.ReadBytes(GuidLength));
					_version = _reader.ReadInt32();
					if (_version != CurrentVersion)
						throw new NotSupportedException(string.Format("Unsupported LibZ file version ({0})", _version));
					_stream.Position = _stream.Length - GuidLength - sizeof (long);
					_magicOffset = _reader.ReadInt64();
					guid = new Guid(_reader.ReadBytes(GuidLength));
					if (guid != Magic)
						throw new ArgumentException("Invalid LibZ file footer");
					_stream.Position = _magicOffset;
					int count = _reader.ReadInt32();
					for (int i = 0; i < count; i++)
					{
						var entry = ReadEntry();
						_entries.Add(entry.Hash, entry);
					}
				}
			}

			#endregion

			#region codec management

			public static void RegisterDecoder(string codec, Func<byte[], int, byte[]> decoder, bool overwrite = false)
			{
				if (String.IsNullOrEmpty(codec))
					throw new ArgumentException("codecName is null or empty.", "codec");
				if (decoder == null)
					throw new ArgumentNullException("decoder", "decoder is null.");

				var codecId = HashProvider.CRC(codec);
				var decoders = Decoders;

				if (overwrite)
				{
					lock (decoders) decoders[codecId] = decoder;
				}
				else
				{
					try
					{
						lock (decoders) decoders.Add(codecId, decoder);
					}
					catch (ArgumentException e)
					{
						throw new ArgumentException(
							string.Format("Codec '{0}' ({1}) already registered", codec, codecId), e);
					}
				}
			}

			protected static byte[] Decode(
				uint codec, byte[] data, int outputLength,
				IDictionary<uint, Func<byte[], int, byte[]>> decoders = null)
			{
				if (codec == 0) return data;
				if (decoders == null) decoders = Decoders;
				Func<byte[], int, byte[]> decoder;
				lock (decoders)
				{
					if (!decoders.TryGetValue(codec, out decoder))
						throw new ArgumentException(string.Format("Unknown codec id '{0}'", codec));
				}
				return decoder(data, outputLength);
			}

			#endregion

			#region read

			private Entry ReadEntry()
			{
				lock (_stream)
				{
					var entry = new Entry {
						Hash = new Guid(_reader.ReadBytes(GuidLength)),
						Flags = (EntryFlags) _reader.ReadInt32(),
						Offset = _reader.ReadInt64(),
						OriginalLength = _reader.ReadInt32(),
						StorageLength = _reader.ReadInt32(),
						Codec = _reader.ReadUInt32(),
					};
					return entry;
				}
			}

			private byte[] ReadData(Entry entry, IDictionary<uint, Func<byte[], int, byte[]>> decoders)
			{
				byte[] buffer;

				lock (_stream)
				{
					_stream.Position = entry.Offset;
					buffer = ReadBytes(_stream, entry.StorageLength);
				}

				// this needs to be outside lock!
				return Decode(entry.Codec, buffer, entry.OriginalLength, decoders);
			}

			#endregion

			#region access

			public byte[] GetBytes(Guid hash, IDictionary<uint, Func<byte[], int, byte[]>> decoders)
			{
				return ReadData(_entries[hash], decoders);
			}

			public byte[] GetBytes(string resourceName, IDictionary<uint, Func<byte[], int, byte[]>> decoders)
			{
				return GetBytes(HashProvider.MD5(resourceName), decoders);
			}

			public bool HasEntry(Guid hash)
			{
				return _entries.ContainsKey(hash);
			}

			//public bool HasEntry(string resourceName) { return HasEntry(HashProvider.MD5(resourceName)); }

			#endregion

			#region utility

			internal static byte[] DeflateDecoder(byte[] input, int outputLength)
			{
				using (var mstream = new MemoryStream(input))
				using (var zstream = new DeflateStream(mstream, CompressionMode.Decompress))
				{
					var result = new byte[outputLength];
					var read = zstream.Read(result, 0, outputLength);
					if (read != outputLength) throw new IOException("Corrupted data in deflate stream");
					return result;
				}
			}

			protected static byte[] ReadBytes(Stream stream, int length)
			{
				var result = new byte[length];
				var read = stream.Read(result, 0, length);
				if (read < length) throw new IOException("Stream ended prematurely");
				return result;
			}

			#endregion

			#region IDisposable Members

			protected virtual void Clear()
			{
				TryDispose(ref _reader);
				TryDispose(ref _stream);
				_entries = null;
			}

			/// <summary>
			/// Releases unmanaged resources and performs other cleanup operations before the
			/// object is reclaimed by garbage collection.
			/// </summary>
			~LibZReader()
			{
				Dispose(false);
			}

			/// <summary>
			/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
			/// </summary>
			public void Dispose()
			{
				Dispose(true);
				GC.SuppressFinalize(this);
			}

			/// <summary>
			/// Releases unmanaged and - optionally - managed resources
			/// </summary>
			/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; 
			/// <c>false</c> to release only unmanaged resources.</param>
			private void Dispose(bool disposing)
			{
				if (_disposed) return;

				try
				{
					if (disposing)
						DisposeManaged();
					DisposeUnmanaged();
				}
				finally
				{
					_disposed = true;
				}
			}

			/// <summary>Disposes the managed resources.</summary>
			protected virtual void DisposeManaged()
			{
				Clear();
			}

			protected virtual void DisposeUnmanaged()
			{
				// do nothing
			}

			protected static void TryDispose<T>(ref T subject) where T : class
			{
				if (ReferenceEquals(subject, null)) return;
				var disposable = subject as IDisposable;
				if (ReferenceEquals(disposable, null)) return;
				disposable.Dispose();
				subject = null;
			}

			#endregion
		}

		#endregion

#if !LIBZ_MANAGER
	}
#endif

	#region namespace Internal

	namespace Internal
	{
		#region class GlobalDictionary

		internal class GlobalDictionary
		{
			private readonly Dictionary<int, object> _vmt;

			public GlobalDictionary(string name)
			{
				lock (typeof(object))
				{
					_vmt = AppDomain.CurrentDomain.GetData(name) as Dictionary<int, object>;
					if (_vmt != null) return;

					_vmt = new Dictionary<int, object>();
					AppDomain.CurrentDomain.SetData(name, _vmt);
					IsOwner = true;
				}
			}

			public bool IsOwner { get; private set; }

			public T Get<T>(int slot, T defaultValue = default (T))
			{
				object result;
				if (!_vmt.TryGetValue(slot, out result)) return defaultValue;
				return (T)result;
			}

			public void Set(int slot, object value)
			{
				_vmt[slot] = value;
			}
		}

		#endregion

		#region class HashProvider

		/// <summary>CRC32 calculator.</summary>
		internal class HashProvider
		{
			#region fields

			/// <summary>CRC Table.</summary>
			private static readonly uint[] Crc32Table;
			private readonly static MD5 MD5Provider = System.Security.Cryptography.MD5.Create();

			#endregion

			#region constructor

			/// <summary>Initializes the <see cref="HashProvider"/> class.</summary>
			static HashProvider()
			{
				const uint poly = 0xedb88320;
				Crc32Table = new uint[256];
				for (uint i = 0; i < Crc32Table.Length; ++i)
				{
					var temp = i;
					for (var j = 8; j > 0; --j) temp = (temp & 1) == 1 ? (temp >> 1) ^ poly : temp >> 1;
					Crc32Table[i] = temp;
				}
			}

			#endregion

			#region public interface

			/// <summary>Computes the CRC for specified byte array.</summary>
			/// <param name="bytes">The bytes.</param>
			/// <returns>CRC.</returns>
			public static uint CRC(byte[] bytes)
			{
				var crc = 0xffffffffu;
				for (var i = 0; i < bytes.Length; ++i)
				{
					var index = (byte)((crc & 0xff) ^ bytes[i]);
					crc = (crc >> 8) ^ Crc32Table[index];
				}
				return ~crc;
			}

			public static Guid MD5(byte[] bytes)
			{
				return new Guid(MD5Provider.ComputeHash(bytes));
			}

			public static uint CRC(string text) { return CRC(Encoding.UTF8.GetBytes(text)); }
			public static Guid MD5(string text) { return MD5(Encoding.UTF8.GetBytes(text)); }

			#endregion
		}

		#endregion
	}

	#endregion
}
