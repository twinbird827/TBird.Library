using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Netkeiba
{
	public static class IEnumerableExtension
	{
		public static float Median(this IEnumerable<float> arr, float def)
		{
			var valid = arr.Where(x => !float.IsNaN(x));
			return valid.Any() ? valid.Median() : def;
		}

		public static float Median<T>(this IEnumerable<T> arr, Func<T, float> func, float def)
		{
			var valid = arr.Select(func).Where(x => !float.IsNaN(x));
			return valid.Any() ? valid.Median() : def;
		}

		public static float Max(this IEnumerable<float> arr, float def)
		{
			var valid = arr.Where(x => !float.IsNaN(x));
			return valid.Any() ? valid.Max() : def;
		}

		public static float Max<T>(this IEnumerable<T> arr, Func<T, float> func, float def)
		{
			var valid = arr.Select(func).Where(x => !float.IsNaN(x));
			return valid.Any() ? valid.Max() : def;
		}

		public static float Min(this IEnumerable<float> arr, float def)
		{
			var valid = arr.Where(x => !float.IsNaN(x));
			return valid.Any() ? valid.Min() : def;
		}

		public static float Min<T>(this IEnumerable<T> arr, Func<T, float> func, float def)
		{
			var valid = arr.Select(func).Where(x => !float.IsNaN(x));
			return valid.Any() ? valid.Min() : def;
		}
	}
}
