using System;
using System.Collections.Generic;
using System.Linq;

namespace Netkeiba
{
	public static class ObjectExtension
	{
		public static float GetRank<T>(this T detail, IEnumerable<T> src, Func<T, float> func, bool higherIsBetter)
		{
			return func(detail).GetRank(src.Select(func).ToArray(), higherIsBetter);
		}

		public static float GetZScore<T>(this T detail, IEnumerable<T> src, Func<T, float> func)
		{
			return func(detail).GetZScore(src.Select(func).ToArray());
		}
	}
}
