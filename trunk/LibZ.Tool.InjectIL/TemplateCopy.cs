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
	public class TemplateCopy
	{
		#region static fields

		private static readonly ConstructorInfo InstructionConstructorInfo =
			typeof (Instruction).GetConstructor(
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				new[] {typeof (OpCode), typeof (object)},
				null);

		#endregion

		#region fields

		private readonly AssemblyDefinition _from;
		private readonly AssemblyDefinition _into;

		private readonly List<Tuple<Action, Exception>> _queue =
			new List<Tuple<Action, Exception>>();

		private readonly HashSet<string> _clonedTypes = new HashSet<string>();

		private readonly bool _overwrite;

		#endregion

		#region constructor

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

		private void Inject(Action action, Exception exception = null)
		{
			_queue.Insert(0, Tuple.Create(action, exception));
		}

		private void Enqueue(Action action, Exception exception = null)
		{
			_queue.Add(Tuple.Create(action, exception));
		}

		private Action Dequeue()
		{
			var result = _queue[0].Item1;
			_queue.RemoveAt(0);
			return result;
		}

		private IEnumerable<Exception> GetExceptions()
		{
			return _queue.Select(i => i.Item2).ToArray();
		}


		private static bool BelongsTo(TypeReference typeref, AssemblyDefinition assembly)
		{
			return typeref.Resolve().Module.Assembly.FullName == assembly.FullName;
		}

		private TypeReference Resolve(TypeReference typeref)
		{
			if (typeref == null) return null;

			return
				BelongsTo(typeref, _into) ? typeref :
					BelongsTo(typeref, _from) ? FindOrCloneType(typeref.Resolve(), _overwrite) :
						_into.MainModule.Import(typeref);
		}

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

		private object Resolve(FieldReference fieldref)
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

		private void CloneField(TypeDefinition type, FieldDefinition sourceField)
		{
			var targetField = new FieldDefinition(
				sourceField.Name, sourceField.Attributes, Resolve(sourceField.FieldType));
			CopyAttributes(sourceField, targetField);
			type.Fields.Add(targetField);
		}

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

		private void CloneParameter(IMethodSignature targetMethod, ParameterDefinition sourceParameter)
		{
			var targetParameter = new ParameterDefinition(
				sourceParameter.Name, sourceParameter.Attributes, Resolve(sourceParameter.ParameterType));
			CopyAttributes(sourceParameter, targetParameter);
			targetMethod.Parameters.Add(targetParameter);
		}

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

		private Instruction CloneInstruction(Instruction sourceInstruction)
		{
			return (Instruction)InstructionConstructorInfo.Invoke(new[] {
				sourceInstruction.OpCode, ResolveAny(sourceInstruction.Operand)
			});
		}

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

		private TypeReference FindType(string fullName)
		{
			return _into.MainModule.Types.SingleOrDefault(t => t.FullName == fullName);
		}

		private void CopyAttributes(ParameterDefinition sourceParameter, ParameterDefinition targetParameter) { }

		private void CopyAttributes(TypeDefinition sourceType, TypeDefinition targetType) { }

		private void CopyAttributes(FieldDefinition sourceField, FieldDefinition targetField) { }

		private void CopyAttributes(MethodDefinition sourceMethod, MethodDefinition targetMethod) { }

		#endregion
	}
}
