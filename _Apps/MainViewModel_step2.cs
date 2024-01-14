using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;
using TBird.Wpf;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		private const string step2dir = @"step2";

		public CheckboxItemModel S2Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = true };

		private string[] GetHeaders(string header) => header.Split('\t').Select(x => Regex.Replace(x, @"\((?<str>.+)\)", x => "_" + x.Groups["str"].Value)).ToArray();

		public IRelayCommand S2EXEC => RelayCommand.Create(async _ =>
		{
			var srcdir = step1dir;
			var dstfile = Path.Combine(step2dir, "database.sqlite3");

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = Directory.GetFiles(srcdir).Count() + 1;

			using (var conn = new SQLiteControl(dstfile, string.Empty, false, false, 65536, false))
			{
				if (S2Overwrite.IsChecked || !File.Exists(dstfile))
				{
					try
					{
						if (!await CreateOrig(conn, dstfile, srcdir)) return;
					}
					catch (Exception ex)
					{
						MessageService.Error(ex.ToString());
					}
				}
				Progress.Value += 1;

				foreach (var srcfile in Directory.GetFiles(srcdir))
				{
					try
					{
						// ﾄﾗﾝｻﾞｸｼｮﾝ開始
						await conn.BeginTransaction();

						// ﾃﾞｰﾀ挿入
						if (await InsertOrig(conn, srcfile))
						{
							conn.Commit();
						}
						else
						{
							conn.Rollback();
						}
					}
					catch (Exception ex)
					{
						MessageService.Exception(ex);
						AddLog($"例外発生: {srcfile}");
						conn.Rollback();
					}
					Progress.Value += 1;
				}
			}

			MessageService.Info("STEP2 Completed!!");
		});

		private async Task<bool> CreateOrig(SQLiteControl conn, string dstfile, string srcdir)
		{
			// 既存ﾌｧｲﾙ削除
			FileUtil.BeforeCreate(dstfile);

			// CSVﾌｧｲﾙを一個だけ取得
			var csvfile = Directory.GetFiles(srcdir).First();
			// 先頭行を取得
			var csvenum = File.ReadLinesAsync(csvfile).GetAsyncEnumerator();
			var csvheader = await csvenum.MoveNextAsync() ? csvenum.Current : string.Empty;
			if (string.IsNullOrEmpty(csvheader))
			{
				AddLog("ﾌｧｲﾙﾍｯﾀﾞ不備");
				return false;
			}
			var headers = GetHeaders(csvheader);

			// ﾃｰﾌﾞﾙ作成
			await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_orig (" + headers.Select(x => $"{x} TEXT").GetString(",") + ", PRIMARY KEY (ﾚｰｽID,馬番))");

			// ｲﾝﾃﾞｯｸｽ作成
			foreach (var index in new[] { "馬ID", "騎手ID", "調教師ID", "馬主ID" }.Select((x, i) => $"CREATE INDEX IF NOT EXISTS t_orig_index{i} ON t_orig ({x})"))
			{
				await conn.ExecuteNonQueryAsync(index);
			}
			return true;
		}

		private async Task<bool> InsertOrig(SQLiteControl conn, string srcfile)
		{
			var rows = File.ReadAllLines(srcfile);
			if (rows.Length == 0)
			{
				AddLog($"空ﾌｧｲﾙ: {srcfile}");
				return false;
			}

			// ﾍｯﾀﾞ作成
			var headers = GetHeaders(rows[0]);

			foreach (var row in rows.Skip(1))
			{
				var sql = "INSERT INTO t_orig (" + headers.GetString(",") + ") VALUES (" + headers.Select(x => "?").GetString(",") + ")";
				var prm = row.Split('\t').Select(x => SQLiteUtil.CreateParameter(DbType.String, x)).ToArray();
				await conn.ExecuteNonQueryAsync(sql, prm);
			}

			return true;
		}
	}
}