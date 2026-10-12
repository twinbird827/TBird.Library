using System.IO;
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
	}
}
