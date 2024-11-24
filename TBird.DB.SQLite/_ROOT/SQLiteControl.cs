using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
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

		protected override string CreateLock(string connectionString)
		{
			var fn = this.GetType().FullName;
			var ds = ToConnectionDictionary(connectionString)["datasource"];
			return $"{fn}+{ds}";
		}

		private async Task OpenAsync(bool executerecovery)
		{
			await base.OpenAsync();

			if (!executerecovery) return;

			if (!_m._init) return;

			var result = "ok"; // await Task.Run(() => this.ExecuteScalarAsync<string>("PRAGMA integrity_check"));
			var isok = result.ToLower() == "ok";

			if (!isok)
			{
				// indexが壊れていないか確認
				var mindex = Regex.Match(result, @"row [0-9]+ missing from index (?<s>[\w]+)");
				if (mindex.Success)
				{
					// indexが壊れていたら修復して再帰
					await ExecuteNonQueryAsync($"REINDEX {mindex.Groups["s"]}");
					await OpenAsync(true);
				}
				else
				{
					// 何らかのｴﾗｰ時はﾀﾞﾝﾌﾟしてﾃﾞｰﾀﾍﾞｰｽを再作成する。
					var exe = Directories.GetAbsolutePath("sqlite3.exe");

					var dic = ToConnectionDictionary(_cs);
					var password = dic["password"];
					var src = dic["datasource"];
					var bak = $"{src}.bak";
					var dst = $"{src}.tmp";

					// ｿｰｽﾌｧｲﾙをﾊﾞｯｸｱｯﾌﾟ
					await FileUtil.CopyAsync(src, bak);

					if (!string.IsNullOrEmpty(password))
					{
						// ﾀﾞﾝﾌﾟするためにﾊﾟｽﾜｰﾄﾞを解除する。
						await ExecuteNonQueryAsync($"PRAGMA key = '{password}'");
						await ExecuteNonQueryAsync($"PRAGMA key = ''");
					}
					Close();

					// 一次的なﾃﾞｰﾀﾍﾞｰｽﾌｧｲﾙをﾊﾟｽﾜｰﾄﾞなしで作成
					dic["datasource"] = dst;
					dic["password"] = string.Empty;
					var dcs = dic.Select(x => $"{x.Key}={x.Value}").GetString(";");

					using (var control = new SQLiteControl(dcs))
					{
						await control.OpenAsync();
					}

					// ﾀﾞﾝﾌﾟ実行
					CoreUtil.Execute(new[]
					{
						new ProcessStartInfo(exe, $"\"{src}\" .dump") { StandardOutputEncoding = Encoding.UTF8 },
						new ProcessStartInfo(exe, $"\"{dst}\""),
					});

					// ﾊﾟｽﾜｰﾄﾞ再設定
					if (!string.IsNullOrEmpty(password))
					{
						using (var control = new SQLiteControl(dcs))
						{
							await control.ExecuteNonQueryAsync($"PRAGMA rekey = '{password}'");
						}
					}

					// ｵﾘｼﾞﾅﾙﾃﾞｰﾀﾍﾞｰｽに差し替えて再帰
					FileUtil.Move(dst, src);
					await OpenAsync(true);
				}
			}

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

		protected override Task OpenAsync()
		{
			if (_openinit)
			{
				_openinit = false;
				return OpenAsync(true);
			}
			else
			{
				return OpenAsync(false);
			}
		}

		private bool _openinit = true;

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