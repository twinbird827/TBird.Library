using Netkeiba;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class OddsParseTests
	{
		// api_get_jra_odds.html?type=2 の応答（202608050411 の実応答の一部）
		private const string PlaceJson = "{\"status\":\"result\",\"data\":{\"official_datetime\":\"2026-11-15 15:49:14\",\"odds\":{\"2\":{\"01\":[\"6.7\",\"9.1\",\"11\"],\"02\":[\"1.3\",\"1.6\",\"1\"]}}},\"update_count\":\"0\",\"reason\":\"\"}";

		[Test]
		public void PlaceOddsHaveLowAndHigh()
		{
			var odds = NetkeibaGetter.ParseOdds(PlaceJson, 2);

			Assert.That(odds.Keys, Is.EquivalentTo(new[] { "01", "02" }));
			Assert.That(odds["01"][0], Is.EqualTo("6.7"));
			Assert.That(odds["01"][1], Is.EqualTo("9.1"));
		}
	}
}
