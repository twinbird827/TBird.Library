using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class FeaturesPredictionExtensionTests
	{
		[Test]
		public void PerfectOrderScoresOne()
		{
			var ndcg = new[]
			{
				Prediction("r1", 1, 0.9f),
				Prediction("r1", 2, 0.5f),
				Prediction("r1", 3, 0.1f),
			}.GetNDCG();

			Assert.That(ndcg.NDCG1, Is.EqualTo(1).Within(1e-9));
			Assert.That(ndcg.NDCG3, Is.EqualTo(1).Within(1e-9));
			Assert.That(ndcg.NDCG5, Is.EqualTo(1).Within(1e-9));
		}

		// relevance が 1 / 着順 なので、予測 1 位が 2 着なら NDCG@1 は 0.5。
		[Test]
		public void SecondPlaceOnTopHalvesNDCG1()
		{
			var ndcg = new[]
			{
				Prediction("r1", 1, 0.5f),
				Prediction("r1", 2, 0.9f),
				Prediction("r1", 3, 0.1f),
			}.GetNDCG();

			Assert.That(ndcg.NDCG1, Is.EqualTo(0.5).Within(1e-9));
		}

		[Test]
		public void RacesUnderThreeHorsesAreIgnored()
		{
			var ndcg = new[]
			{
				Prediction("r1", 1, 0.1f),
				Prediction("r1", 2, 0.9f),
			}.GetNDCG();

			Assert.That(ndcg, Is.EqualTo((0d, 0d, 0d)));
		}

		private static FeaturesPrediction Prediction(string raceId, uint actualRank, float score) => new FeaturesPrediction
		{
			RaceId = raceId,
			ActualRank = actualRank,
			Score = score,
		};
	}
}
