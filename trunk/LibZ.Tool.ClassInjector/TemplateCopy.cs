using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Collections.Generic;
using System.Reflection;
using Mono.Cecil.Rocks;

namespace LibZ.Tool.ClassInjector
{
	public class TemplateCopy
	{
		private readonly AssemblyDefinition _from;
		private readonly AssemblyDefinition _into;

		private readonly List<Tuple<Action, Exception>> _queue = 
			new List<Tuple<Action, Exception>>();

		public TemplateCopy(AssemblyDefinition from, AssemblyDefinition into, TypeReference type)
		{
			_from = from;
			_into = into;
			Enqueue(() => CloneType(type.Resolve()));
		}

		public void Run()
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

		private Action Enqueue(Action action, Exception exception = null)
		{
			_queue.Add(Tuple.Create(action, exception));
			return action;
		}

		private Action Dequeue()
		{
			var result = _queue[0].Item1;
			_queue.RemoveAt(0);
			return result;
		}

		public IEnumerable<Exception> GetExceptions()
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

			if (BelongsTo(typeref, _into))
			{
				return typeref;
			}
			else if (BelongsTo(typeref, _from))
			{
				var sourceType = typeref.Resolve();
				var type = FindType(sourceType.FullName) ?? CloneType(sourceType);
				return type;
			}
			else
			{
				return _into.MainModule.Import(typeref);
			}
		}

		private object Resolve(MethodReference methodref)
		{
			if (methodref == null) return null;

			if (BelongsTo(methodref.DeclaringType, _into))
			{
				return methodref;
			}
			else if (BelongsTo(methodref.DeclaringType, _from))
			{
				var targetType = Resolve(methodref.DeclaringType);
				var sourceMethod = methodref.Resolve();
				return targetType.Resolve().Methods.Single(m => IsMatch(m, sourceMethod));
			}
			else
			{
				_into.MainModule.Import(methodref.DeclaringType);
				return _into.MainModule.Import(methodref);
			}
		}

		private static bool IsMatch(MethodDefinition targetMethod, MethodDefinition sourceMethod)
		{
			if (sourceMethod.FullName != targetMethod.FullName) return false;
			if (sourceMethod.ReturnType.FullName != targetMethod.ReturnType.FullName) return false;
			if (sourceMethod.Parameters.Count != targetMethod.Parameters.Count) return false;

			for (int i = 0; i < sourceMethod.Parameters.Count; i++)
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
			else if (BelongsTo(fieldref.DeclaringType, _from))
			{
				var targetType = Resolve(fieldref.DeclaringType);
				return targetType.Resolve().Fields.Single(f => f.FullName == fieldref.FullName);
			}
			else
			{
				_into.MainModule.Import(fieldref.DeclaringType);
				return _into.MainModule.Import(fieldref);
			}
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

		private TypeDefinition CloneType(TypeDefinition sourceType)
		{
			var targetType = new TypeDefinition(
				sourceType.Namespace, sourceType.Name, sourceType.Attributes, Resolve(sourceType.BaseType));
			CopyAttributes(sourceType, targetType);
			_into.MainModule.Types.Add(targetType);

			// TODO:MAK NestedTypes

			Inject(() =>
			{
				// TODO:MAK Interfaces, Properties, Events
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
			sourceMethod.Parameters.ForEach(p => CloneParameter(targetMethod, p));
			type.Methods.Add(targetMethod);
			Enqueue(() => CloneImplementation(sourceMethod, targetMethod));
		}

		private void CloneParameter(MethodDefinition targetMethod, ParameterDefinition sourceParameter)
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
				var targetHandler = new ExceptionHandler(sourceHandler.HandlerType)
				{
					CatchType = Resolve(sourceHandler.CatchType)
				};

				CopyExceptionHandler(
					sourceHandler, targetHandler,
					sourceMethod.Body.Instructions, targetMethod.Body.Instructions);

				targetMethod.Body.ExceptionHandlers.Add(targetHandler);
			}

			targetMethod.Body.OptimizeMacros();
		}

		private readonly static ConstructorInfo instructionConstructorInfo = 
			typeof(Instruction).GetConstructor(
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				new[] { typeof(OpCode), typeof(object) },
				null);

		private Instruction CloneInstruction(Instruction sourceInstruction)
		{
			return (Instruction)instructionConstructorInfo.Invoke(new object[] { 
				sourceInstruction.OpCode, ResolveAny(sourceInstruction.Operand), 
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
			return _into.MainModule.Types.Single(t => t.FullName == fullName);
		}

		private void CopyAttributes(ParameterDefinition sourceParameter, ParameterDefinition targetParameter)
		{
		}

		private void CopyAttributes(TypeDefinition sourceType, TypeDefinition targetType)
		{
		}

		private void CopyAttributes(FieldDefinition sourceField, FieldDefinition targetField)
		{
		}

		private void CopyAttributes(MethodDefinition sourceMethod, MethodDefinition targetMethod)
		{
		}
	}
}
