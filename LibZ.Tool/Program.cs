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

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
using System.Linq;
using System.Reflection;
using LibZ.Bootstrap;
using LibZ.Manager;
using LibZ.Tool.Interfaces;
using ManyConsole;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace LibZ.Tool
{
	/// <summary>Main class.</summary>
	public class Program
	{
		#region consts

		/// <summary>Logger for this class.</summary>
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		#endregion

		#region fields

		[ImportMany(typeof(ICodec))] private readonly List<ICodec> _codecs = new List<ICodec>();

		#endregion

		#region private implementation

		private static void ConfigureLogging()
		{
			var config = new LoggingConfiguration();

			var console = new ColoredConsoleTarget {
				Name = "console",
				Layout = "${message}",
				UseDefaultRowHighlightingRules = true,
				ErrorStream = true,
			};
			console.RowHighlightingRules.Add(
				new ConsoleRowHighlightingRule("level == LogLevel.Trace", ConsoleOutputColor.DarkGray, ConsoleOutputColor.NoChange));
			console.RowHighlightingRules.Add(
				new ConsoleRowHighlightingRule("level == LogLevel.Debug", ConsoleOutputColor.Gray, ConsoleOutputColor.NoChange));
			console.RowHighlightingRules.Add(
				new ConsoleRowHighlightingRule("level == LogLevel.Info", ConsoleOutputColor.Cyan, ConsoleOutputColor.NoChange));
			console.RowHighlightingRules.Add(
				new ConsoleRowHighlightingRule("level == LogLevel.Warn", ConsoleOutputColor.Yellow, ConsoleOutputColor.NoChange));
			console.RowHighlightingRules.Add(
				new ConsoleRowHighlightingRule("level == LogLevel.Error", ConsoleOutputColor.Red, ConsoleOutputColor.NoChange));
			console.RowHighlightingRules.Add(
				new ConsoleRowHighlightingRule("level == LogLevel.Fatal", ConsoleOutputColor.Magenta, ConsoleOutputColor.NoChange));
			config.AddTarget("console", console);

			var product = GetEntryAssemblyAttribute<AssemblyProductAttribute>().Product;
			var folder = Path.Combine(
				Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
				product);
			var fileName = string.Format("{0}.log", product);

			Directory.CreateDirectory(folder);

			var file = new FileTarget {
				Name = "file",
				FileName = Path.Combine(folder, fileName),
				Layout = "${date:format=yyyyMMdd.HHmmss} ${threadid}> [${level}] (${logger}) ${message}",
				ArchiveEvery = FileArchivePeriod.Day,
				ArchiveNumbering = ArchiveNumberingMode.Rolling,
				MaxArchiveFiles = 7,
			};

			config.LoggingRules.Add(new LoggingRule("*", LogLevel.Debug, console));
			config.LoggingRules.Add(new LoggingRule("*", LogLevel.Trace, file));

			LogManager.Configuration = config;
		}

		/// <summary>Loads the plugins.</summary>
		private void LoadPlugins()
		{
			var catalog =
				new AggregateCatalog(
					LibZResolver.GetCatalogs(
						LibZResolver.RegisterMultipleFileContainers(".\\*.libzcodec")));

			using (catalog)
			{
				var container = new CompositionContainer(catalog);
				container.SatisfyImportsOnce(this);
			}
		}

		/// <summary>Registers the plugins.</summary>
		private void RegisterPlugins()
		{
			if (_codecs == null)
				return;

			foreach (var codec in _codecs)
			{
				Log.Info("Registering codec '{0}'", codec.Name);
				codec.Initialize();
				LibZContainer.RegisterCodec(codec.Name, codec.Encode, codec.Decode, true);
			}
		}

		private static T GetEntryAssemblyAttribute<T>()
			where T: Attribute
		{
			return ((T)Attribute.GetCustomAttribute(Assembly.GetEntryAssembly(), typeof(T), false));
		}

		private static void WriteAppInfo()
		{
			var version = Assembly.GetEntryAssembly().GetName().Version;
			Console.WriteLine();
			Console.WriteLine(@"LibZ {0}, Copyright (c) 2013-2014, Milosz Krajewski", version);
			Console.WriteLine(@"https://libz.codeplex.com/");
			Console.WriteLine();
		}

		#endregion

		#region public interface

		/// <summary>Runs the application.</summary>
		/// <param name="args">The arguments.</param>
		/// <returns>Result code.</returns>
		public int Run(string[] args)
		{
			try
			{
				WriteAppInfo();

				LibZContainer.RegisterEncoder("deflate", DefaultCodecs.DeflateEncoder, true);
				LibZContainer.RegisterCodec("zlib", DefaultCodecs.ZLibEncoder, DefaultCodecs.ZLibDecoder);

				ConfigureLogging();
				LoadPlugins();
				RegisterPlugins();

				try
				{
					Log.Trace("LibZ started");
					var commands = ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(GetType());
					return ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);
				}
				finally
				{
					Log.Trace("LibZ stopped");
				}
			}
			catch (Exception e)
			{
				Log.Error("{0}: {1}", e.GetType().Name, e.Message);
				Log.Debug(e.StackTrace);
				return 1;
			}
		}

		#endregion
	}
}