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

		public class Column
		{
			public string GetColumn() => $"{Name} {Type}";

			public string Name { get; set; }

			public string Type { get; set; }

			public bool IsKey { get; set; }
		}
	}
}