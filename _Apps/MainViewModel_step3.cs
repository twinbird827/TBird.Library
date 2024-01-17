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
			Progress.Maximum = 4;

			using (var conn = new SQLiteControl(dstfile, string.Empty, false, false, 65536, false))
			{
				// 騎手情報の追加
				await CreateS3CommonTableEX(conn, S3KisyuOverwrite.IsChecked, "t_kisyu", "騎手");

				Progress.Value += 1;

				// 調教師情報の追加
				await CreateS3CommonTableEX(conn, S3TyokyoOverwrite.IsChecked, "t_tyokyo", "調教師");

				Progress.Value += 1;

				// 馬主情報の追加
				await CreateS3CommonTableEX(conn, S3BanushiOverwrite.IsChecked, "t_banushi", "馬主");

				Progress.Value += 1;

				// 馬情報の追加
				await CreateS3CommonTableEX(conn, S3BanushiOverwrite.IsChecked, "t_uma", "馬");

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

		private async Task CreateS3CommonTableEX(SQLiteControl conn, bool overwrite, string table, string head)
		{
			var id = $"{head}ID";
			var ﾗﾝｸ = new[] { "G1", "G2", "G3", "G4", "OP", "ｵｰﾌﾟﾝ", "3勝", "1600万下", "2勝", "1000万下", "1勝", "500万下", "未勝利", "新馬" };
			var ﾃﾞｰﾀ = new[] { "累勝", "累連", "累複", "直勝", "直連", "直複" };
			var allkeys = ﾃﾞｰﾀ.Select(x => $"{x}_全ﾗﾝｸ")
					.Concat(ﾃﾞｰﾀ.SelectMany(x => ﾗﾝｸ.Select(y => $"{x}_{y}")));

			if (overwrite)
			{
				// 上書き時はﾃｰﾌﾞﾙを削除する。
				await conn.ExecuteNonQueryAsync($"DROP TABLE IF EXISTS {table}");
			}
			if (!await conn.ExistsColumn(table, $"{id}"))
			{
				var cresql = $"" +
					$"CREATE TABLE IF NOT EXISTS {table} ( {id} TEXT, 開催日数 INTEGER, " +
					allkeys.Select(x => $"{x} TEXT").GetString(",") + "," +
					$"PRIMARY KEY ({id}, 開催日数))";

				// 存在しない場合に作成
				await conn.ExecuteNonQueryAsync(cresql);
			}

			var targets = await conn.GetRows($"SELECT DISTINCT {id}, 開催日数 FROM t_orig");

			var basesql =
				$" SELECT COUNT(1) 累全," +
				$"        COUNT(CASE WHEN 着順 = '1' THEN 1 END) 累1着," +
				$"        COUNT(CASE WHEN 着順 = '2' THEN 1 END) 累2着," +
				$"        COUNT(CASE WHEN 着順 = '3' THEN 1 END) 累3着," +
				$"        COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND TRUE       THEN 1 END) 直全," +
				$"        COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND 着順 = '1' THEN 1 END) 直1着," +
				$"        COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND 着順 = '2' THEN 1 END) 直2着," +
				$"        COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND 着順 = '3' THEN 1 END) 直3着" +
				$" FROM (SELECT ? X, 365 Y) X" +
				$" INNER JOIN t_orig ON t_orig.開催日数 < X.X AND t_orig.{id} = ? AND t_orig.ﾗﾝｸ = ?";

			await conn.BeginTransaction();
			foreach (var row in targets)
			{
				var dic = new Dictionary<string, object>();

				dic[id] = row[id];
				dic["開催日数"] = row["開催日数"];

				var 累0 = 0d;
				var 累1 = 0d;
				var 累2 = 0d;
				var 累3 = 0d;
				var 直0 = 0d;
				var 直1 = 0d;
				var 直2 = 0d;
				var 直3 = 0d;

				foreach (var r in ﾗﾝｸ)
				{
					var rrow = await conn.GetRows(basesql, new[]
					{
						SQLiteUtil.CreateParameter(System.Data.DbType.Int64, row["開催日数"]),
						SQLiteUtil.CreateParameter(System.Data.DbType.String, row[id]),
						SQLiteUtil.CreateParameter(System.Data.DbType.String, r),
					}).ContinueWith(x => x.Result.First());

					var _累0 = rrow["累全"].GetDouble(); 累0 += _累0;
					var _累1 = rrow["累1着"].GetDouble(); 累1 += _累1;
					var _累2 = rrow["累2着"].GetDouble(); 累2 += _累2;
					var _累3 = rrow["累3着"].GetDouble(); 累3 += _累3;
					var _直0 = rrow["直全"].GetDouble(); 直0 += 直0;
					var _直1 = rrow["直1着"].GetDouble(); 直1 += 直1;
					var _直2 = rrow["直2着"].GetDouble(); 直2 += 直2;
					var _直3 = rrow["直3着"].GetDouble(); 直3 += 直3;

					dic[$"累勝_{r}"] = $"{(_累1) / _累0}";
					dic[$"累連_{r}"] = $"{(_累1 + _累2) / _累0}";
					dic[$"累複_{r}"] = $"{(_累1 + _累2 + _累3) / _累0}";

					dic[$"直勝_{r}"] = $"{(_直1) / _直0}";
					dic[$"直連_{r}"] = $"{(_直1 + _直2) / _直0}";
					dic[$"直複_{r}"] = $"{(_直1 + _直2 + _直3) / _直0}";
				}

				dic[$"累勝_全ﾗﾝｸ"] = $"{(累1) / 累0}";
				dic[$"累連_全ﾗﾝｸ"] = $"{(累1 + 累2) / 累0}";
				dic[$"累複_全ﾗﾝｸ"] = $"{(累1 + 累2 + 累3) / 累0}";

				dic[$"直勝_全ﾗﾝｸ"] = $"{(直1) / 直0}";
				dic[$"直連_全ﾗﾝｸ"] = $"{(直1 + 直2) / 直0}";
				dic[$"直複_全ﾗﾝｸ"] = $"{(直1 + 直2 + 直3) / 直0}";

				var inssql = $"" +
					$"INSERT INTO {table} ( {id}, 開催日数, {allkeys.GetString(",")} ) VALUES ( ?, ?, {allkeys.Select(x => "?").GetString(",")} )";
				var prm = new[]
				{
					SQLiteUtil.CreateParameter(System.Data.DbType.String, row[id]),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, row["開催日数"]),
				}.Concat(
					allkeys.Select(x => SQLiteUtil.CreateParameter(System.Data.DbType.String, dic[x]))
				);

				// ﾃﾞｰﾀ作成
				await conn.ExecuteNonQueryAsync(inssql, prm.ToArray());
			}
			conn.Commit();

			/*
SELECT COUNT(1) 累全,
       COUNT(CASE WHEN 着順 = '1' THEN 1 END) 累1着,
       COUNT(CASE WHEN 着順 = '2' THEN 1 END) 累2着,
       COUNT(CASE WHEN 着順 = '3' THEN 1 END) 累3着,
       COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND TRUE       THEN 1 END) 直全,
       COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND 着順 = '1' THEN 1 END) 直1着,
       COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND 着順 = '2' THEN 1 END) 直2着,
       COUNT(CASE WHEN (X.X - X.Y) < 開催日数 AND 着順 = '3' THEN 1 END) 直3着
FROM (SELECT 12414 X, 60 Y) X
INNER JOIN t_orig ON 調教師ID = '01141' AND t_orig.開催日数 < X.X
			 */
		}
	}
}