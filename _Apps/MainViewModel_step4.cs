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
				var colbases = new[] { "着率", "着順", "距離", "着差", "通過", "上り", "体重" };
				var columns = colbases.SelectMany(x => new[] { $"{x}平", $"{x}中", $"{x}偏" }).SelectMany(x => new[] { $"累{x}", $"直{x}" });

				if (S4Overwrite.IsChecked)
				{
					// 上書き時はﾃｰﾌﾞﾙを削除する。
					await conn.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS t_uma");
				}
				if (!await conn.ExistsColumn("t_uma", $"馬ID"))
				{
					// 存在しない場合に作成
					await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS t_uma (馬ID, " + columns.Select(x => $"{x} TEXT").GetString(",") + $", PRIMARY KEY (馬ID))");
				}

				var idarr = await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 馬ID FROM t_orig WHERE NOT EXISTS (SELECT 0 FROM t_uma WHERE t_orig.馬ID = t_uma.馬ID)");

				// ｺﾐｯﾄ量の調整=100件ずつｺﾐｯﾄ
				foreach (var ids in idarr.Chunk(100))
				{
					try
					{
						await conn.BeginTransaction();

						await S4CreateUmaInfo(conn, ids);

						conn.Commit();
					}
					catch (Exception ex)
					{
						MessageService.Exception(ex);

						AddLog($"Error: {ids.GetString(",")}");

						conn.Rollback();
					}
				}
			}

			MessageService.Info("Step4 Completed!!");
		});

		private async Task S4CreateUmaInfo(SQLiteControl conn, string[] ids)
		{
			foreach (var id in ids)
			{
				// HTMLﾃﾞｰﾀだと整形しづらいので一度ﾘｽﾄ化する
				var uma = new List<Dictionary<string, string>>();

				using (var parser = await AppUtil.GetDocument($"https://db.netkeiba.com/horse/result/{id}/"))
				{
					if (parser.GetElementsByClassName("db_h_race_results nk_tb_common").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
					{
						foreach (var row in table.Rows.Skip(1))
						{
							var tmp = new Dictionary<string, string>();

							tmp["頭数"] = row.Cells[6].GetInnerHtml();
							tmp["着順"] = row.Cells[11].GetInnerHtml();
							tmp["距離"] = Regex.Match(row.Cells[14].GetInnerHtml(), @"\d+").Value;
							tmp["着差"] = row.Cells[18].GetInnerHtml();
							tmp["通過"] = row.Cells[20].GetInnerHtml();
							tmp["上り"] = row.Cells[22].GetInnerHtml();
							tmp["体重"] = row.Cells[23].GetInnerHtml().Split('(')[0];
							tmp["着率"] = $"{double.Parse(tmp["着順"]) / double.Parse(tmp["頭数"])}";

							uma.Add(tmp);
						}
					}
				}

				// 平均、中央値、標準偏差を計算するための共通処理
				Func<string, Func<string, double>, Dictionary<string, string>> func = (head, conv) =>
				{
					var tmp = new Dictionary<string, string>();

					tmp[$"累{head}平"] = $"{uma.Select(x => conv(x[head])).Mean()}";
					tmp[$"累{head}中"] = $"{uma.Select(x => conv(x[head])).Median()}";
					tmp[$"累{head}偏"] = $"{uma.Select(x => conv(x[head])).PopulationStandardDeviation()}";

					tmp[$"直{head}平"] = $"{uma.Take(5).Select(x => conv(x[head])).Mean()}";
					tmp[$"直{head}中"] = $"{uma.Take(5).Select(x => conv(x[head])).Median()}";
					tmp[$"直{head}偏"] = $"{uma.Take(5).Select(x => conv(x[head])).PopulationStandardDeviation()}";

					return tmp;
				};

				var dic = new Dictionary<string, string>();

				dic["馬ID"] = id;

				dic.AddRange(func("着率", x => x.GetDouble()));
				dic.AddRange(func("着順", x => x.GetDouble()));
				dic.AddRange(func("距離", x => x.GetDouble()));
				dic.AddRange(func("着差", x => x.GetDouble()));
				dic.AddRange(func("通過", x => x.Split('-').Select(z => z.GetDouble()).Mean()));
				dic.AddRange(func("上り", x => x.GetDouble()));
				dic.AddRange(func("体重", x => x.GetDouble()));

				// 格納用SQL文作成
				var sql = $"INSERT INTO t_uma ({dic.Keys.GetString(",")}) VALUES ({dic.Keys.Select(x => "?").GetString(",")})";
				var prm = dic.Values.Select(x => SQLiteUtil.CreateParameter(System.Data.DbType.String, x));

				await conn.ExecuteNonQueryAsync(sql, prm.ToArray());

				AddLog($"Complete: 馬ID={id}");
			}
		}
	}
}