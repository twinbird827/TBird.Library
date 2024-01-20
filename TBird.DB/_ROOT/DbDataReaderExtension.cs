using System.Collections.Generic;
using System;
using System.Data.Common;
using System.Threading.Tasks;
using System.Linq;

namespace TBird.DB
{
	public static class DbDataReaderExtension
	{
		/// <summary>
		/// DbDataReaderから指定したｲﾝﾃﾞｯｸｽにある値を指定した型で取得します。
		/// </summary>
		/// <typeparam name="T">型</typeparam>
		/// <param name="reader">DbDataReader</param>
		/// <param name="index">ｲﾝﾃﾞｯｸｽ</param>
		/// <returns></returns>
		public static T Get<T>(this DbDataReader reader, int index)
		{
			return DbUtil.GetValue<T>(reader.GetValue(index));
		}

		/// <summary>
		/// DbDataReaderを全件読み出し、ﾘｽﾄとして取得します。
		/// </summary>
		/// <typeparam name="T">1行の型</typeparam>
		/// <param name="reader">DbDataReader</param>
		/// <param name="func">1行読み出し用の処理内容</param>
		/// <returns></returns>
		public static async Task<List<T>> GetRows<T>(this DbDataReader reader, Func<DbDataReader, T> func)
		{
			var ret = new List<T>();
			while (await reader.ReadAsync())
			{
				ret.Add(func(reader));
			}
			return ret;
		}

		public static async Task<List<T>> GetRows<T>(this DbControl conn, Func<DbDataReader, T> func, string sql, params DbParameter[] parameters)
		{
			using (var reader = await conn.ExecuteReaderAsync(sql, parameters))
			{
				return await reader.GetRows(func);
			}
		}

		public static Task<List<Dictionary<string, object>>> GetRows(this DbControl conn, string sql, params DbParameter[] parameters)
		{
			Func<DbDataReader, Dictionary<string, object>> func = r =>
			{
				var indexes = Enumerable.Range(0, r.FieldCount)
					.Where(i => i == 0 || !Enumerable.Range(0, i - 1).Any(x => r.GetName(i) == r.GetName(x)))
					.ToArray();
				return indexes.ToDictionary(i => r.GetName(i), i => r.GetValue(i));
			};

			return conn.GetRows(func, sql, parameters);
		}

		/// <summary>
		/// DbDataReaderを1件読み出し、ｵﾌﾞｼﾞｪｸﾄとして取得します。
		/// </summary>
		/// <typeparam name="T">1行の型</typeparam>
		/// <param name="reader">DbDataReader</param>
		/// <param name="func">1行読み出し用の処理内容</param>
		/// <returns></returns>
		public static async Task<T> GetRow<T>(this DbDataReader reader, Func<DbDataReader, T> func)
		{
			if (await reader.ReadAsync())
			{
				return func(reader);
			}
			return default(T);
		}

		public static async Task<Dictionary<string, string>[]> ToStringRows(this Task<List<Dictionary<string, object>>> src)
		{
			return (await src)
				.Select(y => y.Keys.ToDictionary(z => z, z => $"{y[z]}"))
				.ToArray();
		}
	}
}