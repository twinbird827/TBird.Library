using Microsoft.ML.Data;
using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public static partial class SQLite3Extensions
	{
		private static string[] Arr(params string[] arr) => arr;

		private static async Task Create(this SQLiteControl conn, string tablename, string[] columns, string[] keys)
		{
			var arr = columns.Select(x => new Column()
			{
				Name = x,
				Type = "TEXT",
				IsKey = keys.Contains(x)
			}).ToArray();

			await conn.Create(tablename, arr);
		}

		private static async Task Create(this SQLiteControl conn, string tablename, Column[] columns)
		{
			var columnString = columns.Select(x => x.GetColumn()).GetString(",");
			var keyString = columns.Where(x => x.IsKey).Select(x => x.Name).GetString(",");

			await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {tablename} ({columnString}, PRIMARY KEY ({keyString}));");
		}

		private static async Task InsertAsync(this SQLiteControl conn, string tablename, Dictionary<string, string> x)
		{
			var keyString = x.Keys.GetString(",");
			var prmString = x.Keys.Select(x => "?").GetString(",");
			var sql = $"REPLACE INTO {tablename} ({keyString}) VALUES ({prmString})";
			var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.Object, x[k])).ToArray();
			await conn.ExecuteNonQueryAsync(sql, prm);
		}

		private static async Task<bool> Exists(this SQLiteControl conn, string tablename, string columnname, object value)
		{
			var sql = $@"SELECT COUNT(*) FROM {tablename} WHERE {columnname} = ?";
			var cnt = await conn.ExecuteScalarAsync(sql, SQLiteUtil.CreateParameter(DbType.Object, value));
			return cnt.Int32() > 0;
		}

		public static Task<bool> ExistsOrigAsync(this SQLiteControl conn, string raceid)
		{
			return conn.Exists("t_orig_h", "ﾚｰｽID", raceid);
		}

		public class Column
		{
			public string GetColumn() => $"{Name} {Type}";

			public required string Name { get; set; }

			public required string Type { get; set; }

			public bool IsKey { get; set; }
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

		public static async IAsyncEnumerable<string> GetRemoveShortageMissingDatas(this SQLiteControl conn)
		{
			var sql = @$"
SELECT DISTINCT ﾚｰｽID FROM t_orig_d WHERE 着順 IS NULL OR 着順 = '' OR 着順 = 0 OR 着順 = '0'
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["ﾚｰｽID"].Str();
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
		private static readonly string[] col_orig_h = Arr("ﾚｰｽID", "ﾚｰｽ名", "開催日", "開催日数", "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2", "回り", "距離", "天候", "馬場", "馬場状態", "優勝賞金", "頭数", "障害");

		/// <summary>ﾚｰｽ明細</summary>
		private static readonly string[] col_orig_d = Arr("ﾚｰｽID", "着順", "枠番", "馬番", "馬ID", "馬性", "馬齢", "斤量", "騎手名", "騎手ID", "ﾀｲﾑ", "ﾀｲﾑ変換", "着差", "ﾀｲﾑ指数", "通過", "上り", "単勝", "人気", "体重", "増減", "備考", "調教場所", "調教師名", "調教師ID", "馬主名", "馬主ID", "賞金");

		private static readonly string[] col_uma = Arr("馬ID", "馬名", "父ID", "母父ID", "生年月日", "調教師ID", "調教師名", "馬主ID", "馬主名", "生産者ID", "生産者名", "セリ取引価格", "募集情報", "評価額");

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
							case "障害":
								return racearr.Any(x => x["ﾗﾝｸ1"].Contains("障")) ? "1" : "0";
							default:
								return x[s];
						}
					}
					first = false;
					await conn.InsertAsync("t_orig_h", col_orig_h.ToDictionary(s => s, s => ToValue(s)));
				}
				await conn.InsertAsync("t_orig_d", col_orig_d.ToDictionary(s => s, s => x[s]));
				await conn.InsertUmaInfoAsync(x["馬ID"], x["馬名"]);
			}
		}

		public static async Task InsertShutsubaAsync(this SQLiteControl conn, List<Dictionary<string, string>> racearr)
		{
			var first = true;

			foreach (var x in racearr)
			{
				if (first)
				{
					string ToValue(string s)
					{
						switch (s)
						{
							case "頭数":
								return racearr.Count.Str();
							case "障害":
								return racearr.Any(x => x["ﾗﾝｸ1"].Contains("障")) ? "1" : "0";
							default:
								return x[s];
						}
					}
					first = false;
					await conn.InsertAsync("t_orig_h", col_orig_h.ToDictionary(s => s, s => ToValue(s)));
				}
				await conn.InsertAsync("t_orig_d", col_orig_d.ToDictionary(s => s, s => x[s]));
				await conn.InsertUmaInfoAsync(x["馬ID"], x["馬名"]);
			}
		}

		public static async Task InsertUmaInfoAsync(this SQLiteControl conn, string uma, string name)
		{
			try
			{
				if (0 == await conn.ExecuteScalarAsync("SELECT COUNT(*) FROM t_uma WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.Object, uma)).RunAsync(x => x.GetInt32()))
				{
					var info = await NetkeibaGetter.GetUmaInfo(uma, name);

					info["評価額"] = await conn.GetUmaValuation(info);

					await conn.InsertAsync("t_uma", info);
				}
			}
			catch (Exception ex)
			{
				throw;
			}
		}

		private static async Task<string> GetUmaValuation(this SQLiteControl conn, Dictionary<string, string> dic)
		{
			var sql = $@"
WITH vv_uma AS (SELECT * FROM (SELECT 生年月日, MAX(セリ取引価格 * 1, 募集情報 * 0.7) 購入額, 馬主ID, 父ID, 母父ID, 生産者ID FROM t_uma) WHERE 購入額 > 0),
vvv_uma AS (SELECT * FROM vv_uma WHERE 生年月日 < ?)
SELECT COALESCE(
	(SELECT AVG(購入額) FROM vvv_uma WHERE 父ID = ? AND 母父ID = ?),
	(SELECT AVG(購入額) FROM vvv_uma WHERE 父ID = ?),
	(SELECT AVG(購入額) FROM vvv_uma WHERE 母父ID = ?),
	(SELECT AVG(購入額) FROM vvv_uma WHERE 馬主ID = ?),
	(SELECT AVG(購入額) FROM vvv_uma WHERE 生産者ID = ?),
	(SELECT AVG(購入額) FROM vvv_uma),
	(SELECT AVG(購入額) FROM vv_uma)
)";

			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, dic["生年月日"]),
				SQLiteUtil.CreateParameter(DbType.String, dic["父ID"]),
				SQLiteUtil.CreateParameter(DbType.String, dic["母父ID"]),
				SQLiteUtil.CreateParameter(DbType.String, dic["父ID"]),
				SQLiteUtil.CreateParameter(DbType.String, dic["母父ID"]),
				SQLiteUtil.CreateParameter(DbType.String, dic["馬主ID"]),
				SQLiteUtil.CreateParameter(DbType.String, dic["生産者ID"]),
			};

			return await conn.ExecuteScalarAsync(sql, parameters).RunAsync(x => x.Str());
		}

		public static async Task DropSTEP1Oikiri(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_oikiri");
		}

		public static async Task CreateOikiri(this SQLiteControl conn)
		{
			if (await conn.ExistsColumn("t_oikiri", "ﾚｰｽID")) return;

			await conn.Create(
				"t_oikiri",
				Arr("ﾚｰｽID", "枠番", "馬番", "馬ID", "コース", "馬場", "乗り役", "時間1", "時間2", "時間3", "時間4", "時間5", "時間評価1", "時間評価2", "時間評価3", "時間評価4", "時間評価5", "脚色", "一言", "評価"),
				Arr("ﾚｰｽID", "馬ID")
			);
		}

		public static async IAsyncEnumerable<string> GetOikiriTargets(this SQLiteControl conn)
		{
			var sql = @$"
SELECT ﾚｰｽID FROM t_orig_h WHERE ﾚｰｽID NOT IN (SELECT ﾚｰｽID FROM t_oikiri)
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["ﾚｰｽID"].Str();
			}
		}

		public static async Task InsertOikiriAsync(this SQLiteControl conn, string raceid)
		{
			if (!await conn.Exists("t_oikiri", "ﾚｰｽID", raceid))
			{
				var racearr = await NetkeibaGetter.GetOikiris(raceid);
				foreach (var x in racearr)
				{
					await conn.InsertAsync("t_oikiri", x);
				}
			}
		}

		public static async IAsyncEnumerable<string> GetUmaTargets(this SQLiteControl conn)
		{
			var sql = @$"
SELECT DISTINCT 馬ID FROM (SELECT 父ID 馬ID FROM t_uma UNION ALL SELECT 母父ID 馬ID FROM t_uma) WHERE 馬ID NOT IN (SELECT 馬ID FROM t_uma) AND 馬ID <> '' AND 馬ID IS NOT NULL ORDER BY 馬ID
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["馬ID"].Str();
			}
		}

		/// <summary>
		/// 教育ﾃﾞｰﾀ作成用ﾃｰﾌﾞﾙを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task DropSTEP2(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
		}

		public static async Task CreateModel(this SQLiteControl conn)
		{
			await conn.Create(
				"t_model",
				[
					new Column() { Name = "Label", Type = "INTEGER", IsKey = false },
					new Column() { Name = "RaceId", Type = "TEXT", IsKey = true },
					new Column() { Name = "Horse", Type = "TEXT", IsKey = true },
					new Column() { Name = "Features", Type = "BLOB", IsKey = false },
				]
			);

			// TODO indexの作成
		}

		public static async Task<bool> ExistsModelTableAsync(this SQLiteControl conn)
		{
			return await conn.ExistsColumn("t_model", "RaceId");
		}

		public static async Task InsertModelAsync(this SQLiteControl conn, OptimizedHorseFeatures[] features)
		{
			foreach (var x in features)
			{
				var parameters = new[]
				{
					SQLiteUtil.CreateParameter(DbType.Int32, x.Label),
					SQLiteUtil.CreateParameter(DbType.String, x.RaceId),
					SQLiteUtil.CreateParameter(DbType.String, x.Horse),
					SQLiteUtil.CreateParameter(DbType.Object, x.Serialize()),
				};
				await conn.ExecuteNonQueryAsync($"REPLACE INTO t_model (Label, RaceId, Horse, Features) VALUES (?, ?, ?, ?)", parameters);
			}
		}

		//public static async Task InsertModelAsync(this SQLiteControl conn, IEnumerable<OptimizedHorseFeatures> data)
		//{
		//	if (!data.Any()) return;

		//	using (await Locker.LockAsync(InsertModelKey))
		//	{
		//		foreach (var chunk in data.GroupBy(x => x.RaceId))
		//		{
		//			await conn.BeginTransaction();
		//			foreach (var x in chunk)
		//			{
		//				var properties = OptimizedHorseFeatures.GetProperties();
		//				var parameters = properties
		//					.Select(p => SQLiteUtil.CreateParameter(p.GetDBType(), p.Property.GetValue(x)))
		//					.ToArray();
		//				var items = properties.Select(p => p.Name).GetString(",");
		//				var values = properties.Select(p => "?").GetString(",");
		//				await conn.ExecuteNonQueryAsync($"REPLACE INTO t_model ({items}) VALUES ({values})", parameters);
		//			}
		//			conn.Commit();
		//		}
		//	}
		//}

		private static string InsertModelKey = Guid.NewGuid().ToString();

		public static async Task<bool> ExistsModelAsync(this SQLiteControl conn, string raceid)
		{
			var cnt = await conn.ExecuteScalarAsync(
				"SELECT COUNT(*) FROM t_orig_h WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			).RunAsync(x => x.GetInt32());
			return 0 < cnt;
		}

		public static async IAsyncEnumerable<Race> GetRaceAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
WHERE  CAST(h.障害 AS INTEGER) = 0
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new Race(x);
			}
		}

		public static async IAsyncEnumerable<Race> GetRaceAsync(this SQLiteControl conn, DateTime date)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
