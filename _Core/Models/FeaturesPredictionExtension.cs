using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace Netkeiba.Models
{
	public static class FeaturesPredictionExtension
	{
		public static (double NDCG1, double NDCG3, double NDCG5) GetNDCG(this FeaturesPrediction[] tests)
		{
			var raceGroups = tests.GroupBy(x => x.RaceId).ToArray();

			var results = new ConcurrentBag<(double NDCG1, double NDCG3, double NDCG5)>();

			Parallel.ForEach(raceGroups, raceGroup =>
			{
				if (raceGroup.Count() < 3) return;

				var sortedByPrediction = raceGroup.OrderByDescending(x => x.Score).ToArray();

				double CalculateDCG(int k)
				{
					double dcg = 0;
					for (int i = 0; i < Math.Min(k, sortedByPrediction.Length); i++)
					{
						var actualRank = sortedByPrediction[i].ActualRank;
						var relevance = 1.0 / actualRank;
						var discount = Math.Log2(i + 2);
						dcg += relevance / discount;
					}
					return dcg;
				}

				double CalculateIDCG(int k)
				{
					var idealOrder = raceGroup.OrderBy(x => x.ActualRank).ToArray();
					double idcg = 0;
					for (int i = 0; i < Math.Min(k, idealOrder.Length); i++)
					{
						var actualRank = idealOrder[i].ActualRank;
						var relevance = 1.0 / actualRank;
						var discount = Math.Log2(i + 2);
						idcg += relevance / discount;
					}
					return idcg;
				}

				var dcg1 = CalculateDCG(1);
				var dcg3 = CalculateDCG(3);
				var dcg5 = CalculateDCG(5);

				var idcg1 = CalculateIDCG(1);
				var idcg3 = CalculateIDCG(3);
				var idcg5 = CalculateIDCG(5);

				double ndcg1 = idcg1 > 0 ? dcg1 / idcg1 : 0;
				double ndcg3 = idcg3 > 0 ? dcg3 / idcg3 : 0;
				double ndcg5 = idcg5 > 0 ? dcg5 / idcg5 : 0;

				results.Add((ndcg1, ndcg3, ndcg5));
			});

			var validRaces = results.Count;
			var ndcg1Sum = results.Sum(x => x.NDCG1);
			var ndcg3Sum = results.Sum(x => x.NDCG3);
			var ndcg5Sum = results.Sum(x => x.NDCG5);

			return (
				validRaces > 0 ? ndcg1Sum / validRaces : 0,
				validRaces > 0 ? ndcg3Sum / validRaces : 0,
				validRaces > 0 ? ndcg5Sum / validRaces : 0
			);
		}
	}
}
