using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LibZ.Tool.ClassInjector;
using LibZ.Tool.InjectIL;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	public class InstrumentTask: TaskBase
	{
		private InstrumentHelper _instrumentHelper;

		public void Execute(
			string mainFileName,
			bool allAsmZResources,
			bool allLibZresources,
			ICollection<string> libzFiles,
			ICollection<string> libzFolders)
		{
			var inject = 
				allAsmZResources || 
				allLibZresources || 
				libzFiles.Count > 0 || 
				libzFolders.Count > 0;

			var targetAssembly = LoadAssembly(mainFileName);

			_instrumentHelper = new InstrumentHelper(targetAssembly);
			_instrumentHelper.InjectLibZInitializer();

			if (allAsmZResources)
			{
				_instrumentHelper.InjectAsmZResolver();
			}
		}
	}
}
