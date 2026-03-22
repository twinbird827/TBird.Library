using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using Tensorflow.Keras.Layers;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Netkeiba
{
	public class STEP1Command : STEPBase
	{
		public STEP1Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var create = VM.S1Overwrite.IsChecked || !File.Exists(AppUtil.Sqlitepath);

				if (create && !MessageService.Confirm("netkeibaから全データを取得し直します。よろしいでしょうか。"))
				{
					return;
				}

				if (create)
				{
					// ﾃﾞｰﾀﾍﾞｰｽﾃﾞｨﾚｸﾄﾘ作成
					DirectoryUtil.Create(Path.GetDirectoryName(AppUtil.Sqlitepath));

					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropSTEP1();
				}

				// CREATE
				await conn.CreateOrig();

				// 欠落ﾃﾞｰﾀを除外
				await conn.RemoveShortageMissingDatasAsync();

				await Task.Delay(1000);

				var sdate = create ? new DateTime(VM.SYear, 1, 1) : await conn.GetLastMonth();
				var edate = DateTime.Now.AddDays(-4);

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = (edate.Year - sdate.Year) * 12 + edate.Month - sdate.Month + 1;

				for (var i = 0; i < (int)Progress.Maximum; i++)
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

							Progress.Value += 1D / dates.Length / racebases.Length;
							existsrace = true;
						}
						if (!existsrace) Progress.Value += 1D / dates.Length;
					}

				}

				foreach (var racebase in conn.GetRemoveShortageMissingDatas().ToBlockingEnumerable().ToArray())
				{
					await conn.BeginTransaction();
					foreach (var racearr in await GetSTEP1Racearrs(conn, racebase).ToArrayAsync())
					{
						await conn.InsertOrigAsync(racearr);
						await conn.InsertOikiriAsync(racebase);

					}
					conn.Commit();
					MessageService.Debug($"completed racebase:{racebase}");
				}
			}
		}

		private async IAsyncEnumerable<List<Dictionary<string, string>>> GetSTEP1Racearrs(SQLiteControl conn, string raceid)
		{
			if (!await conn.ExistsOrigAsync(raceid))
			{
				var arr = await NetkeibaGetter.GetRaceResults(raceid);

				if (arr.Any(x => x["回り"] != "障" && string.IsNullOrEmpty(x["ﾀｲﾑ指数"]))) yield break;

				yield return arr;
			}
		}
	}
}