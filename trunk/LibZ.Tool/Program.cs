using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.ComponentModel.Composition.Hosting;
using System.ComponentModel.Composition.Primitives;
using System.IO;
using System.Linq;
using System.Reflection;
using LibZ.Manager;
using LibZ.Tool.Interfaces;
using ManyConsole;

namespace LibZ.Tool
{
	public class Program
	{
		// ReSharper disable FieldCanBeMadeReadOnly.Local
		[ImportMany(typeof(ICodec))]
		private List<ICodec> _codecs = new List<ICodec>();
		// ReSharper restore FieldCanBeMadeReadOnly.Local

		private void LoadPlugins()
		{
			var dllCatalog = new DirectoryCatalog(".");
			var pluginsCatalog = LibZResolver.RegisterContainers(".", "*.libzcodec").Catalog;
			var catalog = new AggregateCatalog(dllCatalog, libzCatalog);
			var container = new CompositionContainer(catalog);
			container.SatisfyImportsOnce(this);
		}

		public void RegisterPlugins()
		{
			if (_codecs == null) return;

			foreach (var codec in _codecs)
			{
				Log.Info("Registering codec '{0}'", codec.Name);
				codec.Initialize();
				LibZContainer.RegisterCodec(codec.Name, codec.Encode, codec.Decode);
			}
		}

		public int Run(string[] args)
		{
			try
			{
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
	}
}