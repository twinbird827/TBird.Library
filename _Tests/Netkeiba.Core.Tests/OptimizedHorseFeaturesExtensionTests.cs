using System;
using System.Collections.Generic;
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

			Assert.That(results.Select(x => x.TrainerEloRank), Is.EqualTo(new[] { 2.5f / 3, 1.5f / 3, 0.5f / 3 }).Within(1e-4f));
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

			// SireDamSireBrosAvgTime2Top = 0.5 / 1.0 / 1.5 → 平均 1.0、母標準偏差 √(1/6)。
			var z = 0.5f / MathF.Sqrt(1f / 6);
			Assert.That(results.Select(x => x.SireDamSireBrosAvgTime2TopZScore), Is.EqualTo(new[] { z, 0f, -z }).Within(1e-4f));
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

		[Test]
		public void TukaGroupFollowsResearchFormula()
		{
			var results = new[]
			{
				new OptimizedHorseFeatures { Tuka = 0.1f },
				new OptimizedHorseFeatures { Tuka = 0.4f },
				new OptimizedHorseFeatures { Tuka = float.NaN },
				new OptimizedHorseFeatures { Tuka = 0.25f },
			}.CalculateInRaces();

			// 有効な Tuka = 0.1 / 0.25 / 0.4 → pace 0.25。0.1 は最小で 2 番手と 0.15 離れるので単騎逃げ。
			Assert.That(results.Select(x => x.FrontCount), Is.EqualTo(new[] { 0f, 1f, float.NaN, 1f }));
			Assert.That(results.Select(x => x.TukaOrder), Is.EqualTo(new[] { 2.5f / 3, 0.5f / 3, float.NaN, 1.5f / 3 }).Within(1e-4f));
			Assert.That(results.Select(x => x.LoneFront), Is.EqualTo(new[] { 1f, 0f, float.NaN, 0f }));
			Assert.That(results.Select(x => x.TukaPace), Is.EqualTo(new[] { 0.025f, 0.1f, float.NaN, 0.0625f }).Within(1e-4f));
		}

		[Test]
		public void LoneFrontCountsTiedSecond()
		{
			var results = new[] { 0.1f, 0.1f, 0.5f }.Select(t => new OptimizedHorseFeatures { Tuka = t }).CalculateInRaces();

			// 研究の rank(method="first") == 2 は同値の 2 頭目を 2 番手にするので、最小が並ぶと誰も単騎逃げにならない。
			Assert.That(results.Select(x => x.LoneFront), Is.EqualTo(new[] { 0f, 0f, 0f }));
		}

		[Test]
		public void TukaGroupIsNaNWithSingleValidTuka()
		{
			var results = new[] { 0.1f, float.NaN }.Select(t => new OptimizedHorseFeatures { Tuka = t }).CalculateInRaces();

			Assert.That(new[] { results[0].FrontCount, results[0].TukaOrder, results[0].LoneFront, results[0].TukaPace }, Is.All.NaN);
		}

		// static の BlobLength が列に混ざると BLOB が 1 列長くなり、Deserialize が get しかないプロパティへ代入して落ちる
		[Test]
		public void SerializeRoundTripsWithBlobLength()
		{
			var bytes = new OptimizedHorseFeatures { TukaPace = 0.5f, TrainerBreederAvgTime2Condition = 2f }.Serialize();

			Assert.That(bytes.Length, Is.EqualTo(OptimizedHorseFeatures.BlobLength));
			Assert.That(OptimizedHorseFeatures.BlobLength, Is.EqualTo(230 * sizeof(float)));
			var back = OptimizedHorseFeatures.Deserialize(new Dictionary<string, object> { ["Label"] = 1, ["RaceId"] = "R", ["Horse"] = "H", ["Features"] = bytes })!;
			Assert.That(new[] { back.TukaPace, back.TrainerBreederAvgTime2Condition }, Is.EqualTo(new[] { 0.5f, 2f }));
		}

		private static OptimizedHorseFeatures[] Calculate() => new[]
		{
			new OptimizedHorseFeatures { TrainerElo = 10, DistanceDiff = -100, SireDamSireBrosAvgTime2Top = 0.5f },
			new OptimizedHorseFeatures { TrainerElo = 5, DistanceDiff = 50, SireDamSireBrosAvgTime2Top = 1.0f },
			new OptimizedHorseFeatures { TrainerElo = 1, DistanceDiff = 300, SireDamSireBrosAvgTime2Top = 1.5f },
		}.CalculateInRaces();
	}
}
