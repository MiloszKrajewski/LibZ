#region License

/*
 * Copyright (c) 2013-2014, Milosz Krajewski
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

using System.Collections.Generic;
using LibZ.Tool.Tasks;
using ManyConsole;

namespace LibZ.Tool.Commands
{
	/// <summary>
	/// Instrument LibZ initialization command.
	/// </summary>
	public class InstrumentLibZCommand: ConsoleCommand
	{
		#region fields

		private string _mainFileName;
		private bool _allLibZResources;
		private string _keyFileName;
		private string _keyFilePassword;
		private readonly List<string> _libzFiles = new List<string>();
		private readonly List<string> _libzPatterns = new List<string>();

		#endregion

		#region constructor

		/// <summary>
		/// Initializes a new instance of the <see cref="InstrumentLibZCommand"/> class.
		/// </summary>
		public InstrumentLibZCommand()
		{
			IsCommand("instrument", "Instruments assembly with initialization code");
			HasRequiredOption("a|assembly=", "assembly to be instrumented", s => _mainFileName = s);
			HasOption("libz-resources", "registers embedded LibZ container on startup", _ => _allLibZResources = true);
			HasOption("libz-file=", "registers file on startup", s => _libzFiles.Add(s));
			HasOption("libz-pattern=", "registers multiple files on startup (wildcards)", s => _libzPatterns.Add(s));
			HasOption("k|key=", "key file name", s => _keyFileName = s);
			HasOption("p|password=", "password for password protected key file", s => _keyFilePassword = s);
		}

		#endregion

		#region public interface

		/// <summary>Runs the command.</summary>
		/// <param name="remainingArguments">The remaining arguments.</param>
		/// <returns>Return code.</returns>
		public override int Run(string[] remainingArguments)
		{
			var task = new InstrumentLibZTask();
			task.Execute(
				_mainFileName,
				_allLibZResources, _libzFiles.ToArray(), _libzPatterns.ToArray(),
				_keyFileName, _keyFilePassword);

			return 0;
		}

		#endregion
	}
}
