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

/*
 * NOTE: Define HIJACK_LIBZ if you need LibZResolver for startup, and LibZ library is inside .libz file.
 * Otherwise (you use LibZ for resolving assemblies only) you do not have to care.
 * In most applications just include this file in your .exe, that's all.
 */

#if HIJACK_LIBZ
namespace Hijack
#else
namespace Softpark
#endif
{
	namespace LibZ
	{
		using Internal;

		#region class LibZResolver

		public class LibZResolver
		{
			#region consts

			private const string AssemblyResolverId = "345A8453-0528-4356-8A89-EB8599791CBA";

			#endregion

			#region fields

			private readonly static string s_ExecutableFolder;
			private readonly static string[] s_SearchPaths;

			private readonly static object m_Lock;
			private readonly static Action<Stream> m_AddFile;
			private readonly static Func<ResolveEventArgs, Assembly> m_Resolve;
			private readonly static Action<Type> m_AddAnchor;

			private static LinkedList<LibZReader> m_Containers;
			private static LinkedList<Type> m_Anchors;

			#endregion

			#region static constructor

			static LibZResolver()
			{
				// initialize VMT table

				var vmt = AppDomain.CurrentDomain.GetData(AssemblyResolverId) as object[];

				if (vmt == null)
				{
					m_Lock = new object();
					m_AddFile = AddFile;
					m_AddAnchor = AddAnchor;
					m_Resolve = Resolve;

					vmt = new object[] 
				{
					/* 0 */ m_Lock,
					/* 1 */ m_AddFile, 
					/* 2 */ m_AddAnchor,
					/* 3 */ m_Resolve,
				};

					AppDomain.CurrentDomain.SetData(AssemblyResolverId, vmt);
					AppDomain.CurrentDomain.AssemblyResolve += (s, e) => m_Resolve(e);
				}
				else
				{
					m_Lock = vmt[0];
					m_AddFile = vmt[1] as Action<Stream>;
					m_AddAnchor = vmt[2] as Action<Type>;
					m_Resolve = vmt[3] as Func<ResolveEventArgs, Assembly>;
				}

				// intialize paths

				var assembly = Assembly.GetEntryAssembly() ?? typeof(LibZResolver).Assembly;
				s_ExecutableFolder = Path.GetDirectoryName(assembly.Location);
				var paths = new List<string>();

				paths.Add(s_ExecutableFolder);
				paths.Add(Environment.CurrentDirectory);
				string systemPathVariable = Environment.GetEnvironmentVariable("PATH");
				if (systemPathVariable != null)
				{
					paths.AddRange(systemPathVariable.Split(';').Where(p => !string.IsNullOrWhiteSpace(p)));
				}

				s_SearchPaths = paths.ToArray();
			}

			#endregion

			#region engine

			private static void AddFile(Stream stream)
			{
				if (m_Containers == null) m_Containers = new LinkedList<LibZReader>();
				var container = new LibZReader(stream);
				if (m_Containers.Any(c => c.ContainerId == container.ContainerId)) return;
				m_Containers.AddLast(container);
			}

			private static void AddAnchor(Type type)
			{
				if (m_Anchors == null) m_Anchors = new LinkedList<Type>();
				if (m_Anchors.Contains(type)) return;
				m_Anchors.AddLast(type);
			}

			private static Assembly Resolve(ResolveEventArgs args)
			{
				if (m_Containers == null) return null;
				var fullName = args.Name.ToLower();
				var guid = LibZReader.CreateHash(fullName);

				foreach (var container in m_Containers)
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

			public static void Register(string libzFileName, bool optional = true)
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
					m_AddFile(stream);
				}
				catch
				{
					if (optional) return;
					throw;
				}
			}

			public static void Register(string folder, string libzFilePattern, bool optional = true)
			{
				try
				{
					folder = Path.Combine(s_ExecutableFolder, folder);
				}
				catch
				{
					// assembly may not be found, it may fail, but we still can try to resolve against current folder
					while (false) ;
				}

				if (!Directory.Exists(folder)) return;
				foreach (var file in new DirectoryInfo(folder).GetFiles(libzFilePattern))
				{
					Register(file.FullName, optional);
				}
			}

			public static void RegisterDecoder(uint codec, Func<byte[], byte[]> decoder)
			{
				LibZReader.RegisterDecoder(codec, decoder);
			}

			public static void RegisterDecoder(string codec, Func<byte[], byte[]> decoder)
			{
				LibZReader.RegisterDecoder(codec, decoder);
			}

			#endregion

			#region private implementation

			private static string FindFile(string libzFileName)
			{
				if (Path.IsPathRooted(libzFileName))
				{
					return File.Exists(libzFileName) ? libzFileName : null;
				}

				foreach (var candidate in s_SearchPaths)
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
				Compressed = 0x01, // this is reserved for old 'deflate' approach
				Encrypted = 0x02,
				Doboz = 0x04,
				Huffman = 0x08,
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

			protected static readonly Guid m_MagicNumber = new Guid("F434A49F-859F-40AB-A460-C87270575249");
			protected static readonly int m_GuidLength = Guid.Empty.ToByteArray().Length; // that's nasty, but reliable
			protected static readonly int m_CurrentVersion = 102;
			protected const int m_CopyBufferLength = 0x4000;

			#endregion

			#region fields

			private readonly static Dictionary<uint, Func<byte[], byte[]>> m_Decoders
				= new Dictionary<uint, Func<byte[], byte[]>>();

			protected Guid m_ContainerId = Guid.Empty;
			protected long m_MagicOffset;
			protected int m_Version;
			protected Dictionary<Guid, Entry> m_Entries = new Dictionary<Guid, Entry>();

			protected Stream m_Stream;
			protected BinaryReader m_Reader;

			#endregion

			#region properties

			public Guid ContainerId
			{
				get { return m_ContainerId; }
			}

			#endregion

			#region constructor

			protected LibZReader()
			{
			}

			public LibZReader(Stream stream)
			{
				m_Stream = stream;
				m_Reader = new BinaryReader(m_Stream);
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
				lock (m_Stream)
				{
					m_Stream.Position = 0;
					var guid = new Guid(m_Reader.ReadBytes(m_GuidLength));
					if (guid != m_MagicNumber)
						throw new ArgumentException("Invalid LibZ file header");
					m_ContainerId = new Guid(m_Reader.ReadBytes(m_GuidLength));
					m_Version = m_Reader.ReadInt32();
					if (m_Version != m_CurrentVersion)
						throw new NotSupportedException(string.Format("Unsupported LibZ file version ({0})", m_Version));
					m_Stream.Position = m_Stream.Length - m_GuidLength - sizeof(long);
					m_MagicOffset = m_Reader.ReadInt64();
					guid = new Guid(m_Reader.ReadBytes(m_GuidLength));
					if (guid != m_MagicNumber)
						throw new ArgumentException("Invalid LibZ file footer");
					m_Stream.Position = m_MagicOffset;
					int count = m_Reader.ReadInt32();
					for (int i = 0; i < count; i++)
					{
						var entry = ReadEntry();
						m_Entries.Add(entry.Hash, entry);
					}
				}
			}

			#endregion

			#region codec management

			public static void RegisterDecoder(uint codec, Func<byte[], byte[]> decoder)
			{
				if (decoder == null)
					throw new ArgumentNullException("decoder", "decoder is null.");

				try
				{
					m_Decoders.Add(codec, decoder);
				}
				catch (ArgumentException e)
				{
					throw new ArgumentException(string.Format("Codec {0} already registered", codec), e);
				}
			}

			public static void RegisterDecoder(string codec, Func<byte[], byte[]> decoder)
			{
				if (String.IsNullOrEmpty(codec))
					throw new ArgumentException("codec is null or empty.", "codec");

				try
				{
					RegisterDecoder(StringToCodec(codec), decoder);
				}
				catch (ArgumentException e)
				{
					throw new ArgumentException(string.Format("Cannot register codec '{0}'", codec), e);
				}
			}

			public static uint StringToCodec(string codec)
			{
				uint result = Crc32.Compute(Encoding.UTF8.GetBytes(codec));
				if (result != 0) return result;

				throw new ArgumentException(string.Format("Invalid codec name '{0}', cannot produce valid CRC", codec));
			}

			public static byte[] Decode(uint codec, byte[] data)
			{
				if (codec == 0) return data;
				Func<byte[], byte[]> decoder;
				if (!m_Decoders.TryGetValue(codec, out decoder))
					throw new ArgumentException(string.Format("Unknown codec {0}", codec));
				return decoder(data);
			}

			#endregion

			#region read

			private Entry ReadEntry()
			{
				lock (m_Stream)
				{
					var entry = new Entry
					{
						Hash = new Guid(m_Reader.ReadBytes(m_GuidLength)),
						Flags = (EntryFlags)m_Reader.ReadInt32(),
						Offset = m_Reader.ReadInt64(),
						OriginalLength = m_Reader.ReadInt32(),
						StorageLength = m_Reader.ReadInt32(),
						Codec = m_Reader.ReadUInt32(),
					};
					return entry;
				}
			}

			private byte[] ReadData(Entry entry, string password = null)
			{
				byte[] buffer;

				lock (m_Stream)
				{
					m_Stream.Position = entry.Offset;

					var compressed = (entry.Flags & EntryFlags.Compressed) != 0;
					var encrypted = (entry.Flags & EntryFlags.Encrypted) != 0;

					using (var isolated = Isolate(m_Stream))
					using (var decrypted = Decrypt(isolated, password, encrypted))
					using (var inflated = Inflate(decrypted, compressed))
					{
						buffer = ReadBytes(inflated, entry.OriginalLength);
					}
				}

				// this needs to be outside lock!
				buffer = Decode(entry.Codec, buffer);

				return buffer;
			}

			#endregion

			#region access

			public byte[] GetBytes(Guid hash, string password = null)
			{
				return ReadData(m_Entries[hash], password);
			}

			public byte[] GetBytes(string resourceName, string password = null)
			{
				return GetBytes(CreateHash(resourceName), password);
			}

			public bool HasEntry(Guid hash)
			{
				return m_Entries.ContainsKey(hash);
			}

			public bool HasEntry(string resourceName)
			{
				return HasEntry(CreateHash(resourceName));
			}

			#endregion

			#region utility

			public static Guid CreateHash(string resourceName)
			{
				return new Guid(MD5CryptoServiceProvider.Create().ComputeHash(Encoding.UTF8.GetBytes(resourceName)));
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
				TryDispose(ref m_Reader);
				TryDispose(ref m_Stream);
				m_Entries = null;
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

			#region stream encapsulation

			protected static Stream Isolate(Stream other, bool isolate = true)
			{
				return (!isolate || other is IsolatingStream)
					? other
					: new IsolatingStream(other);
			}

			protected static Stream Decrypt(Stream other, string password = null, bool decrypt = true)
			{
				return (password == null || !decrypt)
					? other
					: new PasswordStream(other, password, CryptoStreamMode.Read);
			}

			protected static Stream Inflate(Stream other, bool decompress = true)
			{
				return !decompress
					? other
					: new DeflateStream(other, CompressionMode.Decompress);
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

				public IsolatingStream(Stream other)
				{
					m_InnerStream = other;
				}

				public override bool CanRead
				{
					get { return m_InnerStream.CanRead; }
				}

				public override bool CanSeek
				{
					get { return m_InnerStream.CanSeek; }
				}

				public override bool CanWrite
				{
					get { return m_InnerStream.CanWrite; }
				}

				public override void Flush()
				{
					m_InnerStream.Flush();
				}

				public override long Length
				{
					get { return m_InnerStream.Length; }
				}

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

				public override bool CanTimeout
				{
					get { return m_InnerStream.CanTimeout; }
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

			#region class PasswordStream

			/// <summary>Provides simple access to encrypted files. It uses RC2 with 40bit key. Read notes below.</summary>
			/// <example><code>
			/// using (FileStream fileStream = new FileStream("c:\\file.ext"))
			/// using (PasswordStream stream = new PasswordStream(fileStream, "NoMoreSecrets", CryptoStreamMode.Write))
			/// {
			///   StreamWriter writer = new StreamWriter(stream);
			///   writer.WriteLine("Confidential information");
			/// }
			/// </code></example>
			public class PasswordStream: CryptoStream
			{
				#region constructor

				/// <summary>
				/// Initializes a new instance of the <see cref="PasswordStream"/> class.
				/// Creates encrypted stream over existing stream.
				/// </summary>
				/// <param name="stream">The stream.</param>
				/// <param name="password">The password. Password is used to generate encryption key.</param>
				/// <param name="mode">Stream mode (Read or Write).</param>
				public PasswordStream(Stream stream, string password, CryptoStreamMode mode)
					: base(stream, BuildTransform(password, mode), mode)
				{
				}

				#endregion

				#region Key & IV

				private static byte[] ExtendHash(byte[] password, int keyBytes, int ivBytes)
				{
					if (password.Length >= keyBytes + ivBytes)
						return password;
					throw new NotImplementedException("Hash extension has not been implemented");
				}

				private static SymmetricAlgorithm SetupAlgorithm(
						byte[] password,
						SymmetricAlgorithm encryptionAlgorithm,
						int keyBytes, int ivBytes)
				{
					password = ExtendHash(password, keyBytes, ivBytes);

					byte[] key = new byte[keyBytes];
					byte[] iv = new byte[ivBytes];

					Array.Copy(password, 0, key, 0, keyBytes);
					Array.Copy(password, keyBytes, iv, 0, ivBytes);
					encryptionAlgorithm.Key = key;
					encryptionAlgorithm.IV = iv;

					return encryptionAlgorithm;
				}

				private static SymmetricAlgorithm SetupAlgorithm(
					string password,
					SymmetricAlgorithm encryptionAlgorithm,
					HashAlgorithm hashAlgorithm,
					int keyBytes, int ivBytes)
				{
					return SetupAlgorithm(
						hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(password)),
						encryptionAlgorithm,
						keyBytes, ivBytes);
				}

				/*

				private static SymmetricAlgorithm SetupRC2_40(string password)
				{
					return SetupAlgorithm(
						password,
						RC2CryptoServiceProvider.Create(),
						MD5CryptoServiceProvider.Create(),
						5, // 40bit
						8 //64bit
					);
				}

				private static SymmetricAlgorithm SetupDES_56(string password)
				{
					return SetupAlgorithm(
						password,
						DESCryptoServiceProvider.Create(),
						MD5CryptoServiceProvider.Create(),
						8, // 64bit
						8 // 64bit
					);
				}
			
				*/

				private static SymmetricAlgorithm SetupAES_128(string password)
				{
					return SetupAlgorithm(
						password,
						RijndaelManaged.Create(),
						SHA256Managed.Create(),
						16, // 128bit
						16 // 128bit
					);
				}

				#endregion

				#region utility

				private static ICryptoTransform BuildTransform(string password, CryptoStreamMode mode)
				{
					var algorithm = SetupAES_128(password);
					// SymmetricAlgorithm algorithm = SetupRC2_40(password);

					switch (mode)
					{
						case CryptoStreamMode.Read:
							return algorithm.CreateDecryptor();
						case CryptoStreamMode.Write:
							return algorithm.CreateEncryptor();
						default:
							throw new ArgumentException();
					}
				}

				#endregion
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
}
