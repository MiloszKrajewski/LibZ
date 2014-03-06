using System;
using System.Collections.Generic;
using Mono.Cecil;

namespace LibZ.Tool.InjectIL
{
	/// <summary>
	///     Some quick and dirty extension methods.
	/// </summary>
	public static class Extensions
	{
		/// <summary>ForEach version for IEnumerable.</summary>
		/// <typeparam name="T">Item type.</typeparam>
		/// <param name="collection">The collection.</param>
		/// <param name="action">The action.</param>
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var i in collection)
				action(i);
		}

		/// <summary>Imports the type.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <param name="type">The type.</param>
		/// <returns>Reference to imported type.</returns>
		public static TypeReference ImportType(this AssemblyDefinition assembly, TypeReference type)
		{
			return assembly.MainModule.Import(type);
		}

		/// <summary>Imports the type.</summary>
		/// <typeparam name="T">Type to be imported</typeparam>
		/// <param name="assembly">The assembly.</param>
		/// <returns>Reference to imported type.</returns>
		public static TypeReference ImportType<T>(this AssemblyDefinition assembly)
		{
			return assembly.MainModule.Import(typeof(T));
		}

		/// <summary>Imports method.</summary>
		/// <typeparam name="T">Type which declares method.</typeparam>
		/// <param name="assembly">The assembly.</param>
		/// <param name="methodName">Name of the method.</param>
		/// <returns>Reference to imported method.</returns>
		public static MethodReference ImportMethod<T>(this AssemblyDefinition assembly, string methodName)
		{
			return assembly.MainModule.Import(typeof(T).GetMethod(methodName));
		}

		/// <summary>Imports the method.</summary>
		/// <param name="assembly">The assembly.</param>
		/// <param name="type">Type which declares method.</param>
		/// <param name="methodName">Name of the method.</param>
		/// <param name="types">The types.</param>
		/// <returns>Reference to imported method.</returns>
		public static MethodReference ImportMethod(this AssemblyDefinition assembly, Type type, string methodName, params Type[] types)
		{
			return assembly.MainModule.Import(type.GetMethod(methodName, types));
		}

		// ReSharper disable MethodOverloadWithOptionalParameter

		/// <summary>Imports the method.</summary>
		/// <typeparam name="T">Type which declares method.</typeparam>
		/// <param name="assembly">The assembly.</param>
		/// <param name="methodName">Name of the method.</param>
		/// <param name="types">The types.</param>
		/// <returns>Reference to imported method.</returns>
		public static MethodReference ImportMethod<T>(this AssemblyDefinition assembly, string methodName, params Type[] types)
		{
			return assembly.MainModule.Import(typeof(T).GetMethod(methodName, types));
		}

		// ReSharper restore MethodOverloadWithOptionalParameter

		/// <summary>Imports the constructor.</summary>
		/// <typeparam name="T">Type which declares constructor.</typeparam>
		/// <param name="assembly">The assembly.</param>
		/// <param name="types">The types.</param>
		/// <returns>Rference to impored constructor.</returns>
		public static MethodReference ImportCtor<T>(this AssemblyDefinition assembly, params Type[] types)
		{
			return assembly.MainModule.Import(typeof(T).GetConstructor(types));
		}
	}
}