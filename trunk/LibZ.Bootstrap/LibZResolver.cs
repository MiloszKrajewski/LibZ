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
		#region consts

		private const string AssemblyResolverId = "EA585A2C-974E-4F00-8855-3AD377D30322";

		#endregion

		#region fields

		private readonly static string s_ExecutableFolder;
		private readonly static string[] _searchPaths;

		private readonly static Action<Stream> _addFile;
		private readonly static Func<ResolveEventArgs, Assembly> _resolve;
		private readonly static Action<Type> _addAnchor;

		private static LinkedList<LibZReader> _containers;
		private static LinkedList<Type> _anchors;

		#endregion

		#region static constructor

		static LibZResolver()
		{
			// initialize VMT table

			var vmt = AppDomain.CurrentDomain.GetData(AssemblyResolverId) as object[];

			if (vmt == null)
			{
				_addFile = AddFile;
				_addAnchor = AddAnchor;
				_resolve = Resolve;

				vmt = new object[] 
				{
					/* 0 */ null,
					/* 1 */ _addFile, 
					/* 2 */ _addAnchor,
					/* 3 */ _resolve,
				};

				AppDomain.CurrentDomain.SetData(AssemblyResolverId, vmt);
				AppDomain.CurrentDomain.AssemblyResolve += (s, e) => _resolve(e);
			}
			else
			{
				// attaching to first LibZResolver
				_lock = vmt[0];
				_addFile = vmt[1] as Action<Stream>;
				_addAnchor = vmt[2] as Action<Type>;
				_resolve = vmt[3] as Func<ResolveEventArgs, Assembly>;
			}

			// intialize paths

			var assembly = Assembly.GetEntryAssembly() ?? typeof(LibZResolver).Assembly;
			s_ExecutableFolder = Path.GetDirectoryName(assembly.Location);
			var paths = new List<string>();

			paths.Add(s_ExecutableFolder);
			string systemPathVariable = Environment.GetEnvironmentVariable("PATH");
			if (systemPathVariable != null)
			{
				paths.AddRange(systemPathVariable.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)));
			}

			_searchPaths = paths.ToArray();
		}

		#endregion

		#region engine

		private static void AddFile(Stream stream)
		{
			if (_containers == null) _containers = new LinkedList<LibZReader>();
			var container = new LibZReader(stream);
			if (_containers.Any(c => c.ContainerId == container.ContainerId)) return;
			_containers.AddLast(container);
		}

		private static void AddAnchor(Type type)
		{
			if (_anchors == null) _anchors = new LinkedList<Type>();
			if (_anchors.Contains(type)) return;
			_anchors.AddLast(type);
		}

		private static Assembly Resolve(ResolveEventArgs args)
		{
			if (_containers == null) return null;
			var fullName = args.Name.ToLower();
			var guid = LibZReader.CreateHash(fullName);

			foreach (var container in _containers)
			{
				if (container.HasEntry(guid))
				{
					return Assembly.Load(container.GetBytes(guid));
				}
			}

			return null;
		}

		#endregion

		#region public interface

		public static void Initialize()
		{
			// do nothing, but trigger static constructor
			while (false) ; // supress "no implementation" warning
		}

		public static void RegisterS

		public static void RegisterFile(string libzFileName, bool optional = true)
		{
			libzFileName = FindFile(libzFileName);

			if (libzFileName == null)
			{
				if (optional) return;
				throw new FileNotFoundException("LibZ library cannot be found", libzFileName);
			}

			try
			{
				var stream = File.Open(libzFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
				_addFile(stream);
			}
			catch
			{
				if (optional) return;
				throw;
			}
		}

		public static void RegisterFiles(string folder, string libzFilePattern, bool optional = true)
		{
			var folderInfo = new DirectoryInfo(Path.Combine(s_ExecutableFolder, folder));
			if (!folderInfo.Exists) return;
			foreach (var file in folderInfo.GetFiles(libzFilePattern))
			{
				RegisterFile(file.FullName, optional);
			}
		}

		public static void RegisterDecoder(string codec, Action<byte[], byte[]> decoder, bool overwrite = false)
		{
			LibZReader.RegisterDecoder(codec, decoder, overwrite);
		}

		#endregion

		#region private implementation

		private static string FindFile(string libzFileName)
		{
			if (Path.IsPathRooted(libzFileName))
			{
				return File.Exists(libzFileName) ? libzFileName : null;
			}

			foreach (var candidate in _searchPaths)
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
		protected enum EntryFlags: int
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

		private readonly static MD5 _md5 = MD5CryptoServiceProvider.Create();

		#endregion

		#region fields

		private readonly static Dictionary<uint, Action<byte[], byte[]>> _decoders
				= new Dictionary<uint, Action<byte[], byte[]>>();

		protected Guid _containerId = Guid.Empty;
		protected long _magicOffset;
		protected int _version;
		protected Dictionary<Guid, Entry> _entries = new Dictionary<Guid, Entry>();

		protected Stream _stream;
		protected BinaryReader _reader;

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

		public static void RegisterDecoder(string codec, Action<byte[], byte[]> decoder, bool overwrite = false)
		{
			if (String.IsNullOrEmpty(codec))
				throw new ArgumentException("codec is null or empty.", "codec");
			if (decoder == null)
				throw new ArgumentNullException("decoder", "decoder is null.");

			var codecId = StringToCodec(codec);

			if (overwrite)
			{
				_decoders[codecId] = decoder;
			}
			else
			{
				try
				{
					_decoders.Add(codecId, decoder);
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
			Action<byte[], byte[]> decoder;
			if (!_decoders.TryGetValue(codec, out decoder))
				throw new ArgumentException(string.Format("Unknown codec id '{0}'", codec));

			var result = new byte[outputLength];
			decoder(data, result);
			return result;
		}

		#endregion

		#region read

		private Entry ReadEntry()
		{
			lock (_stream)
			{
				var entry = new Entry
				{
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

		private byte[] ReadData(Entry entry, string password = null)
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

		public byte[] GetBytes(Guid hash, string password = null)
		{
			return ReadData(_entries[hash], password);
		}

		public byte[] GetBytes(string resourceName, string password = null)
		{
			return GetBytes(CreateHash(resourceName), password);
		}

		public bool HasEntry(Guid hash) { return _entries.ContainsKey(hash); }
		public bool HasEntry(string resourceName) { return HasEntry(CreateHash(resourceName)); }

		#endregion

		#region utility

		public static Guid CreateHash(string resourceName)
		{
			return new Guid(_md5.ComputeHash(Encoding.UTF8.GetBytes(resourceName)));
		}

		protected static byte[] Isolate(byte[] buffer, int index = 0, int length = int.MaxValue)
		{
			if (buffer == null) return null;
			if (index == 0 && length >= buffer.Length) return buffer;
			length = Math.Min(length, buffer.Length - index);
			var result = new byte[length];
			System.Buffer.BlockCopy(buffer, index, result, 0, length);
			return buffer;
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
		/// Indicates if object has been already disposed.
		/// </summary>
		protected bool m_Disposed;

		/// <summary>
		/// Releases unmanaged resources and performs other cleanup operations before the
		/// <see cref="TemporaryFile"/> is reclaimed by garbage collection.
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
			if (!m_Disposed)
			{
				if (disposing)
					DisposeManaged();
				DisposeUnmanaged();
			}
			m_Disposed = true;
		}

		/// <summary>
		/// Disposes the managed resources.
		/// </summary>
		protected virtual void DisposeManaged()
		{
			Clear();
		}

		protected static void TryDispose<T>(ref T subject) where T: class, IDisposable
		{
			if (object.ReferenceEquals(subject, null)) return;
			subject.Dispose();
			subject = null;
		}

		/// <summary>
		/// Disposes the unmanaged resources.
		/// </summary>
		protected virtual void DisposeUnmanaged()
		{
			/* unmanaged resources */
		}

		#endregion
	}

	#endregion

	#region namespace Internal

	namespace Internal
	{
		#region class IsolatingStream

		public class IsolatingStream: Stream
		{
			private readonly Stream m_InnerStream;

			public IsolatingStream(Stream other) { m_InnerStream = other; }
			public override bool CanRead { get { return m_InnerStream.CanRead; } }
			public override bool CanSeek { get { return m_InnerStream.CanSeek; } }
			public override bool CanWrite { get { return m_InnerStream.CanWrite; } }
			public override bool CanTimeout { get { return m_InnerStream.CanTimeout; } }
			public override void Flush() { m_InnerStream.Flush(); }
			public override long Length { get { return m_InnerStream.Length; } }

			public override long Position
			{
				get { return m_InnerStream.Position; }
				set { m_InnerStream.Position = value; }
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				return m_InnerStream.Read(buffer, offset, count);
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				return m_InnerStream.Seek(offset, origin);
			}

			public override void SetLength(long value)
			{
				m_InnerStream.SetLength(value);
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				m_InnerStream.Write(buffer, offset, count);
			}

			public override int ReadTimeout
			{
				get { return m_InnerStream.ReadTimeout; }
				set { m_InnerStream.ReadTimeout = value; }
			}

			public override int WriteTimeout
			{
				get { return m_InnerStream.WriteTimeout; }
				set { m_InnerStream.WriteTimeout = value; }
			}

			public override int ReadByte()
			{
				return m_InnerStream.ReadByte();
			}

			public override void WriteByte(byte value)
			{
				m_InnerStream.WriteByte(value);
			}

			public override void Close()
			{
				m_InnerStream.Flush();
			}
		}

		#endregion

		#region class Crc32

		public class Crc32
		{
			private static uint[] s_Crc32Table;

			public static uint Compute(byte[] bytes)
			{
				uint crc = 0xffffffff;
				for (int i = 0; i < bytes.Length; ++i)
				{
					byte index = (byte)(((crc) & 0xff) ^ bytes[i]);
					crc = (uint)((crc >> 8) ^ s_Crc32Table[index]);
				}
				return ~crc;
			}

			static Crc32()
			{
				uint poly = 0xedb88320;
				s_Crc32Table = new uint[256];
				uint temp = 0;
				for (uint i = 0; i < s_Crc32Table.Length; ++i)
				{
					temp = i;
					for (int j = 8; j > 0; --j)
					{
						temp = (uint)((temp & 1) == 1 ? (uint)((temp >> 1) ^ poly) : temp >> 1);
					}
					s_Crc32Table[i] = temp;
				}
			}
		}

		#endregion
	}

	#endregion
}
