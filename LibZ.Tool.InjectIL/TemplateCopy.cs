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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Mono.Collections.Generic;

namespace LibZ.Tool.InjectIL
{
	/// <summary>
	/// Helper class copying a class from one assembly (actually the tree) to another.
	/// Please note, initially LibZ was not going to instrument code. This "instrumentation"
	/// is a an effect of 2-day crash-diving into world of IL manipulation and it's very
	/// messy. But it works (I hope, at least).
	/// Lot of those methods use some simplifications which are good enough for LibZ
	/// but wouldn't stand against 'real' assembly merging.
	/// </summary>
	public class TemplateCopy
	{
		#region static fields

		/// <summary>The Instruction class does not expose all constructors. 
		/// Unfortunatelly I need the one which is not exposed.</summary>
		private static readonly ConstructorInfo InstructionConstructorInfo =
			typeof(Instruction).GetConstructor(
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				new[] {typeof(OpCode), typeof(object)},
				null);

		#endregion

		#region fields

		/// <summary>The 'from' assembly.</summary>
		private readonly AssemblyDefinition _from;

		/// <summary>The 'into' assembly.</summary>
		private readonly AssemblyDefinition _into;

		/// <summary>The queue of actions.</summary>
		private readonly List<Tuple<Action, Exception>> _queue =
			new List<Tuple<Action, Exception>>();

		/// <summary>The set of types which has been cloned.</summary>
		private readonly HashSet<string> _clonedTypes = new HashSet<string>();

		/// <summary>The flag indicating if existing classes with conflicting
		/// names should be overwritten.</summary>
		private readonly bool _overwrite;

		#endregion

		#region constructor

		/// <summary>Initializes a new instance of the <see cref="TemplateCopy"/> class.</summary>
		/// <param name="from">From.</param>
		/// <param name="into">The into.</param>
		/// <param name="type">The type.</param>
		/// <param name="overwrite">if set to <c>true</c> existing classes with conflicting
		/// names will be overwritten.</param>
		protected TemplateCopy(
			AssemblyDefinition from, AssemblyDefinition into, TypeReference type,
			bool overwrite)
		{
			_from = from;
			_into = into;
			_overwrite = overwrite;
			Enqueue(() => FindOrCloneType(type.Resolve(), _overwrite));
		}

		#endregion

		#region public interface

		/// <summary>Runs the specified from.</summary>
		/// <param name="from">From.</param>
		/// <param name="into">The into.</param>
		/// <param name="type">The type.</param>
		/// <param name="overwrite">if set to <c>true</c> existing classes with conflicting
		/// names will be overwritten.</param>
		/// <returns>A collection of exceptions.</returns>
		public static IEnumerable<Exception> Run(
			AssemblyDefinition from, AssemblyDefinition into, TypeReference type,
			bool overwrite)
		{
			var worker = new TemplateCopy(from, into, type, overwrite);
			worker.Run();
			return worker.GetExceptions();
		}

		#endregion

		#region private implementation

		/// <summary>Runs the merge.</summary>
		/// <exception cref="System.AggregateException">Aggregates all exceptions.</exception>
		protected void Run()
		{
			var streak = 0;
			while (_queue.Count > 0 && streak < _queue.Count)
			{
				var action = Dequeue();
				try
				{
					action();
					streak = 0;
				}
				catch (Exception e)
				{
					Enqueue(action, e);
					streak++;
				}
			}
			if (streak > 0)
			{
				throw new AggregateException(GetExceptions());
			}
		}

		/// <summary>Injects the action on the top of queue.</summary>
		/// <param name="action">The action.</param>
		/// <param name="exception">The exception.</param>
		private void Inject(Action action, Exception exception = null)
		{
			_queue.Insert(0, Tuple.Create(action, exception));
		}

		/// <summary>Enqueues the specified action.</summary>
		/// <param name="action">The action.</param>
		/// <param name="exception">The exception.</param>
		private void Enqueue(Action action, Exception exception = null)
		{
			_queue.Add(Tuple.Create(action, exception));
		}

