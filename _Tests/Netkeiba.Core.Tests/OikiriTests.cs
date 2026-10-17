using System.Linq;
using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class OikiriTests
	{
		[TestCase("12.9", 12.9f)]
		[TestCase("?12.9", 12.9f)]
		[TestCase("102.751.7", 51.7f)]
		[TestCase("-", float.NaN)]
		[TestCase("", float.NaN)]
		[TestCase("中間軽め", float.NaN)]
		public void Time5ReadsLastOneDecimalNumber(string time5, float expected)
		{
			Assert.That(Create(time5).Time5, Is.EqualTo(expected));
		}

		[Test]
		public void MissingTime5StaysNaNAndTotalScoreDropsTimeTerm()
		{
			var oikiri = Create("");
			Assert.That(oikiri.AdjustedTime5, Is.NaN);
			Assert.That(oikiri.TotalScore, Is.Not.NaN);
		}

		[TestCase("馬也", 0.90f)]
		[TestCase("一杯", 0.18f)]
		[TestCase("強め", 0.48f)]
		[TestCase("仕掛", 0.91f)]
		[TestCase("Ｇ強", 0.86f)]
		[TestCase("", 0.00f)]
		[TestCase("Ｇ一", 0.63f)]
		[TestCase("直強", 1.00f)]
		[TestCase("直一", 0.57f)]
		[TestCase("G強", 0.86f)]
		[TestCase("未知", 0.00f)]
		public void AdaptationMapsEveryDbValue(string adaptation, float expected)
		{
			Assert.That(Create("", adaptation).Adaptation, Is.EqualTo(expected));
		}

		[TestCase("助手", 0.69f)]
		[TestCase("", 0.00f)]
		[TestCase("師", 1.00f)]
		[TestCase("攻手", 0.81f)]
		[TestCase("見習", 0.13f)]
		[TestCase("丹内", 0.58f)]
		public void RiderMapsEveryDbValue(string rider, float expected)
		{
			Assert.That(Create("", rider: rider).Rider, Is.EqualTo(expected));
		}

		[TestCase("A", 1.00f)]
		[TestCase("D", 0.00f)]
		[TestCase("", float.NaN)]
		public void RatingMapsBlankToNaN(string rating, float expected)
		{
			Assert.That(Create("", rating: rating).Rating, Is.EqualTo(expected));
		}

		private static Oikiri Create(string time5, string adaptation = "", string rider = "", string rating = "")
		{
			var x = new[]
			{
				"乗り役", "時間1", "時間2", "時間3", "時間4", "時間5",
				"時間評価1", "時間評価2", "時間評価3", "時間評価4", "時間評価5", "脚色", "評価",
			}.ToDictionary(k => k, k => (object)"");
			x["時間5"] = time5;
			x["脚色"] = adaptation;
			x["乗り役"] = rider;
			x["評価"] = rating;
			return new Oikiri(x);
		}
	}
}
