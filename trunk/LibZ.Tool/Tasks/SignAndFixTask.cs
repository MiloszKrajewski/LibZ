using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;

namespace LibZ.Tool.Tasks
{
	class SignAndFixTask: TaskBase
	{
		private class AssemblyInfoReference
		{
			public AssemblyInfo Source { get; set; }
			public AssemblyInfo Target { get; set; }
			public bool Invalid { get; set; }
		}

		private class AssemblyInfo
		{
			private readonly List<AssemblyInfoReference> _referencedBy = new List<AssemblyInfoReference>();
			private readonly List<AssemblyInfoReference> _references = new List<AssemblyInfoReference>();

			public string FileName { get; set; }
			public AssemblyDefinition Assembly { get; set; }
			public AssemblyNameDefinition AssemblyName { get { return Assembly.Name; } }
			public IEnumerable<AssemblyNameReference> ReferencedNames
			{
				get { return Assembly.Modules.SelectMany(m => m.AssemblyReferences); }
			}

			public List<AssemblyInfoReference> ReferencedBy { get { return _referencedBy; } }
			public List<AssemblyInfoReference> References { get { return _references; } }

			public bool Rewritten { get; set; }
		}

		private readonly List<AssemblyInfo> _assemblyInfos = new List<AssemblyInfo>();

		public void Execute(string keyFileName, string keyFilePassword, string[] patterns)
		{
			var keyPair = LoadKeyPair(keyFileName, keyFilePassword);
			var fileNames = FindFiles(patterns).ToArray();

			foreach (var fileName in fileNames)
			{
				var assembly = LoadAssembly(fileName);
				var assemblyInfo = new AssemblyInfo { FileName = fileName, Assembly = assembly, };
				_assemblyInfos.Add(assemblyInfo);
			}

			ScanDependencies();

			var sorted = TopologicalSort<AssemblyInfo>.Sort(_assemblyInfos, GetReferencedAssemblies).ToArray();

			foreach (var assemblyInfo in sorted)
			{
				var needsRewrite =
					!IsSigned(assemblyInfo.Assembly) ||
					assemblyInfo.References.Any(r => r.Invalid) ||
					assemblyInfo.References.Any(r => r.Target.Rewritten);

				if (!needsRewrite) continue;

				!!! FIX REFERENCES FROM THIS ASSEMBLY

				SaveAssembly(assemblyInfo.Assembly, assemblyInfo.FileName, keyPair);
				assemblyInfo.Assembly = LoadAssembly(assemblyInfo.FileName);
				assemblyInfo.ReferencedBy.ForEach(r => r.Invalid = true);
				assemblyInfo.Rewritten = true;
			}
		}

		private IEnumerable<AssemblyInfo> GetReferencedAssemblies(AssemblyInfo assemblyInfo)
		{
			foreach (var reference in assemblyInfo.References) yield return reference.Target;
		}

		private void ScanDependencies()
		{
			foreach (var assemblyInfo in _assemblyInfos)
			{
				foreach (var referenceName in assemblyInfo.ReferencedNames)
				{
					var found = false;

					foreach (var otherAssemblyInfo in _assemblyInfos)
					{
						if (found) break;
						if (!EqualAssemblyNames(referenceName.FullName, otherAssemblyInfo.AssemblyName.FullName)) continue;

						Log.Debug("Reference to '{0}' has been found", referenceName.FullName);

						var reference = new AssemblyInfoReference() {
							Source = assemblyInfo,
							Target = otherAssemblyInfo,
							Invalid = false,
						};

						assemblyInfo.References.Add(reference);
						otherAssemblyInfo.ReferencedBy.Add(reference);

						found = true;
					}

					foreach (var otherAssemblyInfo in _assemblyInfos)
					{
						if (found) break;
						if (!EqualAssemblyNames(referenceName.Name, otherAssemblyInfo.AssemblyName.Name)) continue;

						Log.Warn(
							"Reference to '{0}' has been resolved to '{1}' using short name",
							referenceName.FullName,
							otherAssemblyInfo.AssemblyName.FullName);

						var reference = new AssemblyInfoReference() {
							Source = assemblyInfo,
							Target = otherAssemblyInfo,
							Invalid = true,
						};

						assemblyInfo.References.Add(reference);
						otherAssemblyInfo.ReferencedBy.Add(reference);

						found = true;
					}

					if (!found)
					{
						Log.Warn("Referenced assembly '{0}' has not been found, assuming system assembly", referenceName.FullName);
					}
				}
			}
		}
	}
}