		/// <summary>Dequeues the action.</summary>
		/// <returns>Action for the top of the queue.</returns>
		private Action Dequeue()
		{
			var result = _queue[0].Item1;
			_queue.RemoveAt(0);
			return result;
		}

		/// <summary>Gets the exceptions.</summary>
		/// <returns>Collection of exceptions.</returns>
		private IEnumerable<Exception> GetExceptions()
		{
			return _queue.Select(i => i.Item2).ToArray();
		}


		/// <summary>Checks if type reference belongs to given assembly.</summary>
		/// <param name="typeref">The type reference.</param>
		/// <param name="assembly">The assembly.</param>
		/// <returns><c>true</c> if type belongs to given assembly; <c>false</c> otherwise</returns>
		private static bool BelongsTo(TypeReference typeref, AssemblyDefinition assembly)
		{
			return typeref.Resolve().Module.Assembly.FullName == assembly.FullName;
		}

		/// <summary>Resolves the specified type reference. Clones the type if needed.</summary>
		/// <param name="typeref">The type reference.</param>
		/// <returns>New type reference.</returns>
		private TypeReference Resolve(TypeReference typeref)
		{
			if (typeref == null) return null;

			return
				BelongsTo(typeref, _into) ? typeref :
					BelongsTo(typeref, _from) ? FindOrCloneType(typeref.Resolve(), _overwrite) :
						_into.MainModule.Import(typeref);
		}

		/// <summary>Resolves the specified method reference. 
		/// Clones the declaring type if needed.</summary>
		/// <param name="methodref">The method reference.</param>
		/// <returns>New method reference.</returns>
		private object Resolve(MethodReference methodref)
		{
			if (methodref == null) return null;

			if (BelongsTo(methodref.DeclaringType, _into))
			{
				return methodref;
			}
			if (BelongsTo(methodref.DeclaringType, _from))
			{
				var targetType = Resolve(methodref.DeclaringType);
				var sourceMethod = methodref.Resolve();
				return targetType.Resolve().Methods.Single(m => IsMatch(m, sourceMethod));
			}
			_into.MainModule.Import(methodref.DeclaringType);
			return _into.MainModule.Import(methodref);
		}

		/// <summary>Determines whether the specified target method matches 
		/// signature of source method.</summary>
		/// <param name="targetMethod">The target method.</param>
		/// <param name="sourceMethod">The source method.</param>
		/// <returns><c>true</c> if the specified target method is match; otherwise, <c>false</c>.</returns>
		private static bool IsMatch(MethodReference targetMethod, MethodReference sourceMethod)
		{
			if (sourceMethod.FullName != targetMethod.FullName) return false;
			if (sourceMethod.ReturnType.FullName != targetMethod.ReturnType.FullName) return false;
			if (sourceMethod.Parameters.Count != targetMethod.Parameters.Count) return false;

			for (var i = 0; i < sourceMethod.Parameters.Count; i++)
			{
				if (sourceMethod.Parameters[i].ParameterType.FullName != targetMethod.Parameters[i].ParameterType.FullName)
					return false;
			}

			return true;
		}

		/// <summary>Resolves the specified field reference. 
		/// Clones decaring type if needed.</summary>
		/// <param name="fieldref">The field reference.</param>
		/// <returns>New field reference.</returns>
		private FieldReference Resolve(FieldReference fieldref)
		{
			if (fieldref == null) return null;

			if (BelongsTo(fieldref.DeclaringType, _into))
			{
				return fieldref;
			}
			if (BelongsTo(fieldref.DeclaringType, _from))
			{
				var targetType = Resolve(fieldref.DeclaringType);
				return targetType.Resolve().Fields.Single(f => f.FullName == fieldref.FullName);
			}
			_into.MainModule.Import(fieldref.DeclaringType);
			return _into.MainModule.Import(fieldref);
		}

		/// <summary>Resolves any refernce.</summary>
		/// <param name="reference">The reference.</param>
		/// <returns>New reference (or not).</returns>
		private object ResolveAny(object reference)
		{
			if (reference == null) return null;

			var t = reference as TypeReference;
			if (t != null) return Resolve(t);

			var m = reference as MethodReference;
			if (m != null) return Resolve(m);

