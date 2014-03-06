using System.Threading;

namespace LibZ.Injected
{
	/// <summary>
	/// Class to be injected into target assembly.
	/// </summary>
	internal class LibZInitializer
	{
		#region static fields

		/// <summary>The initialized flag.</summary>
		private static int _initialized;

		#endregion

		#region public interface

		/// <summary>Initializes resolvers.</summary>
		public static void Initialize()
		{
			if (Interlocked.CompareExchange(ref _initialized, 1, 0) != 0) return;
			InitializeAsmZ();
			InitializeLibZ();
			InitializeDecoders();
		}

		#endregion

		#region private implementation

		/// <summary>Initializes the AsmZResolver.</summary>
		private static void InitializeAsmZ()
		{
			// this method is going to be populated after being injected into target assembly
		}

		/// <summary>Initializes the LibZ resolver.</summary>
		private static void InitializeLibZ()
		{
			// this method is going to be populated after being injected into target assembly
		}

		private static void InitializeDecoders()
		{
			// this method is going to be populated after being injected into target assembly
		}

		#endregion
	}
}
