using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Core;
using TBird.DB;
using System.Data.Common;
using System.Reflection.Metadata.Ecma335;
using AngleSharp.Text;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		private const string step5dir = @"step5";

		public CheckboxItemModel S5Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = true };

		public IRelayCommand S5EXEC => RelayCommand.Create(async _ =>
		{
			var srcfile = Path.Combine(step2dir, "database.sqlite3");
			var dstfile = Path.Combine(step5dir, DateTime.Now.ToString("yyMMdd-HHmmss") + ".csv");

			using (var conn = new SQLiteControl(srcfile, string.Empty, false, false, 65536, false))
			{
				var 全ﾚｰｽ = await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT ﾚｰｽID FROM t_orig");

				/*
	SELECT
	t_orig.*,
	t_banushi.*,
	t_kisyu.*,
	t_tyokyo.*,
	t_uma.*,
	(SELECT AVG(斤量) FROM t_orig v_kin WHERE t_orig.ﾚｰｽID = v_kin.ﾚｰｽID) 斤量平均,
	(SELECT MAX(開催日) FROM t_orig v_date WHERE t_orig.馬ID = v_date.馬ID AND t_orig.ﾚｰｽID > v_date.ﾚｰｽID) 前回開催日
	FROM t_orig
	LEFT OUTER JOIN t_banushi ON t_orig.馬主ID   = t_banushi.馬主ID
	LEFT OUTER JOIN t_kisyu   ON t_orig.騎手ID   = t_kisyu.騎手ID
	LEFT OUTER JOIN t_tyokyo  ON t_orig.調教師ID = t_tyokyo.調教師ID
	LEFT OUTER JOIN t_uma     ON t_orig.馬ID     = t_uma.馬ID
	WHERE t_orig.ﾚｰｽID = ?
				 * */
				var sql = new StringBuilder();

				sql.AppendLine($"SELECT");
				sql.AppendLine($"t_orig.*,");
				sql.AppendLine($"t_banushi.*,");
				sql.AppendLine($"t_kisyu.*,");
				sql.AppendLine($"t_tyokyo.*,");
				sql.AppendLine($"t_uma.*,");
				sql.AppendLine($"(SELECT AVG(斤量) FROM t_orig v_kin WHERE t_orig.ﾚｰｽID = v_kin.ﾚｰｽID) 斤量平均,");
				sql.AppendLine($"(SELECT MAX(開催日) FROM t_orig v_date WHERE t_orig.馬ID = v_date.馬ID AND t_orig.ﾚｰｽID > v_date.ﾚｰｽID) 前回開催日");
				sql.AppendLine($"FROM t_orig");
				sql.AppendLine($"LEFT OUTER JOIN t_banushi ON t_orig.馬主ID   = t_banushi.馬主ID");
				sql.AppendLine($"LEFT OUTER JOIN t_kisyu   ON t_orig.騎手ID   = t_kisyu.騎手ID");
				sql.AppendLine($"LEFT OUTER JOIN t_tyokyo  ON t_orig.調教師ID = t_tyokyo.調教師ID");
				sql.AppendLine($"LEFT OUTER JOIN t_uma     ON t_orig.馬ID     = t_uma.馬ID");
				sql.AppendLine($"WHERE t_orig.ﾚｰｽID = ?");

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = 全ﾚｰｽ.Count;

				var ｸﾗｽ1 = new List<string>();
				var ｸﾗｽ2 = new List<string>();
				var 回り = new List<string>();
				var 天候 = new List<string>();
				var 馬場 = new List<string>();
				var 馬場状態 = new List<string>();
				var 馬性 = new List<string>();
				var 調教場所 = new List<string>();
				var 一言 = new List<string>();
				var 追切 = new List<string>();

				await WpfUtil.ExecuteOnBackground(async () =>
				{
					ｸﾗｽ1.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT ｸﾗｽ FROM t_orig ORDER BY ｸﾗｽ"));
					ｸﾗｽ2.AddRange(new[] { "G1)", "G2)", "G3)", "(G)", "(L)", "オープン", "3勝", "1600万下", "2勝", "1000万下", "1勝", "500万下", "未勝利", "新馬" });
					回り.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 回り FROM t_orig ORDER BY 回り"));
					天候.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 天候 FROM t_orig ORDER BY 天候"));
					馬場.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 馬場 FROM t_orig ORDER BY 馬場"));
					馬場状態.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 馬場状態 FROM t_orig ORDER BY 馬場状態"));
					馬性.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 馬性 FROM t_orig ORDER BY 馬性"));
					調教場所.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 調教場所 FROM t_orig ORDER BY 調教場所"));
					一言.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 一言 FROM t_orig ORDER BY 一言"));
					追切.AddRange(await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 追切 FROM t_orig ORDER BY 追切"));
				});

				foreach (var id in 全ﾚｰｽ)
				{
					var csvfile = Path.Combine(step5dir, $"{id}.csv");
					if (File.Exists(csvfile))
					{
						if (S5Overwrite.IsChecked)
						{
							FileUtil.Delete(csvfile);
						}
						else
						{
							AddLog("Step5: Skip: {id}");
							Progress.Value += 1;
							continue;
						}
					}
					FileUtil.BeforeCreate(csvfile);

					Func<DbDataReader, Dictionary<string, string>> rowfunc = r =>
					{
						var indexes = Enumerable.Range(0, r.FieldCount)
							.Where(i => i == 0 || !Enumerable.Range(0, i - 1).Any(x => r.GetName(i) == r.GetName(x)))
							.ToArray();
						return indexes.ToDictionary(i => r.GetName(i), i => $"{r.GetValue(i) ?? string.Empty}");
					};
					// ﾚｰｽに紐づく全ﾃﾞｰﾀを取得
					var rows = await conn.GetRows(rowfunc, sql.ToString(), SQLiteUtil.CreateParameter(System.Data.DbType.String, id));

					var arr = rows.Select(row =>
					{
						Func<string, double> date = s => (DateTime.Parse(s) - DateTime.Parse("2000/01/01")).TotalDays;
						var dic = new Dictionary<string, object>();

						dic["ﾚｰｽID"] = row["ﾚｰｽID"];

						dic["開催日"] =　date(row["開催日"]);
						dic["前回日"] = date(row["開催日"]) - date(CoreUtil.Nvl(row["前回開催日"], row["開催日"]));

						dic["ｸﾗｽ1"] = ｸﾗｽ1.IndexOf(row["ｸﾗｽ"]);
						dic["ｸﾗｽ2"] = ｸﾗｽ2.Any(x => row["ｸﾗｽ"].Contains(x)) ? ｸﾗｽ2.IndexOf(ｸﾗｽ2.First(x => row["ｸﾗｽ"].Contains(x))) : ｸﾗｽ2.IndexOf(ｸﾗｽ2.FirstOrDefault(x => row["ﾚｰｽ名"].Contains(x)) ?? "-1");
						dic["回り"] = 回り.IndexOf(row["回り"]);
						dic["距離"] = row["距離"];
						dic["天候"] = 天候.IndexOf(row["天候"]);
						dic["馬場"] = 馬場.IndexOf(row["馬場"]);
						dic["馬場状態"] = 馬場状態.IndexOf(row["馬場状態"]);
						dic["着順"] = row["着順"];
						dic["枠番"] = row["枠番"];
						dic["馬番"] = row["馬番"];
						dic["馬ID"] = row["馬ID"];
						dic["馬性"] = 馬性.IndexOf(row["馬性"]);
						dic["馬齢"] = row["馬齢"];
						dic["斤量"] = row["斤量"].GetDouble();
						dic["斤量平均"] = row["斤量平均"];
						dic["斤量差"] = dic["斤量"].GetDouble() - dic["斤量平均"].GetDouble();
						dic["騎手ID"] = row["騎手ID"];
						dic["ﾀｲﾑ"] = row["ﾀｲﾑ"].Split(':')[0].GetDouble() * 60 + row["ﾀｲﾑ"].Split(':')[1].GetDouble();
						// dic["着差"] = row["着差"]; TODO 着差
						dic["通過"] = row["通過"].Split('-').Average(x => x.GetDouble());
						dic["上り"] = row["上り"];
						dic["単勝"] = row["単勝"];
						dic["人気"] = row["人気"];
						dic["体重"] = double.TryParse(row["体重"], out double taiju) ? taiju : double.NaN;
						dic["増減"] = row["増減"].GetDouble();
						dic["増減割"] = double.IsNaN((double)dic["体重"]) ? double.NaN : (double)dic["増減"] / (double)dic["体重"];
						dic["斤量割"] = double.IsNaN((double)dic["体重"]) ? double.NaN : (double)dic["斤量"] / (double)dic["体重"];
						dic["調教場所"] = 調教場所.IndexOf(row["調教場所"]);
						dic["調教師ID"] = row["調教師ID"];
						dic["馬主ID"] = row["馬主ID"].FromHex();
						dic["一言"] = 一言.IndexOf(row["一言"]);
						dic["追切"] = 追切.IndexOf(row["追切"]);
						dic["馬主累勝利"] = row["馬主累勝利"];
						dic["馬主累連対"] = row["馬主累連対"];
						dic["馬主累複勝"] = row["馬主累複勝"];
						dic["馬主直勝利"] = row["馬主直勝利"];
						dic["馬主直連対"] = row["馬主直連対"];
						dic["馬主直複勝"] = row["馬主直複勝"];
						dic["騎手累勝利"] = row["騎手累勝利"];
						dic["騎手累連対"] = row["騎手累連対"];
						dic["騎手累複勝"] = row["騎手累複勝"];
						dic["騎手直勝利"] = row["騎手直勝利"];
						dic["騎手直連対"] = row["騎手直連対"];
						dic["騎手直複勝"] = row["騎手直複勝"];
						dic["調教師累勝利"] = row["調教師累勝利"];
						dic["調教師累連対"] = row["調教師累連対"];
						dic["調教師累複勝"] = row["調教師累複勝"];
						dic["調教師直勝利"] = row["調教師直勝利"];
						dic["調教師直連対"] = row["調教師直連対"];
						dic["調教師直複勝"] = row["調教師直複勝"];
						dic["馬累着率平"] = row["累着率平"];
						dic["馬直着率平"] = row["直着率平"];
						dic["馬累着率中"] = row["累着率中"];
						dic["馬直着率中"] = row["直着率中"];
						dic["馬累着率偏"] = row["累着率偏"];
						dic["馬直着率偏"] = row["直着率偏"];
						dic["馬累着順平"] = row["累着順平"];
						dic["馬直着順平"] = row["直着順平"];
						dic["馬累着順中"] = row["累着順中"];
						dic["馬直着順中"] = row["直着順中"];
						dic["馬累着順偏"] = row["累着順偏"];
						dic["馬直着順偏"] = row["直着順偏"];
						dic["馬累距離平"] = row["累距離平"];
						dic["馬直距離平"] = row["直距離平"];
						dic["馬累距離中"] = row["累距離中"];
						dic["馬直距離中"] = row["直距離中"];
						dic["馬累距離偏"] = row["累距離偏"];
						dic["馬直距離偏"] = row["直距離偏"];
						dic["馬累着差平"] = row["累着差平"];
						dic["馬直着差平"] = row["直着差平"];
						dic["馬累着差中"] = row["累着差中"];
						dic["馬直着差中"] = row["直着差中"];
						dic["馬累着差偏"] = row["累着差偏"];
						dic["馬直着差偏"] = row["直着差偏"];
						dic["馬累通過平"] = row["累通過平"];
						dic["馬直通過平"] = row["直通過平"];
						dic["馬累通過中"] = row["累通過中"];
						dic["馬直通過中"] = row["直通過中"];
						dic["馬累通過偏"] = row["累通過偏"];
						dic["馬直通過偏"] = row["直通過偏"];
						dic["馬累上り平"] = row["累上り平"];
						dic["馬直上り平"] = row["直上り平"];
						dic["馬累上り中"] = row["累上り中"];
						dic["馬直上り中"] = row["直上り中"];
						dic["馬累上り偏"] = row["累上り偏"];
						dic["馬直上り偏"] = row["直上り偏"];
						dic["馬累体重平"] = row["累体重平"];
						dic["馬直体重平"] = row["直体重平"];
						dic["馬累体重中"] = row["累体重中"];
						dic["馬直体重中"] = row["直体重中"];
						dic["馬累体重偏"] = row["累体重偏"];
						dic["馬直体重偏"] = row["直体重偏"];

						return dic;
					});

					//File.AppendAllLines(Path.Combine(step5dir, $"{id}.csv"), arr.Take(1).Select(x => x.Keys.GetString(",")));
					//File.AppendAllLines(Path.Combine(step5dir, $"{id}.csv"), arr.Select(x => x.Values.GetString(",")));

					if (!File.Exists(dstfile))
					{
						await File.AppendAllLinesAsync(dstfile, arr.Take(1).Select(x => x.Keys.GetString(",")));
					}
					await File.AppendAllLinesAsync(dstfile, arr.Select(x => x.Values.GetString(",")));

					AddLog($"Step5 Proccess ﾚｰｽID: {id}");

					Progress.Value += 1;
				}

				MessageService.Info("Step5 Completed!!");
			}
		});
	}
}