			var f = reference as FieldReference;
			if (f != null) return Resolve(f);

			return reference;
		}

		/// <summary>Finds the type.</summary>
		/// <param name="fullName">The full name.</param>
		/// <returns></returns>
		private TypeReference FindType(string fullName)
		{
			return _into.MainModule.Types.SingleOrDefault(t => t.FullName == fullName);
		}

		/// <summary>Finds or clones the type.</summary>
		/// <param name="sourceType">Type in question.</param>
		/// <param name="overwrite">if set to <c>true</c> enforces overwritting existing type.</param>
		/// <returns>New type reference.</returns>
		private TypeReference FindOrCloneType(TypeDefinition sourceType, bool overwrite)
		{
			var typeName = sourceType.FullName;
			var found = FindType(typeName);

			if (found != null)
			{
				if (!overwrite || _clonedTypes.Contains(typeName)) return found;
				found.Module.Types.Remove(found.Resolve());
			}

			return CloneType(sourceType);
		}

		/// <summary>Clones the type.</summary>
		/// <param name="sourceType">Source type.</param>
		/// <returns>New type.</returns>
		private TypeDefinition CloneType(TypeDefinition sourceType)
		{
			var targetType = new TypeDefinition(
				sourceType.Namespace, sourceType.Name, sourceType.Attributes, Resolve(sourceType.BaseType));
			_into.MainModule.Types.Add(targetType);
			_clonedTypes.Add(sourceType.FullName);

			// TODO:MAK NestedTypes

			Inject(() => {
				// TODO:MAK Interfaces, Properties, Events
				CopyAttributes(sourceType, targetType);
				sourceType.Fields.ForEach(f => CloneField(targetType, f));
				sourceType.Methods.ForEach(m => CloneMethod(targetType, m));
			});

			return targetType;
		}

		/// <summary>Clones the field.</summary>
		/// <param name="type">The type.</param>
		/// <param name="sourceField">The source field.</param>
		private void CloneField(TypeDefinition type, FieldDefinition sourceField)
		{
			var targetField = new FieldDefinition(
				sourceField.Name, sourceField.Attributes, Resolve(sourceField.FieldType));
			CopyAttributes(sourceField, targetField);
			type.Fields.Add(targetField);
		}

		/// <summary>Clones the method.</summary>
		/// <param name="type">The type.</param>
		/// <param name="sourceMethod">The source method.</param>
		private void CloneMethod(TypeDefinition type, MethodDefinition sourceMethod)
		{
			var targetMethod = new MethodDefinition(
				sourceMethod.Name, sourceMethod.Attributes, Resolve(sourceMethod.ReturnType));
			CopyAttributes(sourceMethod, targetMethod);
			// ReSharper disable ImplicitlyCapturedClosure
			sourceMethod.Parameters.ForEach(p => CloneParameter(targetMethod, p));
			// ReSharper restore ImplicitlyCapturedClosure
			type.Methods.Add(targetMethod);
			Enqueue(() => CloneImplementation(sourceMethod, targetMethod));
		}

		/// <summary>Clones the parameter.</summary>
		/// <param name="targetMethod">The target method.</param>
		/// <param name="sourceParameter">The source parameter.</param>
		private void CloneParameter(IMethodSignature targetMethod, ParameterDefinition sourceParameter)
		{
			var targetParameter = new ParameterDefinition(
				sourceParameter.Name, sourceParameter.Attributes, Resolve(sourceParameter.ParameterType));
			CopyAttributes(sourceParameter, targetParameter);
			targetMethod.Parameters.Add(targetParameter);
		}

