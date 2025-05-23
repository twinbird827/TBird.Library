using System.Data;
using System.Data.SQLite;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.DB.SQLite
{
	public static class SQLiteUtil
	{
		/// <summary>
		/// LIKE句でｴｽｹｰﾌﾟに使用する文字
		/// </summary>
		private static string EscapeString => @"\";

		public static async Task BackupAsync(SQLiteControl src, string path)
		{
			FileUtil.BeforeCreate(path);

			using (await Locker.LockAsync(src.Lock))
			using (var dst = new SQLiteControl($"datasource={path}"))
			{
				src._m._conn.BackupDatabase(dst._m._conn, "main", "main", -1, null, 0);
			}
		}

		/// <summary>
		/// Sqlite3用ﾊﾟﾗﾒｰﾀを作成します。
		/// </summary>
		/// <param name="type">ﾊﾟﾗﾒｰﾀの型</param>
		/// <param name="value">ﾊﾟﾗﾒｰﾀに設定する値</param>
		/// <returns></returns>
		public static SQLiteParameter CreateParameter(DbType type, object value)
		{
			return new SQLiteParameter()
			{
				DbType = type,
				Value = value
			};
		}

		/// <summary>
		/// LIKE句のｴｽｹｰﾌﾟが必要な文字をｴｽｹｰﾌﾟします。
		/// </summary>
		/// <param name="value">元の文字列</param>
		/// <returns></returns>
		public static string ToEscape(string value)
		{
			return value.ToEscape(EscapeString).ToEscape("%").ToEscape("_");
		}

		/// <summary>
		/// LIKE句の指定された要ｴｽｹｰﾌﾟ文字をｴｽｹｰﾌﾟします。
		/// </summary>
		/// <param name="value">元の文字列</param>
		/// <param name="escape">要ｴｽｹｰﾌﾟ文字</param>
		/// <returns></returns>
		private static string ToEscape(this string value, string escape)
		{
			return value.Replace(escape, $"{EscapeString}{escape}");
		}

		/// <summary>
		/// 指定したﾃｰﾌﾞﾙに指定した列が存在するか確認します。
		/// </summary>
		/// <param name="conn">ｺﾏﾝﾄﾞ</param>
		/// <param name="table">ﾃｰﾌﾞﾙ</param>
		/// <param name="column">列</param>
		/// <returns>存在する: true / しない: false</returns>
		public static async Task<bool> ExistsColumn(this SQLiteControl conn, string table, string column)
		{
			var parameters = new[]
			{
				CreateParameter(DbType.String, table),
				CreateParameter(DbType.String, column),
			};
			var count = await conn.ExecuteScalarAsync<long>(
				$"SELECT COUNT(*) FROM PRAGMA_TABLE_INFO(?) WHERE NAME=?",
				parameters
			);
			return 0 < count;
		}

	}
}