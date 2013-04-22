using System;
using System.Threading;

namespace LibZ.Injected
{
	/// <summary>
	/// Class to be injected into target assembly.
	/// </summary>
	internal class LibZInitializer
	{
		/// <summary>The initialized flag.</summary>
		private static int _initialized;

		static LibZInitializer()
		{
			InitializeAsmZ();
			InitializeLibZ();
		}

		public static void Initialize()
		{
			Interlocked.CompareExchange(ref _initialized, 1, 0);
		}

		private static void InitializeAsmZ()
		{
			Console.WriteLine("InitializeAsmZ");
		}

		private static void InitializeLibZ()
		{
			Console.WriteLine("InitializeLibZ");
		}
	}
}
