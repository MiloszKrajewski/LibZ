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
using System.IO;
using System.Linq;
using LibZ.Msil;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace LibZ.Tool.InjectIL
{
	/// <summary>
	///     Instrumentation helper.
	///     Please note, initially LibZ was not going to instrument code. This "instrumentation"
	///     is a an effect of 2-day crash-diving into world of IL manipulation and it's very
	///     messy. But it works (I hope, at least).
	/// </summary>
	public class InstrumentHelper
	{
		#region consts

		/// <summary>The Silverlight/portable version.</summary>
		private static readonly Version Version2050 = new Version(2, 0, 5, 0);

		/// <summary>.NET 2, 3, 3.5</summary>
		private static readonly Version Version2000 = new Version(2, 0, 0, 0);

		/// <summary>.NET 4, 4.5</summary>
		private static readonly Version Version4000 = new Version(4, 0, 0, 0);

		#endregion

		#region fields

		/// <summary>The assembly to be injected.</summary>
		private readonly AssemblyDefinition _sourceAssembly;

		private readonly byte[] _sourceAssemblyImage;

		/// <summary>The assenbly to inject into.</summary>
		private readonly AssemblyDefinition _targetAssembly;

		/// <summary>The bootstrap assembly to be referenced.</summary>
		private AssemblyDefinition _bootstrapAssembly;

		/// <summary>The bootstrap assembly image</summary>
		private readonly byte[] _bootstrapAssemblyImage;

		#endregion

		#region constructor

		/// <summary>
		///     Initializes a new instance of the <see cref="InstrumentHelper" /> class.
		/// </summary>
		/// <param name="targetAssembly">The target assembly.</param>
		/// <param name="bootstrapAssembly">The bootstrap assembly (optional).</param>
		public InstrumentHelper(
			AssemblyDefinition targetAssembly,
			AssemblyDefinition bootstrapAssembly = null)
		{
			var frameworkVersion = MsilUtilities.GetFrameworkVersion(targetAssembly);
			_sourceAssemblyImage = GetInjectedAssemblyImage(frameworkVersion);
			_sourceAssembly = MsilUtilities.LoadAssembly(_sourceAssemblyImage);

			if (bootstrapAssembly == null)
			{
				_bootstrapAssemblyImage = GetBootstrapAssemblyImage(frameworkVersion);
				_bootstrapAssembly = MsilUtilities.LoadAssembly(_bootstrapAssemblyImage);
			}
			else
			{
				_bootstrapAssembly = bootstrapAssembly;
				// TODO:MAK it should not be needed, but it would be nice if it gets populated in the future
				_bootstrapAssemblyImage = null;
			}

			if (_sourceAssembly == null || _bootstrapAssembly == null)
				throw new ArgumentException(string.Format(
					"Instrumentation assembly could not be found for framework version '{0}'", frameworkVersion));

			_targetAssembly = targetAssembly;
		}

		#endregion

		#region public interface

		/// <summary>Injects the LibZInitializer.</summary>
		public void InjectLibZInitializer()
		{
			const string typeName = "LibZ.Injected.LibZInitializer";
			var targetType = _targetAssembly.MainModule.Types.SingleOrDefault(t => t.FullName == typeName);
			if (targetType != null)
				return;

			var sourceType = _sourceAssembly.MainModule.Types.Single(t => t.FullName == typeName);

			TemplateCopy.Run(_sourceAssembly, _targetAssembly, sourceType, false);

			targetType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeName);
			var targetMethod = targetType.Methods.Single(m => m.Name == "Initialize");

			// find 'module' static constructor
			var moduleType = _targetAssembly.MainModule.Types.Single(t => t.FullName == "<Module>");
			var moduleCtor = moduleType.Methods.SingleOrDefault(m => m.Name == ".cctor");

			// if module static constructor has not been found create it
			if (moduleCtor == null)
			{
				// private hidebysig specialname rtspecialname static - at least that's what other static constructors have
				const MethodAttributes attributes =
					MethodAttributes.Private | MethodAttributes.Static |
						MethodAttributes.SpecialName | MethodAttributes.RTSpecialName |
						MethodAttributes.HideBySig;
				moduleCtor = new MethodDefinition(".cctor", attributes, _targetAssembly.MainModule.TypeSystem.Void);
				moduleCtor.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
				moduleType.Methods.Add(moduleCtor);
			}

			// whenever it was just created or existed before - inject LibZ initialization into it
			moduleCtor.Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, targetMethod));
		}

		/// <summary>Injects the AsmZ (embedded dll) resolver.</summary>
		public void InjectAsmZResolver()
		{
			const string typeLibZInitializer = "LibZ.Injected.LibZInitializer";
			var initializerType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeLibZInitializer);
			var initializerMethod = initializerType.Methods.Single(m => m.Name == "InitializeAsmZ");
			var body = initializerMethod.Body.Instructions;

			body.Clear();

			const string typeAsmZResolver = "LibZ.Injected.AsmZResolver";
			var sourceType = _sourceAssembly.MainModule.Types.Single(t => t.FullName == typeAsmZResolver);
			TemplateCopy.Run(_sourceAssembly, _targetAssembly, sourceType, false);
			var targetType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeAsmZResolver);
			var targetMethod = targetType.Methods.Single(m => m.Name == "Initialize");
			body.Add(Instruction.Create(OpCodes.Call, targetMethod));

			body.Add(Instruction.Create(OpCodes.Ret));
		}

		/// <summary>Injects the LibZ (embedded .libz) startup code.</summary>
		/// <param name="allResources">
		///     if set to <c>true</c> registers all embedded .libz resource.
		/// </param>
		/// <param name="libzFiles">The LibZ files.</param>
		/// <param name="libzPatterns">The LibZ patterns.</param>
		public void InjectLibZStartup(bool allResources, ICollection<string> libzFiles, ICollection<string> libzPatterns)
		{
			var remove = !allResources && libzFiles.Count <= 0 && libzPatterns.Count <= 0;

			const string typeLibZInitializer = "LibZ.Injected.LibZInitializer";
			var initializerType = _targetAssembly.MainModule.Types.Single(t => t.FullName == typeLibZInitializer);
			var initializerMethod = initializerType.Methods.Single(m => m.Name == "InitializeLibZ");
			var body = initializerMethod.Body.Instructions;

			body.Clear();

			if (!remove)
			{
				if (_bootstrapAssembly == null)
				{
					_bootstrapAssembly = AssemblyDefinition.ReadAssembly(
						new MemoryStream(
							Precompiled.LibZInjected40Assembly));
				}

				const string typeLibZResolver = "LibZ.Bootstrap.LibZResolver";
				var targetType = _bootstrapAssembly.MainModule.Types.Single(t => t.FullName == typeLibZResolver);

				if (allResources)
				{
					var targetMethod = targetType.Methods.Single(m => m.Name == "RegisterAllResourceContainers");
					body.Add(Instruction.Create(OpCodes.Ldtoken, initializerType));
					body.Add(Instruction.Create(OpCodes.Call, _targetAssembly.ImportMethod<Type>("GetTypeFromHandle")));
					body.Add(Instruction.Create(OpCodes.Call, _targetAssembly.MainModule.Import(targetMethod)));
					body.Add(Instruction.Create(OpCodes.Pop));
				}

				if (libzFiles.Count > 0)
				{
					var targetMethod = targetType.Methods.Single(m => m.Name == "RegisterFileContainer");
					foreach (var libzFile in libzFiles)
					{
						body.Add(Instruction.Create(OpCodes.Ldstr, libzFile));
						body.Add(Instruction.Create(OpCodes.Ldc_I4_1));
						body.Add(Instruction.Create(OpCodes.Call, _targetAssembly.MainModule.Import(targetMethod)));
						body.Add(Instruction.Create(OpCodes.Pop));
					}
				}

				if (libzPatterns.Count > 0)
				{
					var targetMethod = targetType.Methods.Single(m => m.Name == "RegisterMultipleFileContainers");
					foreach (var libzFolder in libzPatterns)
					{
						body.Add(Instruction.Create(OpCodes.Ldstr, libzFolder));
						body.Add(Instruction.Create(OpCodes.Call, _targetAssembly.MainModule.Import(targetMethod)));
						body.Add(Instruction.Create(OpCodes.Pop));
					}
				}
			}

			body.Add(Instruction.Create(OpCodes.Ret));
		}

		#endregion

		#region private implementation

		/// <summary>Gets the injected assembly.</summary>
		/// <param name="frameworkVersion">The framework version.</param>
		/// <returns>Assembly.</returns>
		public static byte[] GetInjectedAssemblyImage(Version frameworkVersion)
		{
			return
				frameworkVersion < Version2000 ? null :
					frameworkVersion == Version2050 ? null :
						frameworkVersion >= Version4000 ? Precompiled.LibZInjected40Assembly :
							frameworkVersion >= Version2000 ? Precompiled.LibZInjected35Assembly :
								null;
		}

		/// <summary>Gets the bootstrap assembly.</summary>
		/// <param name="frameworkVersion">The framework version.</param>
		/// <returns>Assembly.</returns>
		public static byte[] GetBootstrapAssemblyImage(Version frameworkVersion)
		{
			return
				frameworkVersion < Version2000 ? null :
					frameworkVersion == Version2050 ? null :
						frameworkVersion >= Version4000 ? Precompiled.LibZBootstrap40Assembly :
							frameworkVersion >= Version2000 ? Precompiled.LibZBootstrap35Assembly :
								null;
		}

		#endregion
	}
}