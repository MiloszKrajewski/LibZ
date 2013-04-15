using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LibZ.Manager;

namespace LibZ.Tool.Tasks
{
	public class RebuildLibraryTask: TaskBase
	{
		public void Execute(string libzFileName)
		{
			using (var container = new LibZContainer(libzFileName))
			{
				container.SaveAs(libzFileName + ".temp");
			}
		}
	}
}
