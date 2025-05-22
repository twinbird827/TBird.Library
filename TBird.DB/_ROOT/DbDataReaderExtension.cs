using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;

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
        /// SELECT文のｶﾗﾑﾘｽﾄを取得します。
        /// </summary>
        /// <param name="reader">DbDataReader</param>
        /// <returns></returns>
        private static Col[] GetColumns(this DbDataReader reader)
        {
            // 全列名を取得
            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(i => reader.GetName(i))
                .ToArray();
            // 重複を削除
            return columns
                .Select((c, i) => new Col(!columns.Take(i).Contains(c) ? c : null, i))
                .Where(x => x.Column != null)
                .ToArray();
        }

        /// <summary>
        /// SELECT文を実行し、ﾘｽﾄとして取得します。
        /// </summary>
        /// <typeparam name="T">1行の型</typeparam>
        /// <param name="conn">DbControl</param>
        /// <param name="func">1行読み出し用の処理内容</param>
        /// <param name="sql">SELECT文</param>
        /// <param name="parameters">SQL文の引数</param>
        /// <returns></returns>
        public static async Task<List<T>> GetRows<T>(this DbControl conn, Func<DbDataReader, T> func, string sql, params DbParameter[] parameters)
        {
            using (var reader = await conn.ExecuteReaderAsync(sql, parameters))
            {
                var ret = new List<T>();
                while (await reader.ReadAsync())
                {
                    ret.Add(func(reader));
                }
                return ret;
            }
        }

        /// <summary>
        /// SELECT文を実行し、KeyValuePairﾘｽﾄとして取得します。
        /// </summary>
        /// <param name="conn">DbControl</param>
        /// <param name="sql">SELECT文</param>
        /// <param name="parameters">SQL文の引数</param>
        /// <returns></returns>
        public static Task<List<Dictionary<string, object>>> GetRows(this DbControl conn, string sql, params DbParameter[] parameters)
        {
            Col[] columns = null;

            return conn.GetRows(r =>
            {
                return (columns ?? (columns = r.GetColumns()))
                    .ToDictionary(x => x.Column, x => r.Get<object>(x.Index));
            }, sql, parameters);
        }

        /// <summary>
        /// SELECT文を実行し、KeyValuePairﾘｽﾄとして取得します。
        /// </summary>
        /// <param name="conn">DbControl</param>
        /// <param name="sql">SELECT文</param>
        /// <param name="parameters">SQL文の引数</param>
        /// <returns></returns>
        public static Task<List<Dictionary<string, T>>> GetRows<T>(this DbControl conn, string sql, params DbParameter[] parameters)
        {
            Col[] columns = null;

            return conn.GetRows(r =>
            {
                return (columns ?? (columns = r.GetColumns()))
                    .ToDictionary(x => x.Column, x => r.Get<T>(x.Index));
            }, sql, parameters);
        }

        /// <summary>
        /// SELECT文を実行し、1行目のﾃﾞｰﾀを取得します。
        /// </summary>
        /// <typeparam name="T">1行の型</typeparam>
        /// <param name="conn">DbControl</param>
        /// <param name="func">1行読み出し用の処理内容</param>
        /// <param name="sql">SELECT文</param>
        /// <param name="parameters">SQL文の引数</param>
        /// <returns></returns>
        public static async Task<T> GetRow<T>(this DbControl conn, Func<DbDataReader, T> func, string sql, params DbParameter[] parameters)
        {
            using (var reader = await conn.ExecuteReaderAsync(sql, parameters))
            {
                if (await reader.ReadAsync())
                {
                    return func(reader);
                }
                return default;
            }
        }

        /// <summary>
        /// SELECT文を実行し、1行目のKeyValuePairを取得します。
        /// </summary>
        /// <param name="conn">DbControl</param>
        /// <param name="sql">SELECT文</param>
        /// <param name="parameters">SQL文の引数</param>
        /// <returns></returns>
        public static Task<Dictionary<string, object>> GetRow(this DbControl conn, string sql, params DbParameter[] parameters)
        {
            return GetRow<object>(conn, sql, parameters);
        }

        public static Task<Dictionary<string, T>> GetRow<T>(this DbControl conn, string sql, params DbParameter[] parameters)
        {
            Col[] columns = null;

            return conn.GetRow(r =>
            {
                return (columns ?? (columns = r.GetColumns()))
                    .ToDictionary(x => x.Column, x => r.Get<T>(x.Index));
            }, sql, parameters);
        }

        private static Dictionary<string, Col[]> _columns = new Dictionary<string, Col[]>();
    }

    internal class Col
    {
        public Col(string c, int i)
        {
            Column = c;
            Index = i;
        }

        public string Column { get; set; }

        public int Index { get; set; }
    }
}