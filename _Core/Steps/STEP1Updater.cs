using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public static class STEP1Updater
	{
		public static async Task UpdateAsync(bool create, int syear, IProgress<double> progress)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				if (create)
				{
					// ﾃﾞｰﾀﾍﾞｰｽﾃﾞｨﾚｸﾄﾘ作成
					DirectoryUtil.Create(Path.GetDirectoryName(AppUtil.Sqlitepath));

					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropSTEP1();
				}

				// CREATE
				await conn.CreateOrig();
				await conn.CreateOikiri();

				// 欠落ﾃﾞｰﾀを除外
				await conn.RemoveShortageMissingDatasAsync();

				await Task.Delay(1000);

				var sdate = create ? new DateTime(syear, 1, 1) : await conn.GetLastMonth();
				var edate = DateTime.Now.AddDays(-4);

				var months = (edate.Year - sdate.Year) * 12 + edate.Month - sdate.Month + 1;
				var value = 0D;
				progress.Report(0);

				for (var i = 0; i < months; i++)
				{
					var dates = await sdate
						.AddDays(sdate.Day * -1 + 1)
						.AddMonths(i)
						.Run(target => NetkeibaGetter.GetKaisaiDate(target.Year, target.Month))
						.RunAsync(dates => dates
							.Select(x => DateTime.ParseExact(x, "yyyyMMdd", null))
							.Where(x => x < edate)
							.ToArray()
						);

					foreach (var date in dates)
					{
						var racebases = await NetkeibaGetter.GetRaceIds(date).RunAsync(x => x.ToArray());
						var existsrace = false;
						foreach (var racebase in racebases)
						{
							await conn.BeginTransaction();
							foreach (var racearr in await GetSTEP1Racearrs(conn, racebase).ToArrayAsync())
							{
								await conn.InsertOrigAsync(racearr);
								await conn.InsertOikiriAsync(racebase);

							}
							conn.Commit();
							MessageService.Debug($"completed racebase:{racebase}");

							value += 1D / dates.Length / racebases.Length;
							progress.Report(value / months);
							existsrace = true;
						}
						if (!existsrace)
						{
							value += 1D / dates.Length;
							progress.Report(value / months);
						}
					}

				}
			}
		}

		private static async IAsyncEnumerable<List<Dictionary<string, string>>> GetSTEP1Racearrs(SQLiteControl conn, string raceid)
		{
			if (!await conn.ExistsOrigAsync(raceid))
			{
				var arr = await NetkeibaGetter.GetRaceResults(raceid);

				if (arr.Any(x => !string.IsNullOrWhiteSpace(x["ﾀｲﾑ指数"]) && !int.TryParse(x["ﾀｲﾑ指数"], out _)))
				{
					throw new InvalidOperationException($"ﾀｲﾑ指数が数値ではありません。netkeibaにﾛｸﾞｲﾝできていない可能性があります。raceid:{raceid}");
				}

				if (arr.Any(x => x["回り"] != "障" && string.IsNullOrEmpty(x["ﾀｲﾑ指数"]))) yield break;

				yield return arr;
			}
		}
	}
}
