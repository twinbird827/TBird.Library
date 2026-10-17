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
	}
}
