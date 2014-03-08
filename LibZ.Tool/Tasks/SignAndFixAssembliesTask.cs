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
using System.Linq;
using System.Reflection;
using LibZ.Msil;
using Mono.Cecil;
using NLog;

namespace LibZ.Tool.Tasks
{
	internal class SignAndFixAssembliesTask: TaskBase
	{
		#region consts

		/// <summary>Logger for this class.</summary>
		private static readonly Logger Log = LogManager.GetCurrentClassLogger();

		#endregion

		private class AssemblyInfoReference
		{
			public AssemblyNameReference ReferencedAssemblyName { get; set; }
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

			public AssemblyNameDefinition AssemblyName
			{
				get { return Assembly.Name; }
			}

			public IEnumerable<AssemblyNameReference> ReferencedNames
			{
				get { return Assembly.Modules.SelectMany(m => m.AssemblyReferences); }
			}

			public List<AssemblyInfoReference> ReferencedBy
			{
				get { return _referencedBy; }
			}

			public List<AssemblyInfoReference> References
			{
				get { return _references; }
			}

			public bool Rewritten { get; set; }
			public bool CanBeSigned { get; set; }
		}

		private readonly List<AssemblyInfo> _assemblyInfos = new List<AssemblyInfo>();

		public void Execute(
			string keyFileName, string keyFilePassword,
			string[] includePatterns, string[] excludePatterns)
		{
			var keyPair = MsilUtilities.LoadKeyPair(keyFileName, keyFilePassword);
			var fileNames = FindFiles(includePatterns, excludePatterns).ToArray();

			foreach (var fileName in fileNames)
			{
				var assembly = MsilUtilities.LoadAssembly(fileName);
				var assemblyInfo = new AssemblyInfo {
					FileName = fileName,
					Assembly = assembly,
					CanBeSigned = MsilUtilities.IsManaged(assembly),
				};
				_assemblyInfos.Add(assemblyInfo);
			}

			ScanDependencies();
			SignAndFixAssemblies(keyPair);
		}

		private void SignAndFixAssemblies(StrongNameKeyPair keyPair)
		{
			var sorted = TopologicalSort<AssemblyInfo>.Sort(_assemblyInfos, GetReferencedAssemblies).ToArray();

			foreach (var assemblyInfo in sorted)
			{
				var needsRewrite =
					!MsilUtilities.IsSigned(assemblyInfo.Assembly) ||
						assemblyInfo.References.Any(r => r.Invalid) ||
						assemblyInfo.References.Any(r => r.Target.Rewritten);

				if (!needsRewrite)
					continue;

				if (!assemblyInfo.CanBeSigned)
				{
					Log.Warn(
						"Assembly '{0}' or one of its dependencies is unmanaged or thus it cannot be signed.",
						assemblyInfo.FileName);

					assemblyInfo.ReferencedBy
						.Select(r => r.Source).ToList()
						.ForEach(a => a.CanBeSigned = false);
				}

				MsilUtilities.SaveAssembly(assemblyInfo.Assembly, assemblyInfo.FileName, keyPair);
				assemblyInfo.Assembly = MsilUtilities.LoadAssembly(assemblyInfo.FileName);
				assemblyInfo.ReferencedBy.ForEach(r => r.Invalid = true);
				assemblyInfo.Rewritten = true;

				FixupReferencesTo(assemblyInfo);
			}
		}

		private static IEnumerable<AssemblyInfo> GetReferencedAssemblies(AssemblyInfo assemblyInfo)
		{
			return assemblyInfo.References.Select(reference => reference.Target);
		}

		private static void FixupReferencesTo(AssemblyInfo assemblyInfo)
		{
			foreach (var referenceInfo in assemblyInfo.ReferencedBy)
			{
				var referenceSource = referenceInfo.Source;
				foreach (var module in referenceSource.Assembly.Modules)
				{
					Log.Debug("Replacing reference to '{0}' with '{1}'", referenceInfo.ReferencedAssemblyName, assemblyInfo.AssemblyName);
					module.AssemblyReferences.Remove(referenceInfo.ReferencedAssemblyName);
					module.AssemblyReferences.Add(assemblyInfo.AssemblyName);

					var typeReferences = module.GetTypeReferences();
					foreach (var typeReference in typeReferences)
					{
						if (typeReference.Scope == referenceInfo.ReferencedAssemblyName)
							typeReference.Scope = assemblyInfo.AssemblyName;
					}
				}
			}
		}

		private void ScanDependencies()
		{
			var foundByLongName = new HashSet<string>();
			var foundByShortName = new HashSet<Tuple<string, string>>();
			var notFound = new HashSet<string>();

			foreach (var assemblyInfo in _assemblyInfos)
			{
				foreach (var referenceName in assemblyInfo.ReferencedNames)
				{
					var found = false;

					foreach (var otherAssemblyInfo in _assemblyInfos)
					{
						if (found)
							break;
						if (!MsilUtilities.EqualAssemblyNames(referenceName.FullName, otherAssemblyInfo.AssemblyName.FullName))
							continue;

						foundByLongName.Add(referenceName.FullName);

						var reference = new AssemblyInfoReference {
							ReferencedAssemblyName = referenceName,
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
						if (found)
							break;
						if (!MsilUtilities.EqualAssemblyNames(referenceName.Name, otherAssemblyInfo.AssemblyName.Name))
							continue;

						foundByShortName.Add(Tuple.Create(referenceName.FullName, otherAssemblyInfo.AssemblyName.FullName));

						var reference = new AssemblyInfoReference {
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
						notFound.Add(referenceName.FullName);
					}
				}
			}

			foundByLongName.OrderBy(v => v).ToList()
				.ForEach(n => Log.Info("Assembly '{0}' has been successfully resolved", n));
			foundByShortName.OrderBy(v => v.Item1).ToList()
				.ForEach(t => Log.Info("Assembly '{0}' has been resolved to '{1}' using short name", t.Item1, t.Item2));
			notFound.OrderBy(v => v).ToList()
				.ForEach(n => Log.Warn("Assembly '{0}' has not been found", n));
		}
	}
}