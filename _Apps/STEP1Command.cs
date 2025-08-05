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
					await conn.DropSTEP1();
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
					foreach (var date in dates)
					{
						var racebases = await NetkeibaGetter.GetRaceIds(DateTime.ParseExact(date, "yyyyMMdd", null));
						var existsrace = false;
						foreach (var racebase in racebases)
						{
							await conn.BeginTransaction();
							await foreach (var racearr in GetSTEP1Racearrs(conn, racebase))
							{
								await conn.InsertOrigAsync(racearr);
							}
							conn.Commit();
							AddLog($"completed racebase:{racebase}");

							Progress.Value += 1D / racebases.Count();
							existsrace = true;
						}
						if (!existsrace) Progress.Value += 1;
					}

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

	public static partial class SQLite3Extensions
	{
		/// <summary>
		/// 教育ﾃﾞｰﾀ作成用ﾃｰﾌﾞﾙを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task DropSTEP1(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_orig_h");
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_orig_d");
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_uma");
		}

		/// <summary>
		/// 確定前のﾃﾞｰﾀを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task RemoveShortageMissingDatasAsync(this SQLiteControl conn)
		{
			if (await conn.ExistsColumn("t_orig_d", "ﾚｰｽID"))
			{
				await conn.BeginTransaction();
				await conn.ExecuteNonQueryAsync("DELETE FROM t_orig_d WHERE 着順 IS NULL");
				await conn.ExecuteNonQueryAsync("DELETE FROM t_orig_d WHERE 着順 = ''");
				await conn.ExecuteNonQueryAsync("DELETE FROM t_orig_d WHERE 着順 = 0");
				conn.Commit();
			}
		}

		/// <summary>
		/// 最後に確定ﾃﾞｰﾀを取得した月を取得します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task<DateTime> GetLastMonth(this SQLiteControl conn)
		{
			var datestring = await conn.ExistsColumn("t_orig_h", "ﾚｰｽID")
				? await conn.ExecuteScalarAsync("SELECT IFNULL(MAX(開催日), '2010/01/01') FROM t_orig_h").RunAsync(x => x.Str())
				: "2010/01/01";
			return DateTime.Parse(datestring);
		}

		/// <summary>ﾚｰｽﾍｯﾀﾞ</summary>
		private static readonly string[] col_orig_h = Arr("ﾚｰｽID", "ﾚｰｽ名", "開催日", "開催日数", "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2", "回り", "距離", "天候", "馬場", "馬場状態", "優勝賞金", "頭数");

		/// <summary>ﾚｰｽ明細</summary>
		private static readonly string[] col_orig_d = Arr("ﾚｰｽID", "着順", "枠番", "馬番", "馬ID", "馬性", "馬齢", "斤量", "騎手名", "騎手ID", "ﾀｲﾑ", "ﾀｲﾑ変換", "着差", "ﾀｲﾑ指数", "通過", "上り", "単勝", "人気", "体重", "増減", "備考", "調教場所", "調教師名", "調教師ID", "馬主名", "馬主ID", "賞金");

		private static readonly string[] col_uma = Arr("馬ID", "馬名", "父ID", "母父ID", "生年月日", "調教師ID", "調教師名", "馬主ID", "馬主名", "生産者ID", "生産者名", "セリ取引価格", "募集情報");

		public static async Task CreateOrig(this SQLiteControl conn)
		{
			await conn.Create(
				"t_orig_h",
				col_orig_h,
				Arr("ﾚｰｽID")
			);

			// TODO 開催日時と着順をINTEGERにする

			await conn.Create(
				"t_orig_d",
				col_orig_d,
				Arr("ﾚｰｽID", "馬番")
			);

			await conn.Create(
				"t_uma",
				col_uma,
				Arr("馬ID")
			);

			// TODO indexの作成
		}

		public static async Task InsertOrigAsync(this SQLiteControl conn, List<Dictionary<string, string>> racearr)
		{
			async Task InsertKettoAsync(string uma, string name)
			{
				if (0 == await conn.ExecuteScalarAsync("SELECT COUNT(*) FROM t_uma WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.Object, uma)).RunAsync(x => x.GetInt32()))
				{
					await conn.InsertAsync("t_uma", await NetkeibaGetter.GetUmaInfo(uma, name));
				}
			}

			var first = true;

			foreach (var x in racearr)
			{
				if (first)
				{
					string ToValue(string s)
					{
						switch (s)
						{
							case "優勝賞金":
								return racearr.Sum(x => x["賞金"].GetDouble()).Str();
							case "頭数":
								return racearr.Count.Str();
							default:
								return x[s];
						}
					}
					first = false;
					await conn.InsertAsync("t_orig_h", col_orig_h.ToDictionary(s => s, s => ToValue(s)));
				}
				await conn.InsertAsync("t_orig_d", col_orig_d.ToDictionary(s => s, s => x[s]));
				await InsertKettoAsync(x["馬ID"], x["馬名"]);
			}
		}

		public static async Task<bool> ExistsOrigAsync(this SQLiteControl conn, string raceid)
		{
			var cnt = await conn.ExecuteScalarAsync(
				"SELECT COUNT(*) FROM t_orig_h WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			).RunAsync(x => x.GetInt32());
			return 0 < cnt;
		}
	}
}