WHERE  CAST(h.障害 AS INTEGER) = 0 AND h.開催日 < ?
ORDER BY h.開催日, h.ﾚｰｽID
";

			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.Object, date.ToString("yyyy/MM/dd")),
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				yield return new Race(x);
			}
		}

		public static async IAsyncEnumerable<TrackConditionDistance> GetTrackDistanceAsync(this SQLiteControl conn)
		{
			var sql = @"
WITH
v_kyba AS (SELECT DISTINCT 馬場, 距離 FROM t_orig_h),
v_baba AS (SELECT DISTINCT 馬場状態 FROM t_orig_h),
v_allc AS (SELECT * FROM v_kyba, v_baba),
v_orig AS (SELECT 馬場, 馬場状態, 距離, CAST(上り AS REAL) 上り, CAST(ﾀｲﾑ変換 AS REAL) ﾀｲﾑ変換 FROM t_orig_h h, t_orig_d d WHERE h.ﾚｰｽID = d.ﾚｰｽID),
v_group1 AS (SELECT 馬場, 馬場状態, 距離, AVG(上り) 上り, AVG(ﾀｲﾑ変換) ﾀｲﾑ変換, COUNT(*) CNT FROM v_orig GROUP BY 馬場, 馬場状態, 距離),
v_group2 AS (SELECT 馬場,           距離, AVG(上り) 上り, AVG(ﾀｲﾑ変換) ﾀｲﾑ変換 FROM v_orig GROUP BY 馬場, 距離)
SELECT a.馬場, a.馬場状態, a.距離, (CASE WHEN o1.CNT > 100 THEN o1.上り ELSE o2.上り END) 上り, (CASE WHEN o1.CNT > 100 THEN o1.ﾀｲﾑ変換 ELSE o2.ﾀｲﾑ変換 END) ﾀｲﾑ変換 FROM v_allc a LEFT JOIN v_group1 o1 ON a.馬場 = o1.馬場 AND a.馬場状態 = o1.馬場状態 AND a.距離 = o1.距離 LEFT JOIN v_group2 o2 ON a.馬場 = o2.馬場 AND a.距離 = o2.距離
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new TrackConditionDistance(x);
			}
		}

		public static string GetRaceDetailSql()
		{
			return @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数,
       d.枠番, d.馬番, d.馬ID, d.騎手ID, d.調教師ID, u.父ID, u.母父ID, u.生産者ID, d.着順, d.ﾀｲﾑ変換, (h.優勝賞金 / MIN(d.着順, h.頭数)) 賞金, u.評価額, u.生年月日, d.斤量, d.通過, d.上り, d.馬性, d.ﾀｲﾑ指数, d.着差, CAST(d.体重 AS REAL) 体重, CAST(d.増減 AS REAL) 増減,
       o.コース, o.馬場, o.乗り役, CAST(o.時間1 AS REAL) 時間1, CAST(o.時間2 AS REAL) 時間2, CAST(o.時間3 AS REAL) 時間3, CAST(o.時間4 AS REAL) 時間4, CAST(o.時間5 AS REAL) 時間5, o.時間評価1, o.時間評価2, o.時間評価3, o.時間評価4, o.時間評価5, o.脚色, o.一言, o.評価
FROM   t_orig_h h, t_orig_d d, t_uma u, t_oikiri o
WHERE  h.ﾚｰｽID = d.ﾚｰｽID AND d.馬ID = u.馬ID AND d.ﾚｰｽID = o.ﾚｰｽID AND d.馬ID = o.馬ID
";
		}

		public static async IAsyncEnumerable<RaceDetail> GetRaceDetailsAsync(this SQLiteControl conn, Race race)
		{
			var sql = $@"{GetRaceDetailSql()}
AND h.ﾚｰｽID = ?
ORDER BY h.開催日 DESC, h.ﾚｰｽID ASC, d.馬番 ASC
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, race.RaceId),
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				yield return new RaceDetail(x, race);
			}
		}

		public static async IAsyncEnumerable<string> GetAlreadyCreatedRacesAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT DISTINCT RaceId
