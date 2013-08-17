using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.Diagnostics;
using LibZ.Bootstrap;
using LibZ.Manager;
using LibZ.Tool.Interfaces;
using ManyConsole;

namespace LibZ.Tool
{
	/// <summary>Main class.</summary>
	public class Program
	{
		#region fields

		[ImportMany(typeof(ICodec))] private readonly List<ICodec> _codecs = new List<ICodec>();

		#endregion

		#region private implementation

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
			if (_codecs == null) return;

			foreach (var codec in _codecs)
			{
				Log.Info("Registering codec '{0}'", codec.Name);
				codec.Initialize();
				LibZContainer.RegisterCodec(codec.Name, codec.Encode, codec.Decode, true);
			}
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

		private void WriteAppInfo()
		{
			var version = GetType().Assembly.GetName().Version;
			Console.WriteLine();
			Console.WriteLine("LibZ {0}, Copyright (c) 2013 Milosz Krajewski", version);
			Console.WriteLine("https://libz.codeplex.com/");
			Console.WriteLine();
		}

		#endregion
	}
}
