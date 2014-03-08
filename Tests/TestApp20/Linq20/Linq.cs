using System;
using System.Collections.Generic;

namespace Linq20
{
	public delegate TResult Func<out TResult>();

	public delegate TResult Func<T, out TResult>(T subject);

	public delegate void Action();

	public static class Linq
	{
		public static T Single<T>(IEnumerable<T> collection, Func<T, bool> func)
		{
			var found = false;
			var result = default(T);

			foreach (var item in collection)
			{
				if (!func(item))
					continue;
				if (found)
					throw new ArgumentException();
				found = true;
				result = item;
			}

			if (!found)
				throw new ArgumentException();

			return result;
		}
	}
}
