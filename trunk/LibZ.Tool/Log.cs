using System;

namespace LibZ.Tool
{
	public enum LogLevel
	{
		Debug,
		Info,
		Warn,
		Error,
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
