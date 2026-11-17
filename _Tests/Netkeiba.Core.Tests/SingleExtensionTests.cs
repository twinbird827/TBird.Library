using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class SingleExtensionTests
	{
		[Test]
		public void ZScoreWithSingleValidValueIsNaN()
		{
			Assert.That(1f.GetZScore(new[] { 1f, float.NaN, float.NaN }), Is.NaN);
		}

		[Test]
		public void ZScoreWithAllSameValuesIsZero()
		{
			Assert.That(1f.GetZScore(new[] { 1f, 1f, 1f }), Is.EqualTo(0f));
		}

		[Test]
		public void RankWithSingleValidValueIsNaN()
		{
			Assert.That(1f.GetRank(new[] { 1f, float.NaN, float.NaN }, true), Is.NaN);
		}

		// 差 0.01 未満は同値（完全一致への変更は研究の比較で NDCG@1 が下がり不採用）。
		[Test]
		public void RankTreatsNearValuesAsSame()
		{
			Assert.That(0.5f.GetRank(new[] { 0.5f, 0.505f, 0.9f }, true), Is.EqualTo(1f / 3).Within(1e-6f));
		}
	}
}
