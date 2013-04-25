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
using LibZ.Manager.Internal;

namespace LibZ.Manager
{

	#region class LibZContainer

	/// <summary>Class for reading and writing LibZ containers.</summary>
	public class LibZContainer: LibZReader
	{
		#region fields

		/// <summary>The encoder dictionary.</summary>
		private static readonly Dictionary<string, Func<byte[], byte[]>> Encoders
			= new Dictionary<string, Func<byte[], byte[]>>();

		/// <summary>The file writer.</summary>
		private BinaryWriter _writer;

		/// <summary>The dirty flag.</summary>
		protected bool _dirty;

		#endregion

		#region constructor

		/// <summary>Static contstructor.</summary>
		static LibZContainer()
		{
			RegisterEncoder("deflate", DeflateEncoder);
		}

		/// <summary>Initializes a new instance of the <see cref="LibZContainer"/> class.</summary>
		/// <param name="stream">The stream.</param>
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

		/// <summary>Initializes a new instance of the <see cref="LibZContainer"/> class.</summary>
		/// <param name="fileName">Name of the file.</param>
		/// <param name="writable">if set to <c>true</c> container will be writable.</param>
		/// <param name="reset">if set to <c>true</c> resets the container (removes all the data).</param>
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

		/// <summary>Creates the empty file.</summary>
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

		#region public interface

		/// <summary>Appends the assembly into container.</summary>
		/// <param name="assemblyInfo">The assembly info.</param>
		/// <param name="options">The options.</param>
		public void Append(
			AssemblyInfo assemblyInfo,
			AppendOptions options)
		{
			var flags = LibZEntry.EntryFlags.None;
			if (assemblyInfo.Unmanaged) flags |= LibZEntry.EntryFlags.Unmanaged;
			if (assemblyInfo.AnyCPU) flags |= LibZEntry.EntryFlags.AnyCPU;
			if (assemblyInfo.AMD64) flags |= LibZEntry.EntryFlags.AMD64;

			var platformId =
				assemblyInfo.AnyCPU ? string.Empty :
					assemblyInfo.AMD64 ? "x64:" :
						"x86:";
			var assemblyName = assemblyInfo.AssemblyName;
			var bytes = assemblyInfo.Bytes;

			SetBytes(
				platformId + assemblyName.FullName,
				assemblyName,
				bytes,
				flags,
				options);
		}

		/// <summary>Registers the codec.</summary>
		/// <param name="codecName">Name of the codec.</param>
		/// <param name="encoder">The encoder.</param>
		/// <param name="decoder">The decoder.</param>
		/// <param name="overwrite">if set to <c>true</c> overwrites previously 
		/// registered codec.</param>
		public static void RegisterCodec(
			string codecName,
			Func<byte[], byte[]> encoder, Func<byte[], int, byte[]> decoder,
			bool overwrite = false)
		{
			RegisterDecoder(codecName, decoder, overwrite);
			RegisterEncoder(codecName, encoder, overwrite);
		}

		/// <summary>Registers the encoder.</summary>
		/// <param name="codecName">Name of the codec.</param>
		/// <param name="encoder">The encoder.</param>
		/// <param name="overwrite">if set to <c>true</c> overwrites previously 
		/// registered encoder.</param>
		public static void RegisterEncoder(
			string codecName,
			Func<byte[], byte[]> encoder,
			bool overwrite = false)
		{
			if (string.IsNullOrEmpty(codecName)) throw new ArgumentException("codecName is null or empty.");
			if (encoder == null) throw new ArgumentNullException("encoder");

			if (overwrite)
			{
				lock (Encoders) Encoders[codecName] = encoder;
			}
			else
			{
				try
				{
					lock (Encoders) Encoders.Add(codecName, encoder);
				}
				catch (ArgumentException e)
				{
					throw new ArgumentException(string.Format("Cannot register codecName '{0}'", codecName), e);
				}
			}
		}

		/// <summary>Commits all changes.</summary>
		public void Commit()
		{
			lock (_stream)
			{
				_stream.Position = _magicOffset;
				WriteTail();
			}
		}

		/// <summary>Saves container to new file.</summary>
		/// <param name="fileName">Name of the file.</param>
		public void SaveAs(string fileName)
		{
			using (var stream = new FileStream(fileName, FileMode.Create))
			using (var writer = new BinaryWriter(stream))
			{
				WriteHeadTo(writer, Guid.NewGuid());
				var newEntries = new List<LibZEntry>();
				foreach (var oldEntry in _entries.Values.OrderBy(e => e.Offset))
				{
					byte[] buffer;
					lock (_stream)
					{
						_stream.Position = oldEntry.Offset;
						buffer = _reader.ReadBytes(oldEntry.StorageLength);
					}
					var newEntry = new LibZEntry(oldEntry) {Offset = stream.Position};
					writer.Write(buffer);
					newEntries.Add(newEntry);
				}
				WriteTailTo(writer, newEntries);
			}
		}

