using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using Softpark.LibZ.Internal;

namespace Softpark.LibZ
{
	#region class LibZContainer

	public class LibZContainer: LibZReader
	{
		#region fields

		private readonly static Dictionary<uint, Func<byte[], byte[]>> m_Encoders
			= new Dictionary<uint, Func<byte[], byte[]>>();

		private BinaryWriter m_Writer;
		protected bool m_Dirty;

		#endregion

		#region constructor

		public LibZContainer(Stream stream)
		{
			m_Stream = stream;
			m_Reader = new BinaryReader(m_Stream);
			bool writable = m_Stream.CanWrite;
			m_Writer = writable ? new BinaryWriter(m_Stream) : null;

			if (m_Stream.Length == 0 && writable)
			{
				CreateFile();
			}
			else
			{
				OpenFile();
			}
		}

		public LibZContainer(string fileName, bool writable = false, bool reset = false)
		{
			var mode = writable ? (reset ? FileMode.Create : FileMode.OpenOrCreate) : FileMode.Open;
			var access = writable ? FileAccess.ReadWrite : FileAccess.Read;
			var share = writable ? FileShare.None : FileShare.Read;
			m_Stream = new FileStream(fileName, mode, access, share);
			m_Reader = new BinaryReader(m_Stream);
			m_Writer = writable ? new BinaryWriter(m_Stream) : null;

			if (m_Stream.Length == 0 && writable)
			{
				CreateFile();
			}
			else
			{
				OpenFile();
			}
		}

		#endregion

		#region initialization & finalization

		private void CreateFile()
		{
			lock (m_Stream)
			{
				m_ContainerId = Guid.NewGuid();

				m_Stream.Position = 0;
				m_Stream.SetLength(0);
				WriteHead();
				m_MagicOffset = m_Stream.Position;
				WriteTail();
				m_Dirty = false;
			}
		}

		#endregion

		#region codec management

		public static void RegisterCodec(uint codec, Func<byte[], byte[]> encoder, Func<byte[], byte[]> decoder)
		{
			RegisterDecoder(codec, decoder);
			RegisterEncoder(codec, encoder);
		}

		public static void RegisterCodec(string codec, Func<byte[], byte[]> encoder, Func<byte[], byte[]> decoder)
		{
			RegisterCodec(StringToCodec(codec), encoder, decoder);
		}

		public static void RegisterEncoder(uint codec, Func<byte[], byte[]> encoder)
		{
			if (encoder == null)
				throw new ArgumentNullException("encoder", "encoder is null.");

			try
			{
				m_Encoders.Add(codec, encoder);
			}
			catch (ArgumentException e)
			{
				throw new ArgumentException(string.Format("Codec {0} already registered", codec), e);
			}
		}

		public static void RegisterEncoder(string codec, Func<byte[], byte[]> encoder)
		{
			if (String.IsNullOrEmpty(codec))
				throw new ArgumentException("codec is null or empty.", "codec");

			try
			{
				m_Encoders.Add(StringToCodec(codec), encoder);
			}
			catch (ArgumentException e)
			{
				throw new ArgumentException(string.Format("Cannot register codec '{0}'", codec), e);
			}
		}

		private static byte[] Encode(uint codec, byte[] data)
		{
			if (codec == 0) return data;
			Func<byte[], byte[]> encoder;
			if (!m_Encoders.TryGetValue(codec, out encoder))
				throw new ArgumentException(string.Format("Unknown codec {0}", codec));
			return encoder(data);
		}

		#endregion

		#region write

		private void WriteHead()
		{
			lock (m_Stream)
			{
				m_Stream.Position = 0;
				m_Writer.Write(m_MagicNumber.ToByteArray());
				m_Writer.Write(m_ContainerId.ToByteArray());
				m_Writer.Write(m_CurrentVersion);
			}
		}

		private void WriteEntry(Entry entry)
		{
			lock (m_Stream)
			{
				m_Writer.Write(entry.Hash.ToByteArray());
				m_Writer.Write((int)entry.Flags);
				m_Writer.Write(entry.Offset);
				m_Writer.Write(entry.OriginalLength);
				m_Writer.Write(entry.StorageLength);
				m_Writer.Write(entry.Codec);
			}
		}

		private void WriteTail()
		{
			lock (m_Stream)
			{
				m_Stream.Position = m_MagicOffset;
				m_Writer.Write(m_Entries.Count);
				foreach (var entry in m_Entries.Values)
				{
					WriteEntry(entry);
				}
				m_Writer.Write(m_MagicOffset);
				m_Writer.Write(m_MagicNumber.ToByteArray());
			}
		}

