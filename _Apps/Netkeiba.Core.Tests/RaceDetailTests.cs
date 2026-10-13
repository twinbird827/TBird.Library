using System.Collections.Generic;
using System.Linq;
using Netkeiba;
using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class RaceDetailTests
	{
		// DB の上りは GetAgari で係数を割った値、RaceDetail は GetLastThreeFurlongs で係数を掛けて秒へ戻すため、両者が逆変換でないと差がずれる。
		[TestCase("芝", "2000")]
		[TestCase("ダート", "1800")]
		public void LastThreeFurlongsEqualToAverageIsZero(string track, string distance)
		{
			var agari = NetkeibaGetter.GetAgari(distance, track, false, "35.0");
			var race = new Race(new Dictionary<string, object>
			{
				["開催日"] = "2026/10/13",
				["ﾗﾝｸ1"] = "G1古",
				["馬場"] = track,
				["距離"] = distance,
			});
			// new Oikiri(x) はキーをインデクサで読むため、値は空でも追い切りのキーをすべて入れる。
			var x = new[]
			{
				"コース", "馬場", "乗り役", "時間1", "時間2", "時間3", "時間4", "時間5",
				"時間評価1", "時間評価2", "時間評価3", "時間評価4", "時間評価5", "脚色", "一言", "評価",
			}.ToDictionary(k => k, k => (object)"");
			x["生年月日"] = "2023/04/01";
			x["上り"] = agari;
			var detail = new RaceDetail(x, race);
			var tcd = new TrackConditionDistance(new Dictionary<string, object> { ["上り"] = agari });

			detail.SetHistoricalData(new List<RaceDetail>(), new[] { detail }, tcd);

			Assert.That(detail.LastThreeFurlongs2Avg, Is.EqualTo(0f).Within(1e-3f));
		}
	}
}
