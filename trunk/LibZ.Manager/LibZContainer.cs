using System;
using System.Collections.Generic;
using System.IO;
using LibZ.Manager.Internal;

namespace LibZ.Manager
{
	#region class LibZContainer

	public class LibZContainer: LibZReader
	{
		#region static fields

		private readonly static Dictionary<uint, Func<byte[], byte[]>> Encoders
			= new Dictionary<uint, Func<byte[], byte[]>>();

		#endregion

		#region fields

		private BinaryWriter _writer;
		protected bool _dirty;

		#endregion

		#region constructor

		public LibZContainer(Stream stream)
		{
			_stream = stream;
			_reader = new BinaryReader(_stream);
			var writable = _stream.CanWrite;
			_writer = writable ? new BinaryWriter(_stream) : null;

			if (_stream.Length == 0 && writable)
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
			_stream = new FileStream(fileName, mode, access, share);
			_reader = new BinaryReader(_stream);
			_writer = writable ? new BinaryWriter(_stream) : null;

			if (_stream.Length == 0 && writable)
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
			lock (_stream)
			{
				_containerId = Guid.NewGuid();

				_stream.Position = 0;
				_stream.SetLength(0);
				WriteHead();
				_magicOffset = _stream.Position;
				WriteTail();
				_dirty = false;
			}
		}

		#endregion

		#region codec management

		//public static void RegisterCodec(uint codec, Func<byte[], byte[]> encoder, Func<byte[], byte[]> decoder)
		//{
		//	RegisterDecoder(codec, decoder);
		//	RegisterEncoder(codec, encoder);
		//}

		public static void RegisterCodec(string codec, Func<byte[], byte[]> encoder, Func<byte[], int, byte[]> decoder, bool overwrite = false)
		{
			RegisterDecoder(codec, decoder, overwrite);
			RegisterEncoder(codec, encoder, overwrite);
		}

		public static void RegisterEncoder(string codec, Func<byte[], byte[]> encoder, bool overwrite = false)
		{
			if (String.IsNullOrEmpty(codec))
				throw new ArgumentException("codec is null or empty.", "codec");

			var crc = HashProvider.CRC(codec);

			if (overwrite)
			{
				lock (Encoders) Encoders[crc] = encoder;
			}
			else
			{
				try
				{
					lock (Encoders) Encoders.Add(crc, encoder);
				}
				catch (ArgumentException e)
				{
					throw new ArgumentException(string.Format("Cannot register codec '{0}'", codec), e);
				}
			}
		}

		private static byte[] Encode(uint codec, byte[] data)
		{
			if (codec == 0) return data;
			Func<byte[], byte[]> encoder;
			lock (Encoders)
			{
				if (!Encoders.TryGetValue(codec, out encoder))
					throw new ArgumentException(string.Format("Unknown codec {0}", codec));
			}
			return encoder(data);
		}

		#endregion

		#region write

		private void WriteHead()
		{
			lock (_stream)
			{
				_stream.Position = 0;
				_writer.Write(Magic.ToByteArray());
				_writer.Write(_containerId.ToByteArray());
				_writer.Write(CurrentVersion);
			}
		}

		private void WriteEntry(Entry entry)
		{
			lock (_stream)
			{
				_writer.Write(entry.Hash.ToByteArray());
				_writer.Write((int)entry.Flags);
				_writer.Write(entry.Offset);
				_writer.Write(entry.OriginalLength);
				_writer.Write(entry.StorageLength);
				_writer.Write(entry.Codec);
			}
		}

		private void WriteTail()
		{
			lock (_stream)
			{
				_stream.Position = _magicOffset;
				_writer.Write(_entries.Count);
				foreach (var entry in _entries.Values)
				{
					WriteEntry(entry);
				}
				_writer.Write(_magicOffset);
				_writer.Write(Magic.ToByteArray());
			}
		}

		private void WriteData(
			Entry entry, byte[] data, uint codec)
		{
			lock (_stream)
			{
				entry.OriginalLength = data.Length;

				var encoded = Encode(codec, data);
				if (encoded != null)
				{
					entry.Codec = codec;
					data = encoded;
				}

				_stream.Write(data, 0, data.Length);
			}
		}

		public void Append(string resourceName, string fileName, string codecName = null)
		{
			SetBytes(resourceName, File.ReadAllBytes(fileName), codecName);
		}

		public void Alias(string existingResourceName, string newResourceName)
		{
			var existingEntry = _entries[HashProvider.MD5(existingResourceName)];
			var newEntry = new Entry
			{
				Hash = HashProvider.MD5(newResourceName),
				Flags = existingEntry.Flags,
				Offset = existingEntry.Offset,
				OriginalLength = existingEntry.OriginalLength,
				StorageLength = existingEntry.StorageLength,
			};
			_entries.Add(newEntry.Hash, newEntry);
			_dirty = true;
		}

		#endregion

		#region access

		public void SetBytes(string resourceName, byte[] data, string codecName = null)
		{
			var codecId = codecName == null ? 0 : HashProvider.CRC(codecName);

			lock (_stream)
			{
				_stream.Position = _magicOffset;
				var entry = new Entry
				{
					Hash = HashProvider.MD5(resourceName),
					Offset = _stream.Position,
				};
				_entries.Add(entry.Hash, entry);

				WriteData(entry, data, codecId);

				entry.StorageLength = (int)(_stream.Position - entry.Offset);

				_magicOffset = _stream.Position;
				_stream.SetLength(_stream.Position);

				_dirty = true;
			}
		}

		#endregion

		#region overrides

		protected override void Clear()
		{
			base.Clear();
			TryDispose(ref _writer);
		}

		protected override void DisposeManaged()
		{
			try
			{
				if (_dirty)
				{
					lock (_stream)
					{
						_stream.Position = _magicOffset;
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
	}

	#endregion
}
