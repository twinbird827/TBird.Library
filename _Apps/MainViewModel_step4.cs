using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Core;
using System.Windows.Documents;
using System.Text.RegularExpressions;
using MathNet.Numerics.Statistics;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public CheckboxItemModel S4Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = true };

		public IRelayCommand S4EXEC => RelayCommand.Create(async _ =>
		{
			var dstfile = Path.Combine(step2dir, "database.sqlite3");

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = 3;

			using (var conn = new SQLiteControl(dstfile, string.Empty, false, false, 65536, false))
			{
				var colbases = new[] { "距離", "着差", "通過", "上り", "体重" };
				var columns = colbases.SelectMany(x => new[] { $"{x}平", $"{x}中", $"{x}偏" }).SelectMany(x => new[] { $"累{x}", $"直{x}" });

				if (S4Overwrite.IsChecked)
				{
					// 上書き時はﾃｰﾌﾞﾙを削除する。
					await conn.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS t_uma_ex");
				}
				if (!await conn.ExistsColumn("t_uma_ex", $"馬ID"))
				{
					// 存在しない場合に作成
					await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS t_uma_ex (馬ID TEXT, 開催日数 INTEGER," + columns.Select(x => $"{x} TEXT").GetString(",") + $", PRIMARY KEY (馬ID, 開催日数))");
				}

				var idarr = await conn.GetRows("SELECT DISTINCT 馬ID, 開催日数 FROM t_orig WHERE NOT EXISTS (SELECT 0 FROM t_uma_ex WHERE t_orig.馬ID = t_uma_ex.馬ID AND t_orig.開催日数 = t_uma_ex.開催日数)");

				// ｺﾐｯﾄ量の調整=100件ずつｺﾐｯﾄ
				foreach (var targets in idarr.Chunk(100))
				{
					try
					{
						await conn.BeginTransaction();

						await S4CreateUmaInfo(conn, targets);

						conn.Commit();
					}
					catch (Exception ex)
					{
						MessageService.Exception(ex);

						AddLog($"Error: {targets.GetString(",")}");

						conn.Rollback();
					}
				}
			}

			MessageService.Info("Step4 Completed!!");
		});

		private async Task S4CreateUmaInfo(SQLiteControl conn, Dictionary<string, object>[] targets)
		{
			foreach (var target in targets)
			{
				var uma = await conn.GetRows(
					" SELECT 距離, 着差, 通過, 上り, 体重, (CASE WHEN X.X - X.Y < t_orig.開催日数 THEN TRUE ELSE FALSE END) 年内" +
					" FROM (SELECT ? X, 365 Y) X" +
					" INNER JOIN t_orig ON t_orig.開催日数 < X.X AND t_orig.馬ID = ?",
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, target["開催日数"]),
					SQLiteUtil.CreateParameter(System.Data.DbType.String, target["馬ID"])
				);
				// 平均、中央値、標準偏差を計算するための共通処理
				Func<string, Func<string, double>, Dictionary<string, string>> func = (head, conv) =>
				{
					var tmp = new Dictionary<string, string>();

					tmp[$"累{head}平"] = $"{uma.Select(x => conv($"{x[head]}")).Mean()}";
					tmp[$"累{head}中"] = $"{uma.Select(x => conv($"{x[head]}")).Median()}";
					tmp[$"累{head}偏"] = $"{uma.Select(x => conv($"{x[head]}")).PopulationStandardDeviation()}";

					tmp[$"直{head}平"] = $"{uma.Take(5).Select(x => conv($"{x[head]}")).Mean()}";
					tmp[$"直{head}中"] = $"{uma.Take(5).Select(x => conv($"{x[head]}")).Median()}";
					tmp[$"直{head}偏"] = $"{uma.Take(5).Select(x => conv($"{x[head]}")).PopulationStandardDeviation()}";

					return tmp;
				};

				var dic = new Dictionary<string, string>();

				dic["馬ID"] = $"{target["馬ID"]}";
				dic["開催日数"] = $"{target["開催日数"]}";

				dic.AddRange(func("距離", x => x.GetDouble()));
				dic.AddRange(func("着差", x => x.Replace("ハナ", "0.05").Replace("アタマ", "0.10").Replace("クビ", "0.15").Replace("同着", "0").Replace("大", "15").Replace("1/4", "25").Replace("1/2", "50").Replace("3/4", "75").Replace("", "-0.5").Split('+').Sum(x => double.Parse(x))));
				dic.AddRange(func("通過", x => x.Split('-').Select(z => z.GetDouble()).Mean()));
				dic.AddRange(func("上り", x => x.GetDouble()));
				dic.AddRange(func("体重", x => x.GetDouble()));

				// 格納用SQL文作成
				var sql = $"INSERT INTO t_uma_ex ({dic.Keys.GetString(",")}) VALUES ({dic.Keys.Select(x => "?").GetString(",")})";
				var prm = dic.Values.Select(x => SQLiteUtil.CreateParameter(System.Data.DbType.String, x));

				await conn.ExecuteNonQueryAsync(sql, prm.ToArray());

				AddLog($"Complete: 馬ID={dic["馬ID"]}; 開催日数={dic["開催日数"]}");
			}
		}
	}
}