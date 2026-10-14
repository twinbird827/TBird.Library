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

		private static Oikiri Create(string time5)
		{
			var x = new[]
			{
				"乗り役", "時間1", "時間2", "時間3", "時間4", "時間5",
				"時間評価1", "時間評価2", "時間評価3", "時間評価4", "時間評価5", "脚色", "評価",
			}.ToDictionary(k => k, k => (object)"");
			x["時間5"] = time5;
			return new Oikiri(x);
		}
	}
}
