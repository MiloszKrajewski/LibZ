using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace LibZ.Tool.InjectIL
{
	public static class Extensions
	{
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var i in collection) action(i);
		}

		public static TypeReference ImportType(this AssemblyDefinition assembly, TypeReference type)
		{
			return assembly.MainModule.Import(type);
		}

		public static TypeReference ImportType<T>(this AssemblyDefinition assembly)
		{
			return assembly.MainModule.Import(typeof (T));
		}

		public static MethodReference ImportMethod<T>(this AssemblyDefinition assembly, string methodName)
		{
			return assembly.MainModule.Import(typeof (T).GetMethod(methodName));
		}

		public static MethodReference ImportMethod(this AssemblyDefinition assembly, Type type, string methodName, params Type[] types)
		{
			return assembly.MainModule.Import(type.GetMethod(methodName, types));
		}

		public static MethodReference ImportMethod<T>(this AssemblyDefinition assembly, string methodName, params Type[] types)
		{
			return assembly.MainModule.Import(typeof (T).GetMethod(methodName, types));
		}

		public static MethodReference ImportCtor<T>(this AssemblyDefinition assembly, params Type[] types)
		{
			return assembly.MainModule.Import(typeof (T).GetConstructor(types));
		}

		//		private static bool AssemblyNamesEqual(string nameA, string nameB)
		//		{
		//			return string.Compare(nameA, nameB, StringComparison.InvariantCultureIgnoreCase) == 0;
		//		}
	}
}
