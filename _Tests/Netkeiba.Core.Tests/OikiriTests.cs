using System.Linq;
using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class OikiriTests
	{
		[Test]
		public void MissingTime5ScoresAsAverageTime()
		{
			Assert.That(Create("").TotalScore, Is.EqualTo(Create("13.5").TotalScore).Within(1e-6f));
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

		private static Oikiri Create(string time5, string adaptation = "")
		{
			var x = new[]
			{
				"乗り役", "時間1", "時間2", "時間3", "時間4", "時間5",
				"時間評価1", "時間評価2", "時間評価3", "時間評価4", "時間評価5", "脚色", "評価",
			}.ToDictionary(k => k, k => (object)"");
			x["時間5"] = time5;
			x["脚色"] = adaptation;
			return new Oikiri(x);
		}
	}
}
