using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.DB;
using static System.Net.WebRequestMethods;
using AngleSharp.Html.Dom;
using AngleSharp.Browser;
using TBird.Core;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public CheckboxItemModel S3KisyuOverwrite { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3TyokyoOverwrite { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3BanushiOverwrite { get; } = new CheckboxItemModel("", "") { IsChecked = true };

		public IRelayCommand S3EXEC => RelayCommand.Create(async _ =>
		{
			var dstfile = Path.Combine(step2dir, "database.sqlite3");

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = 3;

			using (var conn = new SQLiteControl(dstfile, string.Empty, false, false, 65536, false))
			{
				// 騎手情報の追加
				await CreateS3CommonTable(conn, S3KisyuOverwrite.IsChecked, "t_kisyu", "騎手", "https://db.netkeiba.com/jockey/result/{0}/");

				Progress.Value += 1;

				// 調教師情報の追加
				await CreateS3CommonTable(conn, S3KisyuOverwrite.IsChecked, "t_kisyu", "調教師", "https://db.netkeiba.com/trainer/result/{0}/");

				Progress.Value += 1;

				// 馬主情報の追加
				await CreateS3CommonTable(conn, S3KisyuOverwrite.IsChecked, "t_kisyu", "馬主", "https://db.netkeiba.com/owner/result/{0}/");

				Progress.Value += 1;
			}

			MessageService.Info("Step3 Completed!!");
		});

		private double GetHrefDouble(IHtmlTableRowElement row, int index) => double.Parse(row.Cells[index].GetElementsByTagName("a").First().GetInnerHtml().Replace(",", ""));

		private async Task<Dictionary<string, string>> GetS3CommonDetail(string url)
		{
			var dic = new Dictionary<string, string>();

			using (var parser = await AppUtil.GetDocument(url))
			{
				if (parser.GetElementsByClassName("nk_tb_common race_table_01").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
				{
					var rrow = table.Rows[2];
					var r1 = GetHrefDouble(rrow, 2);
					var r2 = GetHrefDouble(rrow, 3);
					var r3 = GetHrefDouble(rrow, 4);
					var rz = GetHrefDouble(rrow, 5);

					dic["累勝利"] = $"{r1 / (r1 + r2 + r3 + rz)}";
					dic["累連対"] = $"{(r1 + r2) / (r1 + r2 + r3 + rz)}";
					dic["累複勝"] = $"{(r1 + r2 + r3) / (r1 + r2 + r3 + rz)}";

					var trows = table.Rows.Skip(3).Take(3);
					var t1 = trows.Select(x => GetHrefDouble(x, 2)).Sum();
					var t2 = trows.Select(x => GetHrefDouble(x, 3)).Sum();
					var t3 = trows.Select(x => GetHrefDouble(x, 4)).Sum();
					var tz = trows.Select(x => GetHrefDouble(x, 5)).Sum();

					dic["直勝利"] = $"{(t1) / (t1 + t2 + t3 + tz)}";
					dic["直連対"] = $"{(t1 + t2) / (t1 + t2 + t3 + tz)}";
					dic["直複勝"] = $"{(t1 + t2 + t3) / (t1 + t2 + t3 + tz)}";
				}
				else
				{
					dic["累勝利"] = "0";
					dic["累連対"] = "0";
					dic["累複勝"] = "0";
					dic["直勝利"] = "0";
					dic["直連対"] = "0";
					dic["直複勝"] = "0";
				}
			}

			return dic;
		}

		private async Task CreateS3CommonTable(SQLiteControl conn, bool overwrite, string table, string head, string url)
		{
			// 列ﾍﾞｰｽ
			var columns = new[] { "ID", "累勝利", "累連対", "累複勝", "直勝利", "直連対", "直複勝" };

			if (overwrite)
			{
				// 上書き時はﾃｰﾌﾞﾙを削除する。
				await conn.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {table}");
			}
			if (!await conn.ExistsColumn(table, $"{head}ID"))
			{
				// 存在しない場合に作成
				await conn.ExecuteNonQueryAsync($"CREATE TABLE IF NOT EXISTS {table} (" + columns.Select(x => $"{head}{x} TEXT").GetString(",") + $", PRIMARY KEY ({head}ID))");
			}

			try
			{
				await conn.BeginTransaction();

				using (var reader = await conn.ExecuteReaderAsync($"SELECT DISTINCT {head}ID FROM t_orig WHERE NOT EXISTS (SELECT 0 FROM {table} WHERE t_orig.{head}ID = {table}.{head}ID)"))
				{
					foreach (var id in await reader.GetRows(x => x.Get<string>(0)))
					{
						var dic = await GetS3CommonDetail(string.Format(url, id)); dic["ID"] = id;
						var sql = $"INSERT INTO {table} ({columns.Select(x => head + x).GetString(",")}) VALUES ({columns.Select(x => "?").GetString(",")})";
						var prm = columns.Select(x => SQLiteUtil.CreateParameter(System.Data.DbType.String, dic[x]));

						await conn.ExecuteNonQueryAsync(sql, prm.ToArray());
					}
				}

				conn.Commit();
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				conn.Rollback();
			}
		}

	}
}