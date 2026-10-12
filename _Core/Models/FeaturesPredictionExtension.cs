using System;
using System.Linq;

namespace Netkeiba.Models
{
	public static class FeaturesPredictionExtension
	{
		public static (double NDCG1, double NDCG3, double NDCG5) GetNDCG(this FeaturesPrediction[] tests)
		{
			var results = tests
				.GroupBy(x => x.RaceId)
				.Where(g => g.Count() >= 3)
				.Select(raceGroup =>
				{
					var sortedByPrediction = raceGroup.OrderByDescending(x => x.Score).ToArray();
					var idealOrder = raceGroup.OrderBy(x => x.ActualRank).ToArray();

					double NDCG(int k) => CalculateDCG(sortedByPrediction, k) / CalculateDCG(idealOrder, k);

					return (NDCG1: NDCG(1), NDCG3: NDCG(3), NDCG5: NDCG(5));
				})
				.ToArray();

			return results.Length == 0
				? (0d, 0d, 0d)
				: (results.Average(x => x.NDCG1), results.Average(x => x.NDCG3), results.Average(x => x.NDCG5));
		}

		private static double CalculateDCG(FeaturesPrediction[] order, int k) =>
			order.Take(k).Select((x, i) => 1.0 / x.ActualRank / Math.Log2(i + 2)).Sum();
	}
}
