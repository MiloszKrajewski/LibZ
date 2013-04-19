using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LibZ.Bootstrap;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace LibZ.Tool.ClassInjector
{
	[TestFixture]
	public class Class1
	{
		private AssemblyDefinition _into;

		[Test]
		public void Test()
		{
			var from = AssemblyDefinition.ReadAssembly("LibZ.Bootstrap.dll");
			_into = AssemblyDefinition.ReadAssembly("LibZ.Tool.ClassInjector.dll");

			var source = from.GetTypeReference("LibZ.Bootstrap.AsmZResolver");
			CopyType(source.Resolve(), _into);

			_into.AddReference(from);
			var moduleType = _into.GetTypeReference("<Module>");
			if (moduleType == null) throw new ArgumentException("No <Module> class found");
			var constructor = moduleType.GetStaticConstructor() ?? moduleType.CreateStaticConstructor();
			ExtendConstructor(constructor);

			_into.Write("x.dll");
		}

		private static void CopyType(TypeDefinition source, AssemblyDefinition into)
		{
			var type = new TypeDefinition(
				source.Namespace, source.Name, source.Attributes, into.ImportType(source.BaseType));
			into.MainModule.Types.Add(type);

			// skipped: source.Interfaces
			// source.Properties
			// source.NestedTypes
			// source.Events

			var from = source.Module.Assembly;

			foreach (var fld in source.Fields)
			{
				var field = new FieldDefinition(fld.Name, fld.Attributes, Import(from, into, fld.FieldType));
				type.Fields.Add(field);
			}

			foreach (var fun in source.Methods)
			{
				var method = new MethodDefinition(fun.Name, fun.Attributes, Import(from, into, fun.ReturnType));
				foreach (var arg in fun.Parameters)
				{
					var argument = new ParameterDefinition(arg.Name, arg.Attributes, Import(from, into, arg.ParameterType));
					method.Parameters.Add(argument);
				}

				foreach (var var in fun.Body.Variables)
				{
					var variable = new VariableDefinition(var.Name, Import(from, into, Import(from, into, var.VariableType)));
					method.Body.Variables.Add(variable);
				}

				var body = method.Body.GetILProcessor();

				foreach (var ins in fun.Body.Instructions)
				{
					ins.Operand = Import(from, into, ins.Operand);
					body.Append(ins);
				}

				type.Methods.Add(method);
			}
		}

		private static bool BelongsTo(TypeReference type, AssemblyDefinition assembly)
		{
			return 
				type.Scope.MetadataToken == assembly.MetadataToken ||
				type.Scope.MetadataToken == assembly.MainModule.MetadataToken;
		}

		private static object Import(AssemblyDefinition from, AssemblyDefinition into, object subject)
		{
			if (subject is TypeReference) return Import(from, into, (TypeReference) subject);
			if (subject is MethodReference) return Import(from, into, (MethodReference) subject);
			if (subject is FieldReference) return Import(from, into, (FieldReference) subject);
			return subject;
		}

		private static TypeReference Import(AssemblyDefinition from, AssemblyDefinition into, TypeReference reference)
		{
			if (BelongsTo(reference, from))
			{
				return into.MainModule.Types.First(r => r.FullName == reference.FullName);
			}
			else
			{
				return into.MainModule.Import(reference);
			}
		}

		private static MethodReference Import(AssemblyDefinition from, AssemblyDefinition into, MethodReference reference)
		{
			if (reference.Name == "TryLoadAssembly") Debugger.Break();
			if (BelongsTo(reference.DeclaringType, from))
			{
				return new MethodReference(
					reference.Name, 
					Import(from, into, reference.ReturnType),
					Import(from, into, reference.DeclaringType));
			}
			else
			{
				return into.MainModule.Import(reference);
			}
		}

		private static FieldReference Import(AssemblyDefinition from, AssemblyDefinition into, FieldReference reference)
		{
			if (BelongsTo(reference.DeclaringType, from))
			{
				return new FieldReference(
					reference.Name,
					Import(from, into, reference.FieldType),
					Import(from, into, reference.DeclaringType));
			}
			else
			{
				return into.MainModule.Import(reference);
			}
		}

		private void ExtendConstructor(MethodDefinition constructor)
		{
			var assembly = constructor.DeclaringType.Module.Assembly;
			var il = constructor.Body.GetILProcessor();

			constructor.Body.MaxStackSize = 8;

			il.Emit(OpCodes.Ldstr, "Module initialization started");
			il.Emit(OpCodes.Call, assembly.ImportMethod(typeof(Console), "WriteLine", typeof(string)));

			il.Emit(OpCodes.Ldtoken, constructor.DeclaringType);
			il.Emit(OpCodes.Call, assembly.ImportMethod<Type>("GetTypeFromHandle"));
			il.Emit(OpCodes.Call, assembly.ImportMethod<LibZResolver>("RegisterAllResourceContainers"));
			il.Emit(OpCodes.Pop);

			il.Emit(OpCodes.Ldstr, "containers.libz");
			il.Emit(OpCodes.Ldc_I4_1);
			il.Emit(OpCodes.Call, assembly.ImportMethod<LibZResolver>("RegisterFileContainer"));
			il.Emit(OpCodes.Pop);

			il.Emit(OpCodes.Call, assembly.ImportMethod<AppDomain>("get_CurrentDomain"));
			il.Emit(OpCodes.Callvirt, assembly.ImportMethod<AppDomain>("get_BaseDirectory"));
			il.Emit(OpCodes.Ldstr, "*.libz");
			il.Emit(OpCodes.Call, assembly.ImportMethod(typeof(Path), "Combine", typeof(string), typeof(string)));
			il.Emit(OpCodes.Call, assembly.ImportMethod<LibZResolver>("RegisterMultipleFileContainers"));
			il.Emit(OpCodes.Pop);

			il.Emit(OpCodes.Ldstr, "Module initialization done");
			il.Emit(OpCodes.Call, assembly.ImportMethod(typeof(Console), "WriteLine", typeof(string)));

			il.Emit(OpCodes.Ret);
		}

		static Class1()
		{
			Console.WriteLine("Module initialization started");
			LibZResolver.RegisterAllResourceContainers(typeof(Class1));
			LibZResolver.RegisterFileContainer("xxx");
			LibZResolver.RegisterMultipleFileContainers(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "*.libz"));
			Console.WriteLine("Module initialization done");
		}
	}
}