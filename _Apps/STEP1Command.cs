using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using System.IO;
using TBird.DB.SQLite;
using System.Data;

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

				if (create)
				{
					// ﾃﾞｰﾀﾍﾞｰｽﾃﾞｨﾚｸﾄﾘ作成
					DirectoryUtil.Create(Path.GetDirectoryName(AppUtil.Sqlitepath));

					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropAllTablesAsync();
				}

				// CREATE
				await conn.CreateOrig();

				// 欠落ﾃﾞｰﾀを除外
				await conn.RemoveShortageMissingDatasAsync();

				await Task.Delay(1000);

				var sdate = create ? new DateTime(VM.SYear, 1, 1) : await conn.GetLastMonth();
				var edate = DateTime.Now.AddDays(-1);

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = (edate.Year - sdate.Year) * 12 + edate.Month - sdate.Month + 1;

				for (var i = 0; i < (int)Progress.Maximum; i++)
				{
					var dates = await sdate
						.AddMonths(i)
						.Run(target => NetkeibaGetter.GetKaisaiDate(target.Year, target.Month));
					var racebases = await dates
						.Select(date => NetkeibaGetter.GetRaceIds(DateTime.ParseExact(date, "yyyyMMdd", null)))
						.WhenAllExpand();

					await conn.BeginTransaction();

					foreach (var racebase in racebases)
					{

						await foreach (var racearr in GetSTEP1Racearrs(conn, racebase))
						{
							await conn.InsertOrigAsync(racearr);
						}

						AddLog($"completed racebase:{racebase}");

						Progress.Value += 1D / racebases.Count();
					}

					conn.Commit();
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