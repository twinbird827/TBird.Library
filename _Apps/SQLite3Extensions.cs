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
	public static partial class SQLite3Extensions
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

	}
}