		#endregion

		#region private implementation

		#region codec management

		/// <summary>Encodes the data with specified codec.</summary>
		/// <param name="codecName">Name of the codec.</param>
		/// <param name="data">The data.</param>
		/// <returns>Encoded data.</returns>
		private static byte[] Encode(string codecName, byte[] data)
		{
			if (string.IsNullOrEmpty(codecName)) return data;
			Func<byte[], byte[]> encoder;
			lock (Encoders)
			{
				if (!Encoders.TryGetValue(codecName, out encoder))
					throw new ArgumentException(string.Format("Unknown codecName {0}", codecName));
			}
			return encoder(data);
		}

		/// <summary>Encoder for 'deflate' codec.</summary>
		/// <param name="input">The input data.</param>
		/// <returns>Encoded data.</returns>
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

		/// <summary>Writes the head of file.</summary>
		private void WriteHead()
		{
			lock (_stream)
			{
				_writer.Flush();
				_stream.Position = 0;
				WriteHeadTo(_writer, _containerId);
				_writer.Flush();
			}
		}

		/// <summary>Writes the tail of file. Truncates the file.</summary>
		private void WriteTail()
		{
			lock (_stream)
			{
				_writer.Flush();
				_stream.Position = _magicOffset;
				WriteTailTo(_writer, _entries.Values);
				_writer.Flush();
				_stream.SetLength(_stream.Position);
				_dirty = false;
			}
		}

		/// <summary>Writes the head to.</summary>
		/// <param name="writer">The writer.</param>
		/// <param name="containerId">The container id.</param>
		private static void WriteHeadTo(BinaryWriter writer, Guid containerId)
		{
			writer.Write(Magic.ToByteArray());
			writer.Write(containerId.ToByteArray());
			writer.Write(CurrentVersion);
		}

		/// <summary>Writes the entry to.</summary>
		/// <param name="writer">The writer.</param>
		/// <param name="entry">The entry.</param>
		private static void WriteEntryTo(BinaryWriter writer, LibZEntry entry)
		{
			writer.Write(entry.Hash.ToByteArray());
			writer.Write(entry.AssemblyName.FullName);
			writer.Write((int)entry.Flags);
			writer.Write(entry.Offset);
			writer.Write(entry.OriginalLength);
			writer.Write(entry.StorageLength);
			writer.Write(entry.CodecName ?? string.Empty);
		}

		/// <summary>Writes the tail to.</summary>
		/// <param name="writer">The writer.</param>
		/// <param name="entries">The entries.</param>
		private static void WriteTailTo(BinaryWriter writer, ICollection<LibZEntry> entries)
		{
			var magicOffset = writer.BaseStream.Position;
			writer.Write(entries.Count);
			foreach (var entry in entries) WriteEntryTo(writer, entry);
			writer.Write(magicOffset);
			writer.Write(Magic.ToByteArray());
		}

		/// <summary>Saves entry to file.</summary>
		/// <param name="resourceName">Name of the resource.</param>
		/// <param name="assemblyName">Name of the assembly.</param>
		/// <param name="data">The content of the assembly.</param>
		/// <param name="flags">The flags.</param>
		/// <param name="options">The options.</param>
		private void SetBytes(
			string resourceName,
			AssemblyName assemblyName, byte[] data, LibZEntry.EntryFlags flags, AppendOptions options)
		{
			var codecName = options.CodecName;

			lock (_stream)
			{
				// prepare entry
				var entry = new LibZEntry {
					Hash = Hash.MD5(resourceName),
					AssemblyName = assemblyName,
					Offset = _magicOffset,
					Flags = flags,
					OriginalLength = data.Length,
				};

				// add it to dictionary
				if (options.Overwrite)
				{
					_entries[entry.Hash] = entry;
				}
				else
				{
					_entries.Add(entry.Hash, entry);
				}

				// encode it
				var encoded = Encode(codecName, data);
				if (encoded != null)
				{
					entry.CodecName = codecName;
					data = encoded;
				}

				// write it
				_magicOffset += entry.StorageLength = WriteBytes(_stream, _magicOffset, data);
				_stream.SetLength(_stream.Position);

				_dirty = true;
			}
		}

		/// <summary>Writes buffer to stream.</summary>
		/// <param name="stream">The stream.</param>
		/// <param name="position">The position.</param>
		/// <param name="data">The data to be written.</param>
		/// <returns>Number of bytes written.</returns>
		protected static int WriteBytes(Stream stream, long position, byte[] data)
		{
			if (position >= 0) stream.Position = position;
			var length = data.Length;
			stream.Write(data, 0, length);
			return length;
		}

		#endregion

		#endregion

		#region overrides

		/// <summary>Disposes the managed resources.</summary>
		protected override void DisposeManaged()
		{
			try
			{
				if (_dirty) Commit();
			}
			finally
			{
				TryDispose(ref _writer);
				base.DisposeManaged();
			}
		}

		#endregion
	}

	#endregion
}