		private void WriteData(
			Entry entry, byte[] data, LibZOptions options, int offset = 0, int length = int.MaxValue)
		{
			lock (m_Stream)
			{
				length = Math.Min(data.Length - offset, length);

				data = Isolate(data, offset, length);

				var encoded = Encode(options.Codec, data);
				if (encoded != null)
				{
					entry.Codec = options.Codec;
					data = encoded;
				}

				entry.OriginalLength = data.Length;

				using (var source = new MemoryStream(data))
				using (var target = new MemoryStream())
				{
					bool compression = options.Deflate;
					bool encryption = options.Encrypt;

					if (compression)
					{
						using (var isolated = Isolate(target))
						using (var compressed = Deflate(isolated, true))
						{
							CopyStream(source, compressed, length);
						}

						compression = target.Length < source.Length;
					}

					using (var isolated = Isolate(m_Stream))
					using (var encrypted = Encrypt(isolated, options.Password, encryption))
					{
						var decrypted = compression ? target : source;

						decrypted.Position = 0;
						CopyStream(decrypted, encrypted, decrypted.Length);

						if (compression) entry.Flags |= EntryFlags.Compressed;
						if (encryption) entry.Flags |= EntryFlags.Encrypted;
					}
				}
			}
		}

		public void Append(string resourceName, string fileName)
		{
			SetBytes(resourceName, File.ReadAllBytes(fileName));
		}

		public void Append(string resourceName, string fileName, LibZOptions options)
		{
			SetBytes(resourceName, File.ReadAllBytes(fileName), options);
		}

		public void Alias(string existingResourceName, string newResourceName)
		{
			var existingEntry = m_Entries[CreateHash(existingResourceName)];
			var newEntry = new Entry
			{
				Hash = CreateHash(newResourceName),
				Flags = existingEntry.Flags,
				Offset = existingEntry.Offset,
				OriginalLength = existingEntry.OriginalLength,
				StorageLength = existingEntry.StorageLength,
			};
			m_Entries.Add(newEntry.Hash, newEntry);
			m_Dirty = true;
		}

		#endregion

		#region access

		public void SetBytes(string resourceName, byte[] data, LibZOptions options, int offset = 0, int length = int.MaxValue)
		{
			lock (m_Stream)
			{

				length = Math.Min(data.Length - offset, length);
				m_Stream.Position = m_MagicOffset;
				var entry = new Entry
				{
					Hash = CreateHash(resourceName),
					Offset = m_Stream.Position,
				};
				m_Entries.Add(entry.Hash, entry);

				WriteData(entry, data, options, offset, length);

				entry.StorageLength = (int)(m_Stream.Position - entry.Offset);

				m_MagicOffset = m_Stream.Position;
				m_Stream.SetLength(m_Stream.Position);

				m_Dirty = true;
			}
		}

		public void SetBytes(string resourceName, byte[] data, int offset = 0, int length = int.MaxValue)
		{
			SetBytes(resourceName, data, LibZOptions.Default, offset, length);
		}

		#endregion

		#region overrides

		protected override void Clear()
		{
			base.Clear();
			TryDispose(ref m_Writer);
		}

		protected override void DisposeManaged()
		{
			try
			{
				if (m_Dirty)
				{
					lock (m_Stream)
					{
						m_Stream.Position = m_MagicOffset;
						WriteTail();
					}
				}
			}
			finally
			{
				Clear();
			}

			base.DisposeManaged();
		}

		#endregion

		#region copy stream

		/// <summary>
		/// Copies the stream.
		/// </summary>
		/// <param name="source">The source.</param>
		/// <param name="target">The target.</param>
		/// <param name="maximumLength">The maximum length.</param>
		/// <param name="progress">The progress callback. Can be <c>null</c>.</param>
		/// <returns>Number of bytes actually copied.</returns>
		public static void CopyStream(Stream source, Stream target, long maximumLength)
		{
			if (source == null)
				throw new ArgumentNullException("source", "source is null.");
			if (target == null)
				throw new ArgumentNullException("target", "target is null.");

			int bufferSize = (int)Math.Min((long)m_CopyBufferLength, maximumLength);

			byte[] buffer = new byte[bufferSize];
			long copied = 0;
			long left = maximumLength;

			while (left > 0)
			{
				int bytes = (int)Math.Min((long)buffer.Length, left);
				int read = source.Read(buffer, 0, bytes);
				if (read <= 0) break;
				target.Write(buffer, 0, read);
				copied += (long)read;
				left -= (long)read;
			}
		}

		#endregion

		#region stream encapsulation

		protected static Stream Encrypt(Stream other, string password = null, bool encrypt = true)
		{
			return (password == null || !encrypt)
				? other
				: new PasswordStream(other, password, CryptoStreamMode.Write);
		}

		protected static Stream Deflate(Stream other, bool compress = true)
		{
			return !compress
				? other
				: new DeflateStream(other, CompressionMode.Compress);
		}

		#endregion
	}

	#endregion
}
