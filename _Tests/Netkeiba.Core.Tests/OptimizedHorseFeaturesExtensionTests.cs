using System;
using System.Linq;
using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class OptimizedHorseFeaturesExtensionTests
	{
		// 期待値は SingleExtension の GetRank = (劣る件数 + 同値件数 / 2) / 件数、GetZScore = (値 - 平均) / 母標準偏差 から手計算した値。

		[Test]
		public void RankDirectionFollowsHigherIsBetter()
		{
			var results = Calculate();

			Assert.That(results.Select(x => x.RaceCountRank), Is.EqualTo(new[] { 2.5f / 3, 1.5f / 3, 0.5f / 3 }).Within(1e-4f));
		}

		[Test]
		public void DistanceDiffRanksSmallerAbsoluteHigher()
		{
			var results = Calculate();

			Assert.That(results.Select(x => x.DistanceDiffRank), Is.EqualTo(new[] { 1.5f / 3, 2.5f / 3, 0.5f / 3 }).Within(1e-4f));
		}

		[Test]
		public void TimeZScoreIsNegated()
		{
			var results = Calculate();

			// AvgTime2Top3 = 0.5 / 1.0 / 1.5 → 平均 1.0、母標準偏差 √(1/6)。
			var z = 0.5f / MathF.Sqrt(1f / 6);
			Assert.That(results.Select(x => x.AvgTime2Top3ZScore), Is.EqualTo(new[] { z, 0f, -z }).Within(1e-4f));
		}

		[Test]
		public void PaceIgnoresHorsesWithoutTuka()
		{
			var results = new[]
			{
				new OptimizedHorseFeatures { Tuka = 0.2f },
				new OptimizedHorseFeatures { Tuka = 0.6f },
				new OptimizedHorseFeatures { Tuka = float.NaN },
			}.CalculateInRaces();

			// pace = (0.2 + 0.6) / 2 = 0.4 → 1 - |0.4 - Tuka|。
			Assert.That(results.Take(2).Select(x => x.PaceAdvantage), Is.EqualTo(new[] { 0.8f, 0.8f }).Within(1e-4f));
			Assert.That(results[2].PaceAdvantage, Is.NaN);
		}

		private static OptimizedHorseFeatures[] Calculate() => new[]
		{
			new OptimizedHorseFeatures { RaceCount = 10, DistanceDiff = -100, AvgTime2Top3 = 0.5f },
			new OptimizedHorseFeatures { RaceCount = 5, DistanceDiff = 50, AvgTime2Top3 = 1.0f },
			new OptimizedHorseFeatures { RaceCount = 1, DistanceDiff = 300, AvgTime2Top3 = 1.5f },
		}.CalculateInRaces();
	}
}
