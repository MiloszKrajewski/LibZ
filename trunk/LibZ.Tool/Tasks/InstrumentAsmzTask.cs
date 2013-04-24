using System.IO;
using LibZ.Tool.InjectIL;

namespace LibZ.Tool.Tasks
{
	public class InstrumentAsmZTask: TaskBase
	{
		private InstrumentHelper _instrumentHelper;

		public void Execute(
			string mainFileName,
			string keyFileName, string keyFilePassword)
		{
			if (!File.Exists(mainFileName)) throw FileNotFound(mainFileName);

			var targetAssembly = LoadAssembly(mainFileName);
			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);

			_instrumentHelper = new InstrumentHelper(targetAssembly);
			_instrumentHelper.InjectLibZInitializer();
			_instrumentHelper.InjectAsmZResolver();

			SaveAssembly(targetAssembly, mainFileName, keyPair);
		}
	}
}
