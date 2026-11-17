using System;
using System.Linq;
using TBird.Core;

namespace Netkeiba.Models
{
	public static class RacePredictionExtension
	{
		public static void CalculateRank(this RacePrediction[] results, Func<RacePrediction, RaceScore> func)
		{
			results.OrderByDescending(x => func(x).Score).ForEach((x, i) =>
				func(x).Rank = i + 1
			);
		}

		/// <summary>レース内のスコアから 1 着確率 softmax(s/T) を返す</summary>
		public static double[] Softmax(float[] scores, double temperature)
		{
			var max = scores.Max();
			var exps = scores.Select(s => Math.Exp((s - max) / temperature)).ToArray();
			var sum = exps.Sum();
			return exps.Select(e => e / sum).ToArray();
		}

		/// <summary>1 着確率 p1 から Harville 式（q2 = q3 = p1）で 2 着確率 P2・3 着確率 P3 を返す</summary>
		/// <remarks>O(頭数²) 形: c(j) = p1(j)/(1−p1(j))、P2(i) = p1(i)·(Σc − c(i))、b(j,k) = c(j)·p1(k)/(1−p1(j)−p1(k))、P3(i) = p1(i)·(Σb − Σ_k b(i,k) − Σ_j b(j,i))</remarks>
		public static (double[] P2, double[] P3) Harville(double[] p1)
		{
			var n = p1.Length;
			// 分母 0 は 2 頭以下のレースでだけ起き、その着の組み合わせは存在しないので寄与 0
			static double Div(double a, double d) => d > 0 ? a / d : 0;
			var c = p1.Select(p => Div(p, 1 - p)).ToArray();
			var sumC = c.Sum();
			var row = new double[n];
			var col = new double[n];
			var sumB = 0d;
			for (int j = 0; j < n; j++)
			{
				for (int k = 0; k < n; k++)
				{
					if (j == k) continue;
					var b = Div(c[j] * p1[k], 1 - p1[j] - p1[k]);
					row[j] += b;
					col[k] += b;
					sumB += b;
				}
			}
			var p2 = p1.Select((p, i) => p * (sumC - c[i])).ToArray();
			var p3 = p1.Select((p, i) => p * (sumB - row[i] - col[i])).ToArray();
			return (p2, p3);
		}

		/// <summary>レースの 1 着確率の最大値と帯境界（昇順 4 値）から自信度の帯 1〜5 を返す。境界値ちょうどは上の帯</summary>
		public static int GetConfidenceBand(double maxP1, double[] bounds)
		{
			return bounds.Count(x => x <= maxP1) + 1;
		}
	}
}
