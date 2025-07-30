﻿using System;
using System.Data.Common;
using System.Threading.Tasks;

namespace TBird.DB
{
	public interface IDbControl : IDisposable
	{
		DbConnection CreateConnection(string connectionString);

		/// <summary>
		/// ﾄﾗﾝｻﾞｸｼｮﾝを開始します。
		/// </summary>
		Task BeginTransaction();

		/// <summary>
		/// ﾄﾗﾝｻﾞｸｼｮﾝをｺﾐｯﾄします。
		/// </summary>
		void Commit();

		/// <summary>
		/// ﾄﾗﾝｻﾞｸｼｮﾝをﾛｰﾙﾊﾞｯｸします。
		/// </summary>
		void Rollback();

		/// <summary>
		/// 接続を閉じます。
		/// </summary>
		void Close();

		/// <summary>
		/// SQL文を実行し、影響を及ぼした件数を取得します。
		/// </summary>
		/// <param name="sql">SQL文</param>
		/// <param name="parameters">ﾊﾟﾗﾒｰﾀ</param>
		Task<int> ExecuteNonQueryAsync(string sql, params DbParameter[] parameters);

		/// <summary>
		/// SQL文を実行し、結果をobjectとして取得します。
		/// </summary>
		/// <param name="sql">SQL文</param>
		/// <param name="parameters">ﾊﾟﾗﾒｰﾀ</param>
		Task<object> ExecuteScalarAsync(string sql, params DbParameter[] parameters);

		/// <summary>
		/// SQL文を実行し、結果をDbDataReaderとして取得します。
		/// </summary>
		/// <param name="sql">SQL文</param>
		/// <param name="parameters">ﾊﾟﾗﾒｰﾀ</param>
		Task<DbDataReader> ExecuteReaderAsync(string sql, params DbParameter[] parameters);

	}
}