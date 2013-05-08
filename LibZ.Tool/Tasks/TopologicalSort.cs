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

namespace LibZ.Tool.Tasks
{
	/// <summary>
	/// Class that performs topological sort.
	/// </summary>
	/// <typeparam name="T">Type of objects to be sorted.</typeparam>
	public class TopologicalSort<T>
	{
		#region sort

		/// <summary>Sorts given nodes.</summary>
		/// <param name="nodes">Nodes collection.</param>
		/// <param name="dependencyResolver">The dependency resolver. Should return items required by given item.</param>
		/// <returns>Sorted nodes.</returns>
		public static IEnumerable<T> Sort(
			IEnumerable<T> nodes,
			Func<T, IEnumerable<T>> dependencyResolver)
		{
			var visited = new HashSet<T>();

			// visit every node
			foreach (var node in nodes)
			{
				foreach (var result in Visit(node, visited, dependencyResolver))
				{
					yield return result;
				}
			}
		}

		/// <summary>Visits the specified node.</summary>
		/// <param name="node">The node.</param>
		/// <param name="visited">The visited.</param>
		/// <param name="dependencyResolver">The dependency resolver.</param>
		/// <returns>Sorted subset of nodes.</returns>
		private static IEnumerable<T> Visit(T node, HashSet<T> visited, Func<T, IEnumerable<T>> dependencyResolver)
		{
			// If we're visiting some place we've already been, then we need go no further
			if (visited.Contains(node))
				yield break;

			// We're about to explore this entire dependency, so mark it as dead right away...
			visited.Add(node);

			// ...and then recursively explore all this dependency's children, looking for the bottom. 
			foreach (var child in dependencyResolver(node))
			{
				foreach (var result in Visit(child, visited, dependencyResolver))
				{
					yield return result;
				}
			}

			// As we return back up the stack, we know that all of this dependency's children are taken care of.  
			// Therefore, this is a "bottom".  Therefore, put it on the list. 
			yield return node;
		}

		#endregion
	}
}
