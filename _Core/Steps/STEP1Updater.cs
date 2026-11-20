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
					DirectoryUtil.Create(Path.GetDirectoryName(AppUtil.Sqlitepath));

					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropSTEP1();
				}

				await conn.CreateOrig();
				await conn.CreateOikiri();

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

				await SupplementAsync(conn);
			}
		}

		/// <summary>追切と血統（父・母父）の馬情報の欠けを、対象が尽きるまで取得して補う</summary>
		public static async Task SupplementAsync(SQLiteControl conn)
		{
			await conn.CreateOikiri();

			var retry = false;
			do
			{
				retry = false;

				foreach (var raceid in await conn.GetOikiriTargets().ToArrayAsync())
				{
					await conn.BeginTransaction();
					await conn.InsertOikiriAsync(raceid);
					conn.Commit();
					retry = true;
				}

				foreach (var uma in await conn.GetUmaTargets().ToArrayAsync())
				{
					await conn.BeginTransaction();
					await conn.InsertUmaInfoAsync(uma, string.Empty);
					conn.Commit();
					retry = true;
				}
			} while (retry);
		}

		/// <summary>t_orig_h の全ﾚｰｽの備考を ﾚｰｽID の昇順に取り直す。済んだﾚｰｽは t_bikou_done で飛ばすので、止めても続きから再開できる</summary>
		public static async Task BackfillBikouAsync(IProgress<double> progress)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				await conn.CreateOrig();

				var raceids = await conn.GetBikouTargets().ToArrayAsync();
				progress.Report(0);

				for (var i = 0; i < raceids.Length; i++)
				{
					var raceid = raceids[i];
					var arr = await NetkeibaGetter.GetRaceResults(raceid);

					// t_orig_h のﾚｰｽには必ず結果の表があり、空は取得の失敗。済みにすると二度と取り直されない
					if (arr.Count == 0) throw new InvalidOperationException($"ﾚｰｽ結果を取得できません。raceid:{raceid}");
					ThrowIfTimeIndexInvalid(arr, raceid);

					await conn.BeginTransaction();
					await conn.UpdateBikouAsync(raceid, arr);
					conn.Commit();
					MessageService.Debug($"completed bikou:{raceid}");

					progress.Report((i + 1D) / raceids.Length);
				}
			}
		}

		private static void ThrowIfTimeIndexInvalid(List<Dictionary<string, string>> arr, string raceid)
		{
			if (arr.Any(x => !string.IsNullOrWhiteSpace(x["ﾀｲﾑ指数"]) && !int.TryParse(x["ﾀｲﾑ指数"], out _)))
			{
				throw new InvalidOperationException($"ﾀｲﾑ指数が数値ではありません。netkeibaにﾛｸﾞｲﾝできていない可能性があります。raceid:{raceid}");
			}
		}

		private static async IAsyncEnumerable<List<Dictionary<string, string>>> GetSTEP1Racearrs(SQLiteControl conn, string raceid)
		{
			if (!await conn.ExistsOrigAsync(raceid))
			{
				var arr = await NetkeibaGetter.GetRaceResults(raceid);

				ThrowIfTimeIndexInvalid(arr, raceid);

				if (arr.Any(x => x["回り"] != "障" && string.IsNullOrEmpty(x["ﾀｲﾑ指数"]))) yield break;

				yield return arr;
			}
		}
	}
}
