#region Header

// --------------------------------------------------------------------------------------
// LibZ.Tool.Tasks.ListLibraryContentTask.cs
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

using System.Collections.Generic;
using System.Linq;
using LibZ.Manager;
using LibZ.Manager.Internal;

namespace LibZ.Tool.Tasks
{
	public class ListLibraryContentTask: TaskBase
	{
		public void Execute(string libzFileName)
		{
			Log.Info("Opening '{0}'", libzFileName);
			using (var container = new LibZContainer(libzFileName))
			{
				var orderedEnties = container.Entries
					.OrderBy(e => e.AssemblyName.Name)
					.ThenBy(e => e.AssemblyName.Version);

				foreach (var entry in orderedEnties)
				{
					var ratio = entry.OriginalLength != 0
						? entry.StorageLength*100/entry.OriginalLength
						: 100;

					Log.Info(entry.AssemblyName.FullName);
					Log.Debug("    flags:{0}, codec:'{1}', size:{2}, compession:{3}%, id:{4})",
						string.Join(",", GetFlagsText(entry.Flags)),
						entry.CodecName ?? "<none>",
						entry.OriginalLength,
						ratio,
						entry.Hash.ToString("N"));
				}
			}
		}

		private static IEnumerable<string> GetFlagsText(LibZEntry.EntryFlags entryFlags)
		{
			if ((entryFlags & LibZEntry.EntryFlags.Unmanaged) != 0)
				yield return "Unmanaged";

			if ((entryFlags & LibZEntry.EntryFlags.AnyCPU) != 0)
				yield return "AnyCPU";
			else if ((entryFlags & LibZEntry.EntryFlags.AMD64) != 0)
				yield return "x64";
			else
				yield return "x86";
		}
	}
}
