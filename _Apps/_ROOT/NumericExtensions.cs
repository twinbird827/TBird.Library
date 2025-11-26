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
		public static float Median<T>(this IEnumerable<T> arr, Func<T, float> func, float def)
		{
			return arr.Any() ? arr.Select(func).Median() : def;
		}
	}
}