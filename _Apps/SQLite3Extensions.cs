using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public static class SQLite3Extensions
	{
		public static async Task DropAllTablesAsync(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_orig");
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_shutuba");
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
		}

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

		public static async Task<int> GetLastMonth(this SQLiteControl conn)
		{
			return DateTime.Parse(await TOrigH.GetLastDateAsync(conn)).Month;
		}
	}

	internal static class VOrig
	{
		public const string Tablename = "t_orig_h";

		public static readonly string[] Columns = new[]
		{
			"ﾚｰｽID","ﾚｰｽ名","開催日","開催日数","開催場所","ﾗﾝｸ1","ﾗﾝｸ2","回り","距離","天候","馬場","馬場状態"
		};

	}

	internal static class TOrigH
	{
		public const string Tablename = "t_orig_h";

		public static readonly string[] Keys = new[]
		{
			"ﾚｰｽID"
		};

		public static readonly string[] Columns = new[]
		{
			"ﾚｰｽID","ﾚｰｽ名","開催日","開催日数","開催場所","ﾗﾝｸ1","ﾗﾝｸ2","回り","距離","天候","馬場","馬場状態"
		};

		public static async Task Drop(SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {Tablename}");
		}

		public static async Task Create(SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {Tablename} ({Columns.GetString(",")}, PRIMARY KEY ({Keys.GetString(",")}));");
		}

		public static async Task<string> GetLastDateAsync(SQLiteControl conn)
		{
			return await conn.ExecuteScalarAsync("SELECT IFNULL(MAX(開催日), '2000/01/01') FROM t_orig_h").RunAsync(x => x.Str());
		}
	}

	internal static class TOrigD
	{
		public const string Tablename = "t_orig_d";

		public static readonly string[] Keys = new[]
		{
			"ﾚｰｽID","馬番"
		};

		public static readonly string[] Columns = new[]
		{
			"ﾚｰｽID","着順","枠番","馬番","馬名","馬ID","馬性","馬齢","斤量","騎手名","騎手ID","ﾀｲﾑ","ﾀｲﾑ変換","着差","ﾀｲﾑ指数","通過","上り","単勝","人気","体重","増減","備考","調教場所","調教師名","調教師ID","馬主名","馬主ID","賞金",
		};

		public static async Task Drop(SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {Tablename}");
		}

		public static async Task Create(SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {Tablename} ({Columns.GetString(",")}, PRIMARY KEY ({Keys.GetString(",")}));");
		}

	}

	internal static class TOrigT
	{
		public const string Tablename = "t_orig_t";

		public static readonly string[] Keys = new[]
		{
			"ﾚｰｽID","馬番"
		};

		public static readonly string[] Columns = new[]
		{
			"ﾚｰｽID","馬番",
		};

		public static async Task Drop(SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {Tablename}");
		}

		public static async Task Create(SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {Tablename} ({Columns.GetString(",")}, PRIMARY KEY ({Keys.GetString(",")}));");
		}

	}

}