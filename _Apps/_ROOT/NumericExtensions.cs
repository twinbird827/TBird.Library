using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	public static class NumericExtensions
	{
		public static float Median(this IEnumerable<float> arr, float def)
		{
			return arr.Any() ? arr.Median() : def;
		}

		public static float Median<T>(this IEnumerable<T> arr, Func<T, float> func, float def)
		{
			return arr.Any() ? arr.Select(func).Median() : def;
		}

		public static float Max(this IEnumerable<float> arr, float def)
		{
			return arr.Any() ? arr.Max() : def;
		}

		public static float Max<T>(this IEnumerable<T> arr, Func<T, float> func, float def)
		{
			return arr.Any() ? arr.Select(func).Max() : def;
		}

		public static float Min(this IEnumerable<float> arr, float def)
		{
			return arr.Any() ? arr.Min() : def;
		}

		public static float Min<T>(this IEnumerable<T> arr, Func<T, float> func, float def)
		{
			return arr.Any() ? arr.Select(func).Min() : def;
		}

		public static float MinMax(this float val, float min, float max)
		{
			return Math.Min(Math.Max(val, min), max);
		}
	}
}