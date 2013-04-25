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
using Mono.Cecil;

namespace LibZ.Tool.InjectIL
{
	/// <summary>
	/// Some quick and dirty extension methods.
	/// </summary>
	public static class Extensions
	{
		/// <summary>ForEach version for IEnumerable.</summary>
		/// <typeparam name="T">Item type.</typeparam>
		/// <param name="collection">The collection.</param>
		/// <param name="action">The action.</param>
		public static void ForEach<T>(this IEnumerable<T> collection, Action<T> action)
		{
			foreach (var i in collection) action(i);
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
