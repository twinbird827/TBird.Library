using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.DB
{
    public abstract partial class DbControl : IDbControl, ILocker
    {
        protected DbControl(string connectionString)
        {
            _conn = CreateConnection(connectionString);
            Lock = CreateLock(connectionString);
        }

        private DbConnection _conn;

        private DbTransaction _tran;

        private TimeSpan _timeout => TimeSpan.FromMilliseconds(_conn.ConnectionTimeout);

        private Stopwatch _stopwatch = new Stopwatch();

        public string Lock { get; private set; }

        public abstract DbConnection CreateConnection(string connectionString);

        public async Task BeginTransaction()
        {
            using (await Locker.LockAsync(Lock))
            {
                await OpenAsync();
                await WaitTransaction().Timeout(_timeout, null);
                _tran = _conn.BeginTransaction();
            }
        }

        public void Commit()
        {
            if (_tran != null)
            {
                _tran.Commit();
                _tran = null;
            }
        }

        public void Rollback()
        {
            if (_tran != null)
            {
                _tran.Commit();
                _tran = null;
            }
        }

        public virtual void Close()
        {
            if (_conn != null)
            {
                _conn.Close();
                _conn = null;
            }
        }

        public Task<int> ExecuteNonQueryAsync(string sql, params DbParameter[] parameters)
        {
            return ExecuteAsync(cmnd => cmnd.ExecuteNonQueryAsync(), sql, parameters);
        }

        public Task<DbDataReader> ExecuteReaderAsync(string sql, params DbParameter[] parameters)
        {
            return ExecuteAsync(cmnd => cmnd.ExecuteReaderAsync(), sql, parameters);
        }

        public Task<object> ExecuteScalarAsync(string sql, params DbParameter[] parameters)
        {
            return ExecuteAsync(cmnd => cmnd.ExecuteScalarAsync(), sql, parameters);
        }

        private async Task<T> ExecuteAsync<T>(Func<DbCommand, Task<T>> execute, string sql, DbParameter[] parameters)
        {
            await OpenAsync();

            using (await Locker.LockAsync(Lock))
            {
                _stopwatch.Restart();

                var exception = false;
                try
                {
                    using (var cmnd = _conn.CreateCommand())
                    {
                        cmnd.CommandText = sql;
                        cmnd.Parameters.Clear();
                        cmnd.Parameters.AddRange(parameters);
                        return await execute(cmnd);
                    }
                }
                catch
                {
                    exception = true;
                    throw;
                }
                finally
                {
                    if (exception || 1000 < _stopwatch.ElapsedMilliseconds)
                    {
                        // ｴﾗｰが発生したか、処理が1秒超えたらｺﾝｿｰﾙに表示
                        var sb = new StringBuilder();
                        sb.AppendLine($"{_stopwatch.Elapsed} ******************************************************************");
                        sb.AppendLine(sql.ToString());
                        sb.AppendLine(parameters.Select(p => p.Value?.ToString()).GetString(","));
                        MessageService.Debug(sb.ToString());
                    }
                    _stopwatch.Stop();
                }
            }
        }

        private async Task WaitTransaction()
        {
            while (_tran != null) await Task.Delay(16);
        }

        protected virtual async Task OpenAsync()
        {
            if (_conn.State != ConnectionState.Open)
            {
                await _conn.OpenAsync();
            }
        }

        protected Dictionary<string, string> ToConnectionDictionary(string connectionString)
        {
            return connectionString.Split(';')
                .Where(x => x.Contains('='))
                .Select(x => x.Split('='))
                .ToDictionary(
                    x => x[0].ToLower(),
                    x => x[1]
            );
        }

        protected virtual string CreateLock(string connectionString)
        {
            return Locker.GetNewLockKey();
        }
    }
}