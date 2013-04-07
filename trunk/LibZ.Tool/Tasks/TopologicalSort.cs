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
