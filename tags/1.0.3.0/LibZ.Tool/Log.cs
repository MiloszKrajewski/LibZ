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

namespace LibZ.Tool
{
	/// <summary>
	/// Log level.
	/// </summary>
	public enum LogLevel
	{
		/// <summary>The debug</summary>
		Debug,

		/// <summary>The info</summary>
		Info,

		/// <summary>The warning</summary>
		Warn,

		/// <summary>The error</summary>
		Error,

		/// <summary>The fatal error</summary>
		Fatal
	}

	/// <summary>Helper class for console applications.</summary>
	public static class Log
	{
		#region consts

		private static readonly object Lock = new object();

		#endregion

		#region private implementation

		/// <summary>Prints the specified message.</summary>
		/// <param name="level">The level.</param>
		/// <param name="message">The message.</param>
		private static void Print(LogLevel level, string message = null)
		{
			lock (Lock)
			{
				if (string.IsNullOrWhiteSpace(message))
				{
					Console.WriteLine();
				}
				else
				{
					Console.ForegroundColor = LevelToColor(level);
					Console.WriteLine(message);
					Console.ResetColor();
				}
			}
		}

		private static ConsoleColor LevelToColor(LogLevel level)
		{
			switch (level)
			{
				case LogLevel.Debug:
					return ConsoleColor.Gray;
				case LogLevel.Info:
					return ConsoleColor.Cyan;
				case LogLevel.Warn:
					return ConsoleColor.Yellow;
				case LogLevel.Error:
					return ConsoleColor.Red;
				case LogLevel.Fatal:
					return ConsoleColor.Magenta;
				default:
					return ConsoleColor.White;
			}
		}

		/// <summary>Writes the line.</summary>
		/// <param name="level">The level.</param>
		/// <param name="format">The format.</param>
		private static void WriteLine(LogLevel level, object format)
		{
			Print(level, (format ?? string.Empty).ToString());
		}

		/// <summary>Writes the line.</summary>
		/// <param name="level">The level.</param>
		/// <param name="format">The format.</param>
		/// <param name="args">The args.</param>
		private static void WriteLine(LogLevel level, string format, params object[] args)
		{
			Print(level, string.Format(format ?? String.Empty, args));
		}

		#endregion

		#region public interface

		/// <summary>Writes the line.</summary>
		public static void WriteLine()
		{
			Print(LogLevel.Debug, String.Empty);
		}

		/// <summary>Writes a debug line.</summary>
		/// <param name="format">The format.</param>
		public static void Debug(object format)
		{
			WriteLine(LogLevel.Debug, format);
		}

		/// <summary>
		/// Writes a debug line.
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The args.</param>
		public static void Debug(string format, params object[] args)
		{
			WriteLine(LogLevel.Debug, format, args);
		}

		/// <summary>Writes a info line.</summary>
		/// <param name="format">The format.</param>
		public static void Info(object format)
		{
			WriteLine(LogLevel.Info, format);
		}

		/// <summary>
		/// Writes a info line.
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The args.</param>
		public static void Info(string format, params object[] args)
		{
			WriteLine(LogLevel.Info, format, args);
		}

		/// <summary>Writes a warning line.</summary>
		/// <param name="format">The format.</param>
		public static void Warn(object format)
		{
			WriteLine(LogLevel.Warn, format);
		}

		/// <summary>
		/// Writes a warning line.
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The args.</param>
		public static void Warn(string format, params object[] args)
		{
			WriteLine(LogLevel.Warn, format, args);
		}

		/// <summary>Writes a error line.</summary>
		/// <param name="format">The format.</param>
		public static void Error(object format)
		{
			WriteLine(LogLevel.Error, format);
		}

		/// <summary>
		/// Writes a error line.
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The args.</param>
		public static void Error(string format, params object[] args)
		{
			WriteLine(LogLevel.Error, format, args);
		}

		/// <summary>Reads the line.</summary>
		public static void Pause()
		{
			Info("Press <enter>...");
			Console.ReadLine();
		}

		#endregion
	}
}