FROM   t_model h
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["RaceId"].Str();
			}
		}

		public static async Task<OptimizedHorseFeatures[]> GetModelAsync(this SQLiteControl conn, DateTime start, DateTime end)
		{
			var sql = @"
SELECT m.*
FROM   t_orig_h h, t_model m
WHERE  CAST(h.開催日数 AS INTEGER) BETWEEN ? AND ?
AND    h.ﾚｰｽID                 = m.RaceId
AND    CAST(h.障害 AS INTEGER) = 0
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.Int32, AppUtil.ToTotalDays(start)),
				SQLiteUtil.CreateParameter(DbType.Int32, AppUtil.ToTotalDays(end)),
			};

			var dbarr = await conn.GetRows(sql, parameters);

			var results = dbarr.SelectInParallel(x =>
			{
				OptimizedHorseFeatures model;
				try
				{
					model = OptimizedHorseFeatures.Deserialize(x).NotNull();
				}
				catch (Exception ex)
				{
					MessageService.Debug(ex.ToString());
					throw;
				}
				return model;
			});

			return results;
		}

		public static async IAsyncEnumerable<Race> GetShutsubaRaceAsync(this SQLiteControl conn, IEnumerable<string> raceids)
		{
			var sql = $@"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
WHERE  h.ﾚｰｽID IN ({raceids.Select(_ => "?").GetString(",")})
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql, raceids.Select(x => SQLiteUtil.CreateParameter(DbType.String, x)).ToArray()))
			{
				yield return new Race(x);
			}
		}

		//		public static async Task<List<RaceDetail>> GetShutsubaRaceDetailAsync(this SQLiteControl conn, DateTime date, params (string Key, string Value)[] kvp)
		//		{
		//			var sql = $@"
		//SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数, d.馬番, d.馬ID, d.騎手ID, d.調教師ID, u.父ID, u.母父ID, u.生産者ID, d.着順, d.ﾀｲﾑ変換, d.賞金, u.評価額, u.生年月日, d.斤量, d.通過, d.上り, d.馬性, d.ﾀｲﾑ指数, d.着差, o.コース, o.馬場, o.乗り役, CAST(o.時間1 AS REAL) 時間1, CAST(o.時間2 AS REAL) 時間2, CAST(o.時間3 AS REAL) 時間3, CAST(o.時間4 AS REAL) 時間4, CAST(o.時間5 AS REAL) 時間5, o.時間評価1, o.時間評価2, o.時間評価3, o.時間評価4, o.時間評価5, o.脚色, o.一言, o.評価, CAST(d.体重 AS REAL) 体重, CAST(d.増減 AS REAL) 増減
		//FROM   t_orig_h h, v_orig_d d, t_uma u, t_oikiri o
		//WHERE  h.ﾚｰｽID = d.ﾚｰｽID AND d.馬ID = u.馬ID AND h.開催日 < ? AND {kvp.Select(x => $"{x.Key} = ?").GetString(" AND ")} AND d.ﾚｰｽID = o.ﾚｰｽID AND d.馬ID = o.馬ID
		//ORDER BY h.開催日 ASC, h.ﾚｰｽID ASC
		//";
		//			var parameters = new[]
		//			{
		//				SQLiteUtil.CreateParameter(DbType.String, date.ToString("yyyy/MM/dd")),
		//			}.Concat(
		//				kvp.Select(x => SQLiteUtil.CreateParameter(DbType.String, x.Value))
		//			).ToArray();

		//			var results = new List<RaceDetail>();
		//			foreach (var row in await conn.GetRows(sql, parameters).RunAsync(arr => arr.Select(x => new RaceDetail(x, new Race(x))).ToList()))
		//			{
		//				row.Initialize(results);
		//				results.Insert(0, row);
		//			}
		//			return results;
		//		}

		public static async Task DeleteOrigAsync(this SQLiteControl conn, string[] raceids)
		{
			var parameters = raceids.Select(x => SQLiteUtil.CreateParameter(DbType.String, x)).ToArray();

			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_d WHERE ﾚｰｽID IN ({raceids.Select(x => "?").GetString(",")})", parameters);
			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_h WHERE ﾚｰｽID IN ({raceids.Select(x => "?").GetString(",")})", parameters);
		}

		public static async IAsyncEnumerable<Race> GetShutsubaRaceAsync(this SQLiteControl conn, string raceid)
		{
			var sql = $@"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
WHERE  h.ﾚｰｽID = ?
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql, SQLiteUtil.CreateParameter(DbType.String, raceid)))
			{
				yield return new Race(x);
			}
		}

		public static async Task<List<RaceDetail>> GetShutsubaRaceDetailAsync(this SQLiteControl conn, DateTime date, params (string Key, string Value)[] kvp)
		{
			var sql = $@"{GetRaceDetailSql()}
AND h.開催日 < ? AND {kvp.Select(x => $"{x.Key} = ?").GetString(" AND ")}
ORDER BY h.開催日 DESC, h.ﾚｰｽID ASC, d.馬番 ASC
LIMIT  1000
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, date.ToString("yyyy/MM/dd")),
			}.Concat(
				kvp.Select(x => SQLiteUtil.CreateParameter(DbType.String, x.Value))
			).ToArray();

			var results = new List<RaceDetail>();
			foreach (var row in await conn.GetRows(sql, parameters).RunAsync(arr => arr.Select(x => new RaceDetail(x, new Race(x))).ToList()))
			{
				results.Add(row);
			}
			return results;
		}

		public static async Task DeleteOrigAsync(this SQLiteControl conn, string raceid)
		{
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			};

			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_d WHERE ﾚｰｽID = ?", parameters);
			await conn.ExecuteNonQueryAsync($"DELETE FROM t_orig_h WHERE ﾚｰｽID = ?", parameters);
		}

	}

	public class CustomProperty
	{
		public CustomProperty(PropertyInfo property, string name, Type type, FeaturesAttribute? attribute)
		{
			Property = property;
			Name = name;
			Type = type;
			Attribute = attribute;
		}

		public PropertyInfo Property { get; set; }
		public string Name { get; set; }
		public Type Type { get; set; }
		public FeaturesAttribute? Attribute { get; set; }

		public string GetTypeString() => Type.Name switch
		{
			"Single" => "REAL",
			"UInt32" => "INTEGER",
			"Int32" => "INTEGER",
			"Boolean" => "INTEGER",
			_ => "TEXT"
		};

		public DbType GetDBType() => Type.Name switch
		{
			"Single" => DbType.Single,
			"UInt32" => DbType.Int32,
			"Int32" => DbType.Int32,
			"Boolean" => DbType.Int32,
			_ => DbType.String
		};

		public void SetProperty(OptimizedHorseFeatures instance, Dictionary<string, object> x)
		{
			switch (Type.Name)
			{
				case "Single":
					Property.SetValue(instance, x[Name].Single());
					break;
				case "UInt32":
					Property.SetValue(instance, x[Name].UInt32());
					break;
				case "Int32":
					Property.SetValue(instance, x[Name].Int32());
					break;
				case "Boolean":
					Property.SetValue(instance, x[Name].Int32() > 0);
					break;
				default:
					Property.SetValue(instance, x[Name]);
					break;
			}
		}
	}
}