using System;
using System.Linq;

namespace Netkeiba
{
	public static class SingleExtension
	{
		public static float CalculateStandardDeviation(float[] values)
		{
			if (values.Length < 2) return 1.0f;
			var mean = values.Average();
			var variance = values.Select(v => (v - mean) * (v - mean)).Average();
			return (float)Math.Sqrt(variance);
		}

		public static float GetRank(this float val, float[] arr, bool higherIsBetter)
		{
			if (float.IsNaN(val)) return float.NaN;
			var validArr = arr.Where(x => !float.IsNaN(x)).ToArray();
			if (validArr.Length < 2) return float.NaN;
			var same = validArr.Count(x => Math.Abs(x - val) < 0.01f);
			var wrse = higherIsBetter ? validArr.Count(x => x < val) : validArr.Count(x => x > val);

			return (wrse + same / 2.0f) / validArr.Length;
		}

		public static float GetZScore(this float val, float[] arr)
		{
			if (float.IsNaN(val)) return float.NaN;
			var validArr = arr.Where(x => !float.IsNaN(x)).ToArray();
			if (validArr.Length < 2) return float.NaN;
			var mean = validArr.Average();
			var std = CalculateStandardDeviation(validArr);
			if (std < 0.0001f) return 0f;
			return (val - mean) / std;
		}

		public static float MinMax(this float val, float min, float max)
		{
			return Math.Min(Math.Max(val, min), max);
		}
	}
}
