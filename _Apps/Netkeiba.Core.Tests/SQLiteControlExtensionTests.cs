using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba.Core.Tests
{
	public class SQLiteControlExtensionTests
	{
		[Test]
		public async Task RemoveShortageMissingDatasKeepsOnlyConfirmedRaces()
		{
			var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, "remove-shortage");
			if (Directory.Exists(work)) Directory.Delete(work, true);
			Directory.CreateDirectory(work);

			using (var conn = new SQLiteControl(Path.Combine(work, "database.sqlite3"), string.Empty, false, false, 1024 * 1024, true))
			{
				await conn.CreateOrig();
				await conn.CreateOikiri();

				foreach (var (raceid, date, rank) in new[] { ("old", "2024/01/06", "1"), ("new", "2024/01/13", "0") })
				{
					await conn.ExecuteNonQueryAsync($"INSERT INTO t_orig_h (ﾚｰｽID, 開催日) VALUES ('{raceid}', '{date}')");
					await conn.ExecuteNonQueryAsync($"INSERT INTO t_orig_d (ﾚｰｽID, 馬番, 着順) VALUES ('{raceid}', '1', '{rank}')");
					await conn.ExecuteNonQueryAsync($"INSERT INTO t_oikiri (ﾚｰｽID, 馬ID) VALUES ('{raceid}', 'h1')");
				}

				await conn.RemoveShortageMissingDatasAsync();

				foreach (var table in new[] { "t_orig_h", "t_orig_d", "t_oikiri" })
				{
					var raceids = await conn.GetRows(r => r.GetString(0), $"SELECT ﾚｰｽID FROM {table}");
					Assert.That(raceids, Is.EqualTo(new[] { "old" }), table);
				}
			}
		}

		[Test]
		public async Task GetTrackDistanceExcludesJumpRaces()
		{
			var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, "track-distance");
			if (Directory.Exists(work)) Directory.Delete(work, true);
			Directory.CreateDirectory(work);

			using (var conn = new SQLiteControl(Path.Combine(work, "database.sqlite3"), string.Empty, false, false, 1024 * 1024, true))
			{
				await conn.CreateOrig();

				foreach (var (raceid, distance, jump, agari, time) in new[] { ("flat", "3000", "0", "33", "187"), ("jump", "3000", "1", "34", "205"), ("jump2", "2750", "1", "34", "205") })
				{
					await conn.ExecuteNonQueryAsync($"INSERT INTO t_orig_h (ﾚｰｽID, 馬場, 馬場状態, 距離, 障害) VALUES ('{raceid}', '芝', '良', '{distance}', '{jump}')");
					await conn.ExecuteNonQueryAsync($"INSERT INTO t_orig_d (ﾚｰｽID, 馬番, 上り, ﾀｲﾑ変換) VALUES ('{raceid}', '1', '{agari}', '{time}')");
				}

				var rows = await conn.GetTrackDistanceAsync().ToArrayAsync();

				Assert.That(rows, Has.Length.EqualTo(1));
				Assert.That(rows[0].Track, Is.EqualTo("芝"));
				Assert.That(rows[0].TrackCondition, Is.EqualTo("良"));
				Assert.That(rows[0].Distance, Is.EqualTo("3000"));
				Assert.That(rows[0].Time, Is.EqualTo(187f));
				Assert.That(rows[0].LastThreeFurlongs, Is.EqualTo(33f));
			}
		}
	}
}
