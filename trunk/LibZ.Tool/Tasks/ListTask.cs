#region Header
// --------------------------------------------------------------------------------------
// LibZ.Tool.Tasks.ListTask.cs
// --------------------------------------------------------------------------------------
// 
// 
//
// Copyright (c) 2013 Sepura Plc 
//
// Sepura Confidential
//
// Created: 4/15/2013 9:13:27 AM : SEPURA/krajewskim on SEPURA1051 
// 
// --------------------------------------------------------------------------------------
#endregion

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibZ.Manager.Internal;

namespace LibZ.Tool.Tasks
{
	public class ListTask: TaskBase
	{
		public void Execute(string libzFileName)
		{
			var container = new LibZ.Manager.LibZContainer(libzFileName, false, false);
			foreach (var entry in container.Entries)
			{
				int ratio = entry.OriginalLength != 0 
					? entry.StorageLength * 100 / entry.OriginalLength 
					: 100;

				Console.WriteLine(
					"'{0}' (flags:{1}, codec:'{2}', size:{3}, compession:{4}%, id:{5})",
					entry.AssemblyName.FullName,
					GetFlagsText(entry.Flags),
					entry.CodecName ?? "<none>",
					entry.OriginalLength,
					entry.StorageLength * 100 / entry.OriginalLength,
					entry.Hash.ToString("N"));
			}
		}

		private object GetFlagsText(LibZReader.EntryFlags entryFlags)
		{
			throw new NotImplementedException();
		}
	}
}
