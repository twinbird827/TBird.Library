using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Diagnostics;
using System.Linq;
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

		private async Task OpenAsync(bool executerecovery)
		{
			await base.OpenAsync().ConfigureAwait(false);

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
					await ExecuteNonQueryAsync($"REINDEX {mindex.Groups["s"]}").ConfigureAwait(false);
					await OpenAsync(true).ConfigureAwait(false);
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

					// ｿｰｽﾌｧｲﾙをﾊﾞｯｸｱｯﾌﾟ（bak は試行のたびに上書きされるため恒久的な退避先ではない, issue #217）
					await FileUtil.CopyAsync(src, bak).ConfigureAwait(false);
					// Manager は WAL 固定のため、本体だけでは未ﾁｪｯｸﾎﾟｲﾝﾄの内容が bak から欠ける。
					// 前回試行の -wal / -shm が新しい bak と組になると壊れるため先に消す。
					FileUtil.Delete(bak + "-wal");
					FileUtil.Delete(bak + "-shm");
					if (await FileUtil.Exists(src + "-wal").ConfigureAwait(false))
					{
						await FileUtil.CopyAsync(src + "-wal", bak + "-wal").ConfigureAwait(false);
					}

					if (!string.IsNullOrEmpty(password))
					{
						// ﾀﾞﾝﾌﾟするためにﾊﾟｽﾜｰﾄﾞを解除する。
						await ExecuteNonQueryAsync($"PRAGMA key = '{password}'").ConfigureAwait(false);
						await ExecuteNonQueryAsync($"PRAGMA key = ''").ConfigureAwait(false);
					}
					Close();

					// 一次的なﾃﾞｰﾀﾍﾞｰｽﾌｧｲﾙをﾊﾟｽﾜｰﾄﾞなしで作成
					dic["datasource"] = dst;
					dic["password"] = string.Empty;
					var dcs = dic.Select(x => $"{x.Key}={x.Value}").GetString(";");

					// 前回中断の .tmp を再利用するとﾀﾞﾝﾌﾟ再適用が CREATE TABLE 衝突を起こすため消す。
					// Manager は WAL 固定のため、本体だけ消すと stale WAL がﾃｰﾌﾞﾙを復活させる。
					FileUtil.Delete(dst);
					FileUtil.Delete(dst + "-wal");
					FileUtil.Delete(dst + "-shm");

					try
					{
						using (var control = new SQLiteControl(dcs))
						{
							await control.OpenAsync().ConfigureAwait(false);
						}

						// ｶﾞｰﾄﾞが弾けるのは復元結果が空のときだけ。sqlite_schema 側の破損で一部ﾃｰﾌﾞﾙが欠けたまま
						// COMMIT まで完走する系は通過する（3.51.1 未満では .dump 自体の exit code も常に 0）。
						// ﾀﾞﾝﾌﾟ実行
						CoreUtil.Execute(new[]
						{
							new ProcessStartInfo(exe, $"\"{src}\" .dump"),
							new ProcessStartInfo(exe, $"\"{dst}\""),
						});

						using (var control = new SQLiteControl(dcs))
						{
							// 中断したﾀﾞﾝﾌﾟは ROLLBACK; -- due to errors で終わり、2 段目はそれを正常実行して exit 0 になるため、
							// exit code 検査では空ﾀﾞﾝﾌﾟを弾けない。
							if (await control.ExecuteScalarAsync<long>("SELECT count(*) FROM sqlite_master").ConfigureAwait(false) == 0)
							{
								throw new InvalidOperationException($"{src} のﾀﾞﾝﾌﾟ結果が空のため、差し替えを中止しました（ﾊﾞｯｸｱｯﾌﾟ: {bak}）。");
							}

							// ﾊﾟｽﾜｰﾄﾞ再設定
							if (!string.IsNullOrEmpty(password))
							{
								await control.ExecuteNonQueryAsync($"PRAGMA rekey = '{password}'").ConfigureAwait(false);
							}
						}
					}
					catch
					{
						// ここを抜けるのは Move 未到達のときなので、src が現存し dst を残す理由が無い。
						// dst は PRAGMA rekey までﾊﾟｽﾜｰﾄﾞ無し（平文）のため、暗号化構成でも残すと平文の複製が滞在する。
						try
						{
							FileUtil.Delete(dst);
							FileUtil.Delete(dst + "-wal");
							FileUtil.Delete(dst + "-shm");
						}
						catch (Exception ex)
						{
							MessageService.Warn($"{dst} の後始末に失敗しました（{ex.Message}）。復元途中のﾌｧｲﾙが残っている可能性があります。");
						}
						throw;
					}

					// ここから先は dst が完成品なので掃除しない。Move は本体 1 ﾌｧｲﾙしか動かさず、
					// src の stale WAL が復元結果に適用されうるため、破棄する src のｻｲﾄﾞｶｰごと捨てる。
					FileUtil.Delete(src + "-wal");
					FileUtil.Delete(src + "-shm");

					// ｵﾘｼﾞﾅﾙﾃﾞｰﾀﾍﾞｰｽに差し替えて再帰
					FileUtil.Move(dst, src);
					await OpenAsync(true).ConfigureAwait(false);
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