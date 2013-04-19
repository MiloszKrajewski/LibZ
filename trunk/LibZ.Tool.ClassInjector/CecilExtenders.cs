using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;

namespace LibZ.Tool.ClassInjector
{
	public static class CecilExtenders
	{
		public static bool AddReference(this AssemblyDefinition into, AssemblyDefinition assembly)
		{
			return AddReference(into, assembly.Name);
		}

		public static bool AddReference(AssemblyDefinition into, AssemblyNameDefinition assemblyName)
		{
			return AddReference(into, assemblyName.FullName);
		}

		public static bool AddReference(AssemblyDefinition into, AssemblyNameReference assemblyName)
		{
			return AddReference(into, assemblyName.FullName);
		}

		public static bool AddReference(AssemblyDefinition into, string assemblyName)
		{
			if (into.MainModule.AssemblyReferences.Any(r => AssemblyNamesEqual(r.FullName, assemblyName)))
				return false;

			into.MainModule.AssemblyReferences.Add(AssemblyNameReference.Parse(assemblyName));
			return true;
		}

		public static TypeReference GetTypeReference(this AssemblyDefinition assembly, string fullName)
		{
			return assembly.MainModule.GetTypes().SingleOrDefault(t => t.FullName == fullName);
		}

		public static MethodDefinition GetMethodDefinition(this TypeReference type, string name)
		{
			return GetMethodDefinition(type.Resolve(), name);
		}

		public static MethodDefinition GetStaticConstructor(this TypeReference type)
		{
			return GetStaticConstructor(type.Resolve());
		}

		public static MethodDefinition GetStaticConstructor(this TypeDefinition type)
		{
			return GetMethodDefinition(type, ".cctor");
		}

		public static MethodDefinition CreateStaticConstructor(this TypeReference type)
		{
			return CreateStaticConstructor(type.Resolve());
		}

		public static MethodDefinition CreateStaticConstructor(this TypeDefinition type)
		{
			const MethodAttributes flags =
				MethodAttributes.Private |
				MethodAttributes.HideBySig |
				MethodAttributes.SpecialName |
				MethodAttributes.RTSpecialName |
				MethodAttributes.Static;

			var method = new MethodDefinition(".cctor", flags, type.Module.TypeSystem.Void);
			type.Methods.Add(method);
			return method;
		}

		public static MethodDefinition GetMethodDefinition(this TypeDefinition type, string name)
		{
			return type.Methods.SingleOrDefault(m => m.Name == name);
		}

		public static object TryImport(this AssemblyDefinition assembly, object subject)
		{
			if (subject is TypeReference)
				return assembly.MainModule.Import(subject as TypeReference);
			if (subject is MethodReference)
				return assembly.MainModule.Import(subject as MethodReference);
			if (subject is FieldReference)
				return assembly.MainModule.Import(subject as FieldReference);
			return subject;
		}

		public static TypeReference ImportType(this AssemblyDefinition assembly, TypeReference type)
		{
			return assembly.MainModule.Import(type);
		}

		public static TypeReference ImportType<T>(this AssemblyDefinition assembly)
		{
			return assembly.MainModule.Import(typeof(T));
		}

		public static MethodReference ImportMethod<T>(this AssemblyDefinition assembly, string methodName)
		{
			return assembly.MainModule.Import(typeof(T).GetMethod(methodName));
		}

		public static MethodReference ImportMethod(this AssemblyDefinition assembly, Type type, string methodName, params Type[] types)
		{
			return assembly.MainModule.Import(type.GetMethod(methodName, types));
		}

		public static MethodReference ImportMethod<T>(this AssemblyDefinition assembly, string methodName, params Type[] types)
		{
			return assembly.MainModule.Import(typeof(T).GetMethod(methodName, types));
		}

		public static MethodReference ImportCtor<T>(this AssemblyDefinition assembly, params Type[] types)
		{
			return assembly.MainModule.Import(typeof(T).GetConstructor(types));
		}

		private static bool AssemblyNamesEqual(string nameA, string nameB)
		{
			return string.Compare(nameA, nameB, StringComparison.InvariantCultureIgnoreCase) == 0;
		}
	}
}
