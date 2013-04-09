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
	class LibZCatalog: ComposablePartCatalog
	{
		private readonly List<TypeCatalog> _catalogs = new List<TypeCatalog>();
		private readonly List<ComposablePartDefinition> _parts = new List<ComposablePartDefinition>();
		private readonly List<string> _names;

		public LibZCatalog(string libzFileName, bool optional = true)
		{
			try
			{
				if (!File.Exists(libzFileName))
					throw new FileNotFoundException(string.Format("File '{0}' could not be found", libzFileName));
				var container = new LibZContainer(libzFileName);
				_names = container.GetAssemblyNames().ToList();
				LibZResolver.RegisterContainer(libzFileName);

				foreach (var name in _names)
				{
					var assembly = Assembly.Load(name);
					var types = assembly.GetTypes();
					_catalogs.Add(new TypeCatalog(types));
				}
			}
			catch
			{
				if (!optional) throw;
			}
		}

		public override IQueryable<ComposablePartDefinition> Parts
		{
			get { return _parts.AsQueryable(); }
		}

		public override IEnumerable<Tuple<ComposablePartDefinition, ExportDefinition>> GetExports(ImportDefinition definition)
		{
			return _catalogs.SelectMany(c => c.GetExports(definition));
		}
	}

	public class Program
	{
		// ReSharper disable FieldCanBeMadeReadOnly.Local
		[ImportMany(typeof(ICodec))]
		private List<ICodec> _codecs = new List<ICodec>();
		// ReSharper restore FieldCanBeMadeReadOnly.Local

		private void LoadPlugins()
		{
			var dllCatalog = new DirectoryCatalog(".");
			var libzCatalog = new LibZCatalog("LibZ.ZLib.plugin");
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