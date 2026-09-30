using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.DB.SQLite
{
	public partial class SQLiteControl : DbControl
	{
		// https://www.sqlite.org/see/doc/release/www/sds-nuget.wiki

		public SQLiteControl(string datasource, string password, bool @readonly, bool pooling, int cachesize, bool extension) : this($"datasource={datasource};password={password};readonly={@readonly};pooling={pooling};cachesize={cachesize};extension={extension}")
		{

		}

		public SQLiteControl(string connectionString) : base(connectionString)
		{

		}

		public override DbConnection CreateConnection(string connectionString)
		{
			lock (_lock)
			{
				var fn = this.GetType().FullName;
				var ds = ToConnectionDictionary(connectionString)["datasource"];
				_lockstring = $"{fn}+{ds}";

				_cs = connectionString;

				if (_manages.ContainsKey(connectionString))
				{
					_m = _manages[connectionString];
					_m._indx++;
				}
				else
				{
					_m = _manages[connectionString] = new Manager(ToConnectionDictionary(connectionString));
					_m._indx++;
				}
				return _m._conn;
			}
		}

		internal string _cs;
		internal Manager _m;
		private static object _lock = new object();
		private static Dictionary<string, Manager> _manages = new Dictionary<string, Manager>();
		private string _lockstring;

		protected override string GetLockString()
		{
			return _lockstring ?? base.GetLockString();
		}

		protected override async Task OpenAsync()
		{
			await base.OpenAsync().ConfigureAwait(false);

			if (!_m._init) return;

			_m._init = false;

			if (_m._extension)
			{
				// 拡張ﾗｲﾌﾞﾗﾘ(32bit, 64bit)
				var extensionpath = Environment.Is64BitProcess
					? @"extension-functions-64"
					: @"extension-functions-32";
				// 拡張ﾗｲﾌﾞﾗﾘ読込
				_m._conn.EnableExtensions(true);
				_m._conn.LoadExtension(extensionpath);
			}
		}

		public override void Close()
		{
			if (--_m._indx == 0)
			{
				base.Close();
			}
		}

		internal class Manager
		{
			public Manager(Dictionary<string, string> dic)
			{
				var builder = string.IsNullOrEmpty(dic.Get("password"))
					? new SQLiteConnectionStringBuilder()
					{
						DataSource = dic["datasource"],
						DefaultIsolationLevel = IsolationLevel.ReadCommitted,
						SyncMode = SynchronizationModes.Off,
						JournalMode = SQLiteJournalModeEnum.Wal,
						ReadOnly = bool.Parse(dic.Get("readonly", "false")),
						Pooling = bool.Parse(dic.Get("pooling", "false")),
						CacheSize = int.Parse(dic.Get("cachesize", "65536")),
					} : new SQLiteConnectionStringBuilder()
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

				_conn = new SQLiteConnection(builder.ToString());
				_indx = 0;
				_init = true;
				_extension = bool.Parse(dic.Get("extension", "false"));
			}

			public SQLiteConnection _conn;

			public int _indx;

			public bool _init;

			public bool _extension;
		}
	}
}