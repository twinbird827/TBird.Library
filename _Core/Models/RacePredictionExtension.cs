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

		public static void CalculateWinProb(this RacePrediction[] results, Func<RacePrediction, RaceScore> func, double temperature)
		{
			var t = (float)temperature;
			var maxScore = results.Max(x => func(x).Score);
			var exps = results.Select(x => MathF.Exp((func(x).Score - maxScore) / t)).ToArray();
			var sumExp = exps.Sum();
			results.ForEach((x, i) => func(x).WinProb = exps[i] / sumExp);
		}
	}
}