		/// <summary>Clones the implementation.</summary>
		/// <param name="sourceMethod">The source method.</param>
		/// <param name="targetMethod">The target method.</param>
		/// <exception cref="System.InvalidOperationException">Cannot merge P/Invoke methods</exception>
		private void CloneImplementation(MethodDefinition sourceMethod, MethodDefinition targetMethod)
		{
			if (sourceMethod.IsPInvokeImpl)
				throw new InvalidOperationException("Cannot merge P/Invoke methods");

			foreach (var sourceVariable in sourceMethod.Body.Variables)
			{
				var targetVariable = new VariableDefinition(
					sourceVariable.Name, Resolve(sourceVariable.VariableType));
				targetMethod.Body.Variables.Add(targetVariable);
			}

			var il = targetMethod.Body.GetILProcessor();

			foreach (var sourceInstruction in sourceMethod.Body.Instructions)
			{
				var targetInstruction = CloneInstruction(sourceInstruction);
				il.Append(targetInstruction);
			}

			foreach (var sourceHandler in sourceMethod.Body.ExceptionHandlers)
			{
				var targetHandler = new ExceptionHandler(sourceHandler.HandlerType) {
					CatchType = Resolve(sourceHandler.CatchType)
				};

				CopyExceptionHandler(
					sourceHandler, targetHandler,
					sourceMethod.Body.Instructions, targetMethod.Body.Instructions);

				targetMethod.Body.ExceptionHandlers.Add(targetHandler);
			}

			targetMethod.Body.OptimizeMacros();
		}

		/// <summary>Clones the instruction.</summary>
		/// <param name="sourceInstruction">The source instruction.</param>
		/// <returns>Cloned instruction.</returns>
		private Instruction CloneInstruction(Instruction sourceInstruction)
		{
			return (Instruction)InstructionConstructorInfo.Invoke(new[] {
				sourceInstruction.OpCode, ResolveAny(sourceInstruction.Operand)
			});
		}

		/// <summary>Copies the exception handler.</summary>
		/// <param name="sourceHandler">The source handler.</param>
		/// <param name="targetHandler">The target handler.</param>
		/// <param name="sourceIL">The source IL.</param>
		/// <param name="targetIL">The target IL.</param>
		private static void CopyExceptionHandler(
			ExceptionHandler sourceHandler, ExceptionHandler targetHandler,
			Collection<Instruction> sourceIL,
			Collection<Instruction> targetIL)
		{
			if (sourceHandler.TryStart != null)
			{
				targetHandler.TryStart = targetIL[sourceIL.IndexOf(sourceHandler.TryStart)];
			}

			if (sourceHandler.TryEnd != null)
			{
				targetHandler.TryEnd = targetIL[sourceIL.IndexOf(sourceHandler.TryEnd)];
			}

			if (sourceHandler.HandlerStart != null)
			{
				targetHandler.HandlerStart = targetIL[sourceIL.IndexOf(sourceHandler.HandlerStart)];
			}

			if (sourceHandler.HandlerEnd != null)
			{
				targetHandler.HandlerEnd = targetIL[sourceIL.IndexOf(sourceHandler.HandlerEnd)];
			}

			if (sourceHandler.FilterStart != null)
			{
				targetHandler.FilterStart = targetIL[sourceIL.IndexOf(sourceHandler.FilterStart)];
			}
		}

		/// <summary>Copies the parameter attributes. 
		/// NOTE: Not implemented for now because types we clone don't have any.</summary>
		/// <param name="sourceParameter">The source parameter.</param>
		/// <param name="targetParameter">The target parameter.</param>
		private void CopyAttributes(ParameterDefinition sourceParameter, ParameterDefinition targetParameter) { }

		/// <summary>Copies the type attributes.
		/// NOTE: Not implemented for now because types we clone don't have any.</summary>
		/// <param name="sourceType">Type of the source.</param>
		/// <param name="targetType">Type of the target.</param>
		private void CopyAttributes(TypeDefinition sourceType, TypeDefinition targetType) { }

		/// <summary>Copies the field attributes.
		/// NOTE: Not implemented for now because types we clone don't have any.</summary>
		/// <param name="sourceField">The source field.</param>
		/// <param name="targetField">The target field.</param>
		private void CopyAttributes(FieldDefinition sourceField, FieldDefinition targetField) { }

		/// <summary>Copies the method attributes.
		/// NOTE: Not implemented for now because types we clone don't have any.</summary>
		/// <param name="sourceMethod">The source method.</param>
		/// <param name="targetMethod">The target method.</param>
		private void CopyAttributes(MethodDefinition sourceMethod, MethodDefinition targetMethod) { }

		#endregion
	}
}
