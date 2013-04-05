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

		#region static constructor

		static LibZContainer()
		{
			RegisterEncoder("deflate", DeflateEncoder);
		}

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

		#region initialization

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

		public static void RegisterCodec(
			string codecName, 
			Func<byte[], byte[]> encoder, Func<byte[], int, byte[]> decoder, 
			bool overwrite = false)
		{
			RegisterDecoder(codecName, decoder, overwrite);
			RegisterEncoder(codecName, encoder, overwrite);
		}

		public static void RegisterEncoder(
			string codecName, 
			Func<byte[], byte[]> encoder, 
			bool overwrite = false)
		{
			if (string.IsNullOrEmpty(codecName)) throw new ArgumentException("codecName is null or empty.");
			if (encoder == null) throw new ArgumentNullException("encoder");

			var crc = HashProvider.CRC(codecName);

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
					throw new ArgumentException(string.Format("Cannot register codecName '{0}'", codecName), e);
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
					throw new ArgumentException(string.Format("Unknown codecName {0}", codec));
			}
			return encoder(data);
		}

		private static byte[] DeflateEncoder(byte[] input)
		{
			using (var istream = new MemoryStream(input))
			using (var ostream = new MemoryStream())
			{
				using (var zstream = new DeflateStream(ostream, CompressionMode.Compress))
				{
					istream.CopyTo(zstream);
				}
				return ostream.ToArray();
			}
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
