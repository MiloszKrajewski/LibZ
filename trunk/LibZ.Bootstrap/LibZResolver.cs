using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;

/*
 * NOTE: This files contains multiple classes and namespaces for easy embedding into other assemblies.
 * Just drag this file into assebly and you will have access to fully functional decoder.
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

		private static readonly GlobalVariable<bool> VMTInitialized =
			new GlobalVariable<bool>("LibZResolver.71c503c0-c082-4d97-85f4-994d5034c8a0");
		private static readonly GlobalVariable<Action<Stream>> VMTAddStream =
			new GlobalVariable<Action<Stream>>("LibZResolver.7144cba0-0ae6-4afc-9cd0-6b9576cfbccf");
		private static readonly GlobalVariable<List<string>> VMTSearchPath =
			new GlobalVariable<List<string>>("LibZResolver.5671e4f8-dda0-4bb7-a63a-018950c9a79f");
		private static readonly GlobalVariable<string> VMTExecutableFolder =
			new GlobalVariable<string>("LibZResolver.3df709ce-dda9-44b5-9c56-8a5ac4d0a5d0");

		private static readonly List<LibZReader> Containers;

		#endregion

		#region static constructor

		static LibZResolver()
		{
			lock (GlobalVariable.Lock) 
			{
				if (VMTInitialized.Value) return;

				VMTAddStream.Value = InternalAddStream;

				Containers = new List<LibZReader>();

				// intialize paths

				var assembly = Assembly.GetEntryAssembly() ?? typeof (LibZResolver).Assembly;
				VMTExecutableFolder.Value = Path.GetDirectoryName(assembly.Location);
				var systemPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
				VMTSearchPath.Value = new List<string> {ExecutableFolder};
				VMTSearchPath.Value.AddRange(systemPath.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)));

				AppDomain.CurrentDomain.AssemblyResolve += (s, e) => Resolve(e);

				VMTInitialized.Value = true;
			}
		}

		#endregion

		#region public interface

		public static string ExecutableFolder { get { return VMTExecutableFolder.Value; } }
		public static List<string> SearchPath { get { return VMTSearchPath.Value; } }

		public static void RegisterStream(Stream stream, bool optional = true)
		{
			try
			{
				VMTAddStream.Value(stream);
			}
			catch
			{
				if (!optional) throw;
			}
		}

		public static void RegisterFile(string libzFileName, bool optional = true)
		{
			try
			{
				var fileName = FindFile(libzFileName);

				if (fileName == null)
					throw new FileNotFoundException(string.Format("LibZ library '{0}' cannot be found", libzFileName));

				var stream = File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);

				RegisterStream(stream);
			}
			catch
			{
				if (!optional) throw;
			}
		}

		public static void RegisterFiles(string folder, string libzFilePattern, bool optional = true)
		{
			var folderInfo = new DirectoryInfo(Path.Combine(ExecutableFolder, folder));
			if (!folderInfo.Exists) return;
			foreach (var file in folderInfo.GetFiles(libzFilePattern))
			{
				RegisterFile(file.FullName, optional);
			}
		}

		#endregion

		#region private implementation

		private static void InternalAddStream(Stream stream)
		{
			var container = new LibZReader(stream);
			if (Containers.Any(c => c.ContainerId == container.ContainerId)) return;
			Containers.Add(container);
		}

		private static Assembly Resolve(ResolveEventArgs args)
		{
			var fullName = args.Name.ToLower();
			var guid = LibZReader.CreateHash(fullName);

			foreach (var container in Containers)
			{
				if (container.HasEntry(guid))
					return Assembly.Load(container.GetBytes(guid));
			}

			return null;
		}

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

	#region class LibZReader

	public class LibZReader: IDisposable
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

		protected const int CopyBufferLength = 0x4000;
		protected static readonly Guid Magic = new Guid("F434A49F-859F-40AB-A460-C87270575249");
		protected static readonly int GuidLength = Guid.Empty.ToByteArray().Length; // that's nasty, but reliable
		protected static readonly int CurrentVersion = 103;

		#endregion

		#region static fields

		private readonly static MD5 HashProvider = MD5.Create();
		private readonly static Dictionary<uint, Func<byte[], int, byte[]>> Decoders
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

		public Guid ContainerId { get { return _containerId; } }

		#endregion

		#region constructor

		protected LibZReader()
		{
		}

		public LibZReader(Stream stream)
		{
			_stream = stream;
			_reader = new BinaryReader(_stream);
			OpenFile();
		}

		public LibZReader(string fileName)
			: this(File.Open(fileName, FileMode.Open, FileAccess.Read, FileShare.Read))
		{
		}

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
				_stream.Position = _stream.Length - GuidLength - sizeof(long);
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
				throw new ArgumentException("codec is null or empty.", "codec");
			if (decoder == null)
				throw new ArgumentNullException("decoder", "decoder is null.");

			var codecId = StringToCodec(codec);

			if (overwrite)
			{
				Decoders[codecId] = decoder;
			}
			else
			{
				try
				{
					Decoders.Add(codecId, decoder);
				}
				catch (ArgumentException e)
				{
					throw new ArgumentException(
						string.Format("Codec '{0}' ({1}) already registered", codec, codecId), e);
				}
			}
		}

		protected static uint StringToCodec(string codec)
		{
			uint result = Crc32.Compute(Encoding.UTF8.GetBytes(codec));
			if (result != 0) return result;

			throw new ArgumentException(string.Format("Invalid codec name '{0}', cannot produce valid CRC", codec));
		}

		protected static byte[] Decode(uint codec, byte[] data, int outputLength)
		{
			if (codec == 0) return data;
			Func<byte[], int, byte[]> decoder;
			if (!Decoders.TryGetValue(codec, out decoder))
				throw new ArgumentException(string.Format("Unknown codec id '{0}'", codec));
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
					Flags = (EntryFlags)_reader.ReadInt32(),
					Offset = _reader.ReadInt64(),
					OriginalLength = _reader.ReadInt32(),
					StorageLength = _reader.ReadInt32(),
					Codec = _reader.ReadUInt32(),
				};
				return entry;
			}
		}

		private byte[] ReadData(Entry entry)
		{
			byte[] buffer;

			lock (_stream)
			{
				_stream.Position = entry.Offset;
				buffer = ReadBytes(_stream, entry.OriginalLength);
			}

			// this needs to be outside lock!
			return Decode(entry.Codec, buffer, entry.StorageLength);
		}

		#endregion

		#region access

		public byte[] GetBytes(Guid hash)
		{
			return ReadData(_entries[hash]);
		}

		public byte[] GetBytes(string resourceName)
		{
			return GetBytes(CreateHash(resourceName));
		}

		public bool HasEntry(Guid hash) { return _entries.ContainsKey(hash); }
		public bool HasEntry(string resourceName) { return HasEntry(CreateHash(resourceName)); }

		#endregion

		#region utility

		public static Guid CreateHash(string resourceName)
		{
			return new Guid(HashProvider.ComputeHash(Encoding.UTF8.GetBytes(resourceName)));
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

		protected static void TryDispose<T>(ref T subject) where T: class
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

	#region namespace Internal

	namespace Internal
	{
		#region class GlobalVariable

		public class GlobalVariable
		{
			public static readonly object Lock;

			static GlobalVariable()
			{
				// this is VERY bad, I know
				// there are potentially 2 GlobalVariable classes, they have same name, they should
				// share same data, but they are not the SAME class, so to interlock them
				// I need something known to both of them
				lock (typeof(object))
				{
					const string name = "GlobalVariable.a3eef3d0-4ad1-4cef-9bf6-6c795ac345ef";
					Lock = AppDomain.CurrentDomain.GetData(name);
					if (Lock == null) AppDomain.CurrentDomain.SetData(name, Lock = new object());
				}
			}

		}

		public class GlobalVariable<T>
		{
			private readonly string _name;

			public GlobalVariable(string name)
			{
				_name = name;
			}

			public T Value
			{
				get
				{
					var value = AppDomain.CurrentDomain.GetData(_name);
					if (value == null) return default(T);
					return (T) value;
				}
				set
				{
					AppDomain.CurrentDomain.SetData(_name, value);
				}
			}
		}

		#endregion

		#region class Crc32

		public class Crc32
		{
			private static readonly uint[] Crc32Table;

			public static uint Compute(byte[] bytes)
			{
				var crc = 0xffffffffu;
				for (var i = 0; i < bytes.Length; ++i)
				{
					var index = (byte)(((crc) & 0xff) ^ bytes[i]);
					crc = (crc >> 8) ^ Crc32Table[index];
				}
				return ~crc;
			}

			static Crc32()
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
		}

		#endregion
	}

	#endregion
}
