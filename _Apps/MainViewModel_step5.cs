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

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = 全ﾚｰｽ.Count;

				Func<string, Task<IEnumerable<string>>> get_distinct = async x => (await conn.GetRows($"SELECT DISTINCT {x} FROM t_orig ORDER BY {x}")).Select(y => $"{y[x]}");

				var ｸﾗｽ = new List<string>(await get_distinct("ｸﾗｽ"));
				var ﾗﾝｸ = new List<string>(await get_distinct("ﾗﾝｸ"));
				var 回り = new List<string>(await get_distinct("回り"));
				var 天候 = new List<string>(await get_distinct("天候"));
				var 馬場 = new List<string>(await get_distinct("馬場"));
				var 馬場状態 = new List<string>(await get_distinct("馬場状態"));
				var 馬性 = new List<string>(await get_distinct("馬性"));
				var 調教場所 = new List<string>(await get_distinct("調教場所"));
				var 一言 = new List<string>(await get_distinct("一言"));
				var 追切 = new List<string>(await get_distinct("追切"));

				foreach (var id in 全ﾚｰｽ)
				{
					// ﾚｰｽに紐づくﾃﾞｰﾀを取得
					var rows = (await conn.GetRows("SELECT * FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(System.Data.DbType.String, id))).Select(x => x.Keys.ToDictionary(y => y, y => $"{x[y]}"));

					var arr = rows.Select(row =>
					{
						var dic = new Dictionary<string, object>();

						// dic["ﾚｰｽID"] = row["ﾚｰｽID"];

						// dic["開催日数"] =row["開催日数"];

						// 予測したいﾃﾞｰﾀ
						dic["着順"] = row["着順"];

						// 馬毎に違う情報

						dic["枠番"] = row["枠番"];
						dic["馬番"] = row["馬番"];
						dic["馬ID"] = row["馬ID"];
						dic["馬性"] = 馬性.IndexOf(row["馬性"]);
						dic["馬齢"] = row["馬齢"];
						dic["斤量"] = row["斤量"].GetDouble();
						dic["斤量差"] = dic["斤量"].GetDouble() - rows.Select(x => x["斤量"].GetDouble()).Average();
						dic["単勝"] = row["単勝"];
						dic["人気"] = row["人気"];
						dic["体重"] = double.TryParse(row["体重"], out double taiju) ? taiju : double.NaN;
						dic["増減"] = row["増減"].GetDouble();
						dic["体重差"] = double.IsNaN((double)dic["体重"]) ? double.NaN : (double)dic["体重"] - rows.Where(x => double.TryParse(x["体重"], out double dummy)).Select(x => double.Parse(x["体重"])).Average();
						dic["増減割"] = double.IsNaN((double)dic["体重"]) ? double.NaN : (double)dic["増減"] / (double)dic["体重"];
						dic["斤量割"] = double.IsNaN((double)dic["体重"]) ? double.NaN : (double)dic["斤量"] / (double)dic["体重"];
						dic["調教場所"] = 調教場所.IndexOf(row["調教場所"]);
						dic["一言"] = 一言.IndexOf(row["一言"]);
						dic["追切"] = 追切.IndexOf(row["追切"]);

						// 
						dic["通過"] = row["通過"].Split('-').Average(x => x.GetDouble());
						dic["上り"] = row["上り"];
						dic["ﾀｲﾑ"] = double.Parse(row["距離"]) / row["ﾀｲﾑ"].Split(':')[0].GetDouble() * 60 + row["ﾀｲﾑ"].Split(':')[1].GetDouble();

						// ﾚｰｽ情報に対する馬の成績を追加する→過去ﾚｰｽから算出する
						// 
						dic["開催場所"] = ｸﾗｽ.IndexOf(row["ｸﾗｽ"]);
						dic["ｸﾗｽ"] = ｸﾗｽ.IndexOf(row["ｸﾗｽ"]);
						dic["ﾗﾝｸ"] = ﾗﾝｸ.IndexOf(row["ﾗﾝｸ"]);
						dic["回り"] = 回り.IndexOf(row["回り"]);
						dic["距離"] = row["距離"];
						dic["天候"] = 天候.IndexOf(row["天候"]);
						dic["馬場"] = 馬場.IndexOf(row["馬場"]);
						dic["馬場状態"] = 馬場状態.IndexOf(row["馬場状態"]);
						dic["着差"] = row["着差"];

						// ﾚｰｽ情報に対する騎手の成績を追加する→過去ﾚｰｽから算出する
						dic["騎手ID"] = row["騎手ID"];


						dic["調教師ID"] = row["調教師ID"];
						dic["馬主ID"] = row["馬主ID"].FromHex();

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
