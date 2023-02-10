using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Text;
using TBird.Core;

namespace TBird.DB.SQLite
{
    public partial class SQLiteControl : DbControl
    {
        // https://www.sqlite.org/see/doc/release/www/sds-nuget.wiki

        public SQLiteControl(string connectionString) : base(connectionString)
        {

        }

        public override DbConnection CreateConnection(string connectionString)
        {
            var dic = GetConnectionDictionary(connectionString);
            var builder = new SQLiteConnectionStringBuilder()
            {
                DataSource = dic["datasource"],
                DefaultIsolationLevel = IsolationLevel.ReadCommitted,
                SyncMode = SynchronizationModes.Off,
                JournalMode = SQLiteJournalModeEnum.Wal,
                ReadOnly = bool.Parse(dic.Get("readonly", "false")),
                Pooling = bool.Parse(dic.Get("pooling", "false")),
                CacheSize = int.Parse(dic.Get("cachesize", "65536")),
                Password = dic.Get("password"),
            };
            return new SQLiteConnection(builder.ToString());
        }
    }
}
