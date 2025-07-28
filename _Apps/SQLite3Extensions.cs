using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public static class SQLite3Extensions
	{
		private static string[] Arr(params string[] arr) => arr;

		private static async Task Create(this SQLiteControl conn, string tablename, string[] columns, string[] keys)
		{
			await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {tablename} ({columns.GetString(",")}, PRIMARY KEY ({keys.GetString(",")}));");
		}

		private static async Task InsertAsync(this SQLiteControl conn, string tablename, Dictionary<string, string> x)
		{
			var keyString = x.Keys.GetString(",");
			var prmString = x.Keys.Select(x => "?").GetString(",");
			var sql = $"REPLACE INTO {tablename} ({keyString}) VALUES ({prmString})";
			var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.Object, x[k])).ToArray();
			await conn.ExecuteNonQueryAsync(sql, prm);
		}

		/// <summary>
		/// 教育ﾃﾞｰﾀ作成用ﾃｰﾌﾞﾙを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task DropAllTablesAsync(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_orig");
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_shutuba");
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
		}

		/// <summary>
		/// 確定前のﾃﾞｰﾀを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task RemoveShortageMissingDatasAsync(this SQLiteControl conn)
		{
			if (await conn.ExistsColumn("t_orig", "ﾚｰｽID"))
			{
				await conn.BeginTransaction();
				await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 IS NULL");
				await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 = ''");
				await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 = 0");
				conn.Commit();
			}
		}

		/// <summary>
		/// 最後に確定ﾃﾞｰﾀを取得した月を取得します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task<int> GetLastMonth(this SQLiteControl conn)
		{
			var datestring = await conn.ExecuteScalarAsync("SELECT IFNULL(MAX(開催日), '2000/01/01') FROM t_orig_h").RunAsync(x => x.Str());
			return DateTime.Parse(datestring).Month;
		}

		/// <summary>馬ﾍｯﾀﾞ</summary>
		private static readonly string[] col_orig_h = Arr("ﾚｰｽID", "ﾚｰｽ名", "開催日", "開催日数", "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2", "回り", "距離", "天候", "馬場", "馬場状態");

		/// <summary>馬明細</summary>
		private static readonly string[] col_orig_d = Arr("ﾚｰｽID", "着順", "枠番", "馬番", "馬名", "馬ID", "馬性", "馬齢", "斤量", "騎手名", "騎手ID", "ﾀｲﾑ", "ﾀｲﾑ変換", "着差", "ﾀｲﾑ指数", "通過", "上り", "単勝", "人気", "体重", "増減", "備考", "調教場所", "調教師名", "調教師ID", "馬主名", "馬主ID", "賞金");

		public static async Task<bool> CreateOrigAndBeginTransaction(this SQLiteControl conn, bool create)
		{
			if (create)
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
					"t_orig_k",
					Arr("馬ID", "父ID", "母ID"),
					Arr("馬ID")
				);

				// TODO indexの作成

				await conn.BeginTransaction();
			}

			return false;
		}

		public static async Task InsertOrigAsync(this SQLiteControl conn, List<Dictionary<string, string>> racearr)
		{
			async Task InsertKettoAsync(string uma)
			{
				if (0 == await conn.ExecuteScalarAsync("SELECT COUNT(*) FROM t_orig_k WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.Object, uma)).RunAsync(x => x.GetInt32()))
				{
					await foreach (var ketto in NetkeibaGetter.GetKetto(uma))
					{
						await conn.InsertAsync("t_orig_k", ketto);
					}
				}
			}

			var first = true;

			foreach (var x in racearr)
			{
				if (first)
				{
					first = false;
					await conn.InsertAsync("t_orig_h", col_orig_h.ToDictionary(s => s, s => x[s]));
				}
				await conn.InsertAsync("t_orig_d", col_orig_d.ToDictionary(s => s, s => x[s]));
				await InsertKettoAsync(x["馬ID"]);
			}
		}
	}
}