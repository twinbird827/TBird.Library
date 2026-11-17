using System.Collections.Generic;
using System.Linq;
using Netkeiba.Models;
using NUnit.Framework;

namespace Netkeiba.Core.Tests
{
	public class PreviousDataSetsTests
	{
		// OppFastElo・FastEloExcess は UpdateElo が更新する前の FastHorseElo から出す。
		[Test]
		public void UpdateEloWritesOpponentEloAndExcessBeforeUpdate()
		{
			// 他のテストと Elo の辞書を共有するので、このテストだけの馬 ID を使う
			var horses = new[] { "PDS-A", "PDS-B", "PDS-C" };

			// 1 走目: 全馬が初期 Elo なので、相手平均は初期値、Elo 順位パーセンタイルは全馬 0.5
			var first = Race(horses, new[] { 1, 2, 3 });
			var initial = PreviousDataSets.GetFastHorseElo(first[0]);
			PreviousDataSets.UpdateElo(first);
			Assert.That(first.Select(x => x.OppFastElo), Is.All.EqualTo(initial).Within(1e-3f));
			Assert.That(first.Select(x => x.FastEloExcess), Is.EqualTo(new[] { 0.5f, 0f, -0.5f }).Within(1e-5f));

			// 2 走目: 1 走目の勝ち順に Elo が A > B > C。着順は B・C・A
			var second = Race(horses, new[] { 3, 1, 2 });
			var elos = second.Select(PreviousDataSets.GetFastHorseElo).ToArray();
			Assert.That(elos[0] > elos[1] && elos[1] > elos[2]);
			PreviousDataSets.UpdateElo(second);
			Assert.That(second.Select(x => x.OppFastElo), Is.EqualTo(new[] { (elos[1] + elos[2]) / 2, (elos[0] + elos[2]) / 2, (elos[0] + elos[1]) / 2 }).Within(1e-3f));
			// 着順パーセンタイル 0 / 1 / 0.5 − Elo 順位パーセンタイル 1 / 0.5 / 0
			Assert.That(second.Select(x => x.FastEloExcess), Is.EqualTo(new[] { -1f, 0.5f, 0.5f }).Within(1e-5f));
		}

		private static RaceDetail[] Race(string[] horses, int[] finishes)
		{
			var race = new Race(new Dictionary<string, object>
			{
				["開催日"] = "2026/10/13",
				["ﾗﾝｸ1"] = "G1古",
				["馬場"] = "芝",
				["距離"] = "2000",
			});
			return horses.Select((horse, i) =>
			{
				// new Oikiri(x) はキーをインデクサで読むため、値は空でも追い切りのキーをすべて入れる。
				var x = new[]
				{
					"乗り役", "時間1", "時間2", "時間3", "時間4", "時間5",
					"時間評価1", "時間評価2", "時間評価3", "時間評価4", "時間評価5", "脚色", "評価",
				}.ToDictionary(k => k, k => (object)"");
				x["生年月日"] = "2023/04/01";
				x["馬ID"] = horse;
				x["着順"] = finishes[i].ToString();
				return new RaceDetail(x, race);
			}).ToArray();
		}
	}
}
