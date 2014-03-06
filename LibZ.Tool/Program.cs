using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.IO;
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
				ErrorStream = true
			};
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
			Console.WriteLine(@"LibZ {0}, Copyright (c) 2013 Milosz Krajewski", version);
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

				var commands = ConsoleCommandDispatcher.FindCommandsInSameAssemblyAs(GetType());
				return ConsoleCommandDispatcher.DispatchCommand(commands, args, Console.Out);
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