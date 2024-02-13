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
using AngleSharp.Text;
using System.Data.OleDb;
using System.Text.RegularExpressions;
using MathNet.Numerics.Statistics;
using Tensorflow.Keras.Layers;
using System.Windows.Forms;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public CheckboxItemModel S2Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

		public IRelayCommand S2EXEC => RelayCommand.Create(async _ =>
		{
			using (var conn = CreateSQLiteControl())
			{
				var create = S2Overwrite.IsChecked || !await conn.ExistsColumn("t_model", "着順");

				var drops = await conn.ExistsColumn("t_model", "着順") && !create
					? await conn.GetRows(r => $"{r.GetValue(0)}", "SELECT DISTINCT ﾚｰｽID FROM t_model")
					: Enumerable.Empty<string>();

				// 新馬戦は除外
				var maxdate = await conn.ExecuteScalarAsync("SELECT MAX(開催日数) FROM t_orig");
				var mindate = await conn.ExecuteScalarAsync("SELECT MIN(開催日数) FROM t_orig");
				var target = maxdate.GetDouble().Subtract(mindate.GetDouble()).Multiply(0.3).Add(mindate.GetDouble());
				var racbase = await conn.GetRows(r => r.Get<string>(0),
					"SELECT DISTINCT ﾚｰｽID FROM t_orig WHERE ﾚｰｽID < ? AND 開催日数 >= ? AND ﾗﾝｸ2 < ? ORDER BY ﾚｰｽID DESC",
					SQLiteUtil.CreateParameter(System.Data.DbType.String, "202400000000"),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, target),
					SQLiteUtil.CreateParameter(System.Data.DbType.String, "RANK5")
				);

				var rac = racbase
					.Where(id => !drops.Contains(id))
					.ToArray();

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = rac.Length;

				var ﾗﾝｸ2 = await AppUtil.Getﾗﾝｸ2(conn);
				var 馬性 = await AppUtil.Get馬性(conn);
				var 調教場所 = await AppUtil.Get調教場所(conn);
				var 追切 = await AppUtil.Get追切(conn);

				foreach (var raceid in rac)
				{
					MessageService.Debug($"ﾚｰｽID:開始:{raceid}");

					// ﾚｰｽ毎の纏まり
					var racarr = await CreateRaceModel(conn, raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切);

					if (create)
					{
						create = false;

						// ﾃｰﾌﾞﾙ作成
						await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
						await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_model (" + racarr.First().Keys.Select(x => $"{x} REAL").GetString(",") + ", PRIMARY KEY (ﾚｰｽID, 馬番))");
						await conn.ExecuteNonQueryAsync("CREATE INDEX IF NOT EXISTS t_model_index_00 ON t_model (開催日数)");
					}

					await conn.BeginTransaction();
					foreach (var ins in racarr)
					{
						await conn.ExecuteNonQueryAsync(
							$"INSERT INTO t_model ({ins.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", ins.Keys.Count).GetString(",")})",
							ins.Keys.Select(k => SQLiteUtil.CreateParameter(System.Data.DbType.Double, ins[k])).ToArray()
						);
					}
					conn.Commit();

					AddLog($"Step5 Proccess ﾚｰｽID: {raceid}");

					Progress.Value += 1;
				}

				MessageService.Info("Step5 Completed!!");
			}
		});

		private double Avg1(IEnumerable<Dictionary<string, object>> arr, string n, double def)
		{
			return arr.Select(x => x[n].GetDoubleNaN()).Where(x => !double.IsNaN(x)).Average(def);
		}

		private double Avg2(IEnumerable<Dictionary<string, object>> arr1, Func<Dictionary<string, object>, double> arr_func, double def)
		{
			return arr1.Select(arr_func).Average(def);
		}

		private async Task<IEnumerable<Dictionary<string, double>>> CreateRaceModel(SQLiteControl conn, string raceid, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			// 同ﾚｰｽの平均を取りたいときに使用する
			var 同ﾚｰｽ = await conn.GetRows("SELECT * FROM t_orig WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(System.Data.DbType.String, raceid)
			);

			// ﾚｰｽ毎の纏まり
			var racarr = await 同ﾚｰｽ.Select(src => Task.Run(() => ToModel(conn, src, ﾗﾝｸ2, 馬性, 調教場所, 追切))).WhenAll();

			var drops = Arr("ﾚｰｽID", "開催日数", "ﾗﾝｸ2", "着順", "枠番", "馬番", "馬ID");
			var keys = racarr.First().Keys.Where(y => !drops.Contains(y)).ToArray();

			// 他の馬との比較
			racarr.ForEach(dic =>
			{
				keys.ForEach(key =>
				{
					dic[$"{key}平"] = dic[key] - racarr.Select(x => x[key]).Average();
					dic[$"{key}中"] = dic[key] - racarr.Select(x => x[key]).Median();
				});
			});

			return racarr;
		}

		private async Task<Dictionary<string, double>> ToModel(SQLiteControl conn, Dictionary<string, object> src, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			var 馬情報 = await conn.GetRows(
					$" SELECT t_orig.*, t_top.ﾀｲﾑ TOPﾀｲﾑ, t_top.上り TOP上り" +
					$" FROM t_orig" +
					$" LEFT OUTER JOIN t_orig t_top ON t_orig.ﾚｰｽID = t_top.ﾚｰｽID AND t_orig.開催日数 = t_top.開催日数 AND t_top.着順 = 1" +
					$" WHERE t_orig.馬ID = ? AND t_orig.開催日数 < ? ORDER BY t_orig.開催日数 DESC",
					SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["開催日数"])
			);

			var dic = new Dictionary<string, double>();
			// ﾍｯﾀﾞ情報
			dic["ﾚｰｽID"] = src["ﾚｰｽID"].GetDouble();
			dic["開催日数"] = src["開催日数"].GetDouble();
			//dic["ﾗﾝｸ1"] = ﾗﾝｸ1.IndexOf(src["ﾗﾝｸ1"]);
			dic["ﾗﾝｸ2"] = ﾗﾝｸ2.IndexOf(src["ﾗﾝｸ2"]);

			// 予測したいﾃﾞｰﾀ
			dic["着順"] = src["着順"].GetDouble();

			// 馬毎に違う情報
			dic["枠番"] = src["枠番"].GetDouble();
			dic["馬番"] = src["馬番"].GetDouble();
			dic["馬ID"] = src["馬ID"].GetDouble();
			dic["馬性"] = 馬性.IndexOf(src["馬性"]);
			dic["馬齢"] = src["馬齢"].GetDouble();
			dic["斤量"] = src["斤量"].GetDouble();
			dic["斤量平均"] = Avg1(馬情報, "斤量", 斤量DEF);

			//dic["単勝"] = src["単勝"].GetDouble();
			//dic["人気"] = src["人気"].GetDouble();
			dic["体重"] = double.TryParse($"{src["体重"]}", out double tmp_taiju) ? tmp_taiju : (0 < 馬情報.Count ? 馬情報[0]["体重"].GetDouble() : 体重DEF);
			dic["増減"] = src["増減"].GetDouble();
			dic["増減割"] = dic["増減"] / dic["体重"];
			dic["斤量割"] = dic["斤量"] / dic["体重"];
			dic["調教場所"] = 調教場所.IndexOf(src["調教場所"]);
			//dic["一言"] = 一言.IndexOf(src["一言"]);
			dic["追切"] = 追切.IndexOf(src["追切"]);

			var analysis = await Arr(
				GetAnalysis(conn, 365, src, "馬ID", new[] { "馬ID", "馬場", "馬場状態" }),
				GetAnalysis(conn, 180, src, "騎手ID", new[] { "騎手ID", "馬場", "馬場状態" }),
				GetAnalysis(conn, 180, src, "調教師ID", new[] { "調教師ID" }),
				GetAnalysis(conn, 180, src, "馬主ID", new[] { "馬主ID" })
			).WhenAll();

			dic.AddRange(analysis.SelectMany(x => x));

			// 得意距離、及び今回のﾚｰｽ距離との差
			dic["距離"] = src["距離"].GetDouble();
			dic["馬_得意距離"] = 馬情報.Select(x => x["距離"].GetDouble()).Average(dic["距離"]);
			dic["馬_距離差"] = dic["馬_得意距離"] - dic["距離"];

			// 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			Func<object, double> func_tuka = v => $"{v}".Split('-').Take(2).Select(x => x.GetDouble()).Average(通過DEF);
			dic["通過平均"] = Avg2(馬情報, x => func_tuka(x["通過"]), 着順DEF);

			// 通過順の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			dic["上り平均"] = Avg2(馬情報, x => x["上り"].GetDouble(), 上りDEF);
			dic["上り最小"] = 馬情報.MinOrDefault(x => x["上り"].GetDouble(), 上りDEF);

			// ﾀｲﾑの平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			Func<object, double> func_time = v => $"{v}".Split(':')[0].GetDouble() * 60 + $"{v}".Split(':')[1].GetDouble();
			dic["時間平均"] = Avg2(馬情報, x => x["距離"].GetDouble() / func_time(x["ﾀｲﾑ"]), 時間DEF);

			// 1着との差
			dic["勝馬時差"] = 馬情報.Select(x => func_time(x["ﾀｲﾑ"]) - func_time(x["TOPﾀｲﾑ"])).Average(時差DEF);
			dic["勝馬上差"] = 馬情報.Select(x => x["上り"].GetDouble() - x["TOP上り"].GetDouble()).Average(時差DEF);

			// 着順平均
			dic["着順平均"] = Avg2(馬情報, x => GET着順(x), 着順DEF);

			//// 着差の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			//Func<object, double> func_tyaku = v => CoreUtil.Nvl(
			//	$"{v}"
			//	.Replace("ハナ", "0.05")
			//	.Replace("アタマ", "0.10")
			//	.Replace("クビ", "0.15")
			//	.Replace("同着", "0")
			//	.Replace("大", "15")
			//	.Replace("1/4", "25")
			//	.Replace("1/2", "50")
			//	.Replace("3/4", "75"), "-0.25")
			//	.Split('+').Sum(x => double.Parse(x));
			////dic["着差"] = func_tyaku(src["着差"]);
			//dic["着差平均"] = func_avg2(馬情報, x => func_tyaku(x["着差"]), 着差DEF);

			for (var i = 0; i < 4; i++)
			{
				if (i < 馬情報.Count)
				{
					dic[$"前{i + 1}_着順"] = GET着順(馬情報[i]);
					dic[$"前{i + 1}_上り"] = 馬情報[i]["上り"].GetDouble();
					dic[$"前{i + 1}_斤量"] = 馬情報[i]["斤量"].GetDouble();
					dic[$"前{i + 1}_日数"] = dic["開催日数"] - 馬情報[i]["開催日数"].GetDouble();
					dic[$"前{i + 1}_時間"] = 馬情報[i]["距離"].GetDouble() / func_time(馬情報[i]["ﾀｲﾑ"]);
					dic[$"前{i + 1}_時差"] = func_time(馬情報[i]["ﾀｲﾑ"]) - func_time(馬情報[i]["TOPﾀｲﾑ"]);
					dic[$"前{i + 1}_上差"] = 馬情報[i]["上り"].GetDouble() - 馬情報[i]["TOP上り"].GetDouble();
				}
				else
				{
					dic[$"前{i + 1}_着順"] = i == 0 ? 着順DEF : dic[$"前{i}_着順"];
					dic[$"前{i + 1}_上り"] = i == 0 ? 上りDEF : dic[$"前{i}_上り"];
					dic[$"前{i + 1}_斤量"] = i == 0 ? 斤量DEF : dic[$"前{i}_斤量"];
					dic[$"前{i + 1}_日数"] = i == 0 ? 日数DEF : dic[$"前{i}_日数"] * 2;
					dic[$"前{i + 1}_時間"] = i == 0 ? 時間DEF : dic[$"前{i}_時間"];
					dic[$"前{i + 1}_時差"] = i == 0 ? 時差DEF : dic[$"前{i}_時差"];
                    dic[$"前{i + 1}_上差"] = i == 0 ? 時差DEF : dic[$"前{i}_上差"];
                }
            }

			return dic;
		}

		private double GET着順(Dictionary<string, object> x)
		{
			var 着順 = x["着順"].GetDouble();
			switch ($"{x["ﾗﾝｸ2"]}")
			{
				case "RANK1":
					return 着順 * 0.50;
				case "RANK2":
					return 着順 * 0.75;
				case "RANK3":
					return 着順 * 1.00;
				case "RANK4":
					return 着順 * 1.25;
				default:
					return 着順 * 1.50;
			}
		}

		private async Task<Dictionary<string, double>> GetAnalysis(SQLiteControl conn, int num, Dictionary<string, object> src, string keyname, string[] headnames)
		{
			var ranks = new[] { 1, 3, 5 };
			var fieldCount = ranks.Length * headnames.Length * 10;
			var now = $"{src["開催日数"]}".GetDouble();
			var keyvalue = $"{src[keyname]}";

			var whe = new List<string>();
			foreach (var i in ranks)
			{
				foreach (var headname in headnames)
				{
					var key = keyname == headname ? keyname : $"{keyname}_{headname}";
					var val = i == 1
						? "着順 * 0.50"
						: i == 3
						? "着順 * (CASE WHEN ﾗﾝｸ2 = 'RANK1' THEN 0.50 WHEN ﾗﾝｸ2 = 'RANK2' THEN 0.75 ELSE 1.00 END)"
						: "着順 * (CASE WHEN ﾗﾝｸ2 = 'RANK1' THEN 0.50 WHEN ﾗﾝｸ2 = 'RANK2' THEN 0.75 WHEN ﾗﾝｸ2 = 'RANK3' THEN 1.00 WHEN ﾗﾝｸ2 = 'RANK4' THEN 1.25 ELSE 1.50 END)";

					whe.Add(Arr(
						$"( SELECT",
						$"                                  IFNULL(MEDIAN({val}), {着順DEF})           R{i}A1_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) + IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}A2_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) - IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}A3_{key},",
						$"                          IFNULL(LOWER_QUARTILE({val}), {着順DEF})           R{i}A4_{key},",
						$"                          IFNULL(UPPER_QUARTILE({val}), {着順DEF})           R{i}A5_{key} ",
						$"FROM t_orig WHERE",
						$"t_orig.{keyname} = '{keyvalue}' AND",
						$"t_orig.開催日数  < {now}        AND",
						$"t_orig.ﾗﾝｸ2     <= 'RANK{i}'",
						keyname == headname ? string.Empty : $"AND t_orig.{headname} = '{src[headname]}'",
						$") R{i}A_{key} "
					).GetString(" "));

					whe.Add(Arr(
						$"( SELECT",
						$"                                  IFNULL(MEDIAN({val}), {着順DEF})           R{i}N1_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) + IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}N2_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) - IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}N3_{key},",
						$"                          IFNULL(LOWER_QUARTILE({val}), {着順DEF})           R{i}N4_{key},",
						$"                          IFNULL(UPPER_QUARTILE({val}), {着順DEF})           R{i}N5_{key} ",
						$"FROM t_orig WHERE",
						$"t_orig.{keyname} = '{keyvalue}' AND",
						$"t_orig.開催日数  < {now}        AND",
						$"t_orig.開催日数  > {now - num}  AND",
						$"t_orig.ﾗﾝｸ2     <= 'RANK{i}'",
						keyname == headname ? string.Empty : $"AND t_orig.{headname} = '{src[headname]}'",
						$") R{i}A_{key} "
					).GetString(" "));
				}
			}
			var dic = new Dictionary<string, double>();
			using (var reader = await conn.ExecuteReaderAsync(Arr($"SELECT * FROM", whe.GetString("CROSS JOIN")).GetString(" ")))
			{
				if (await reader.ReadAsync())
				{
					for (var i = 0; i < fieldCount; i++)
					{
						dic[reader.GetName(i)] = reader.GetDouble(i);
					}
				}
			}

			return dic;
		}

		//private const double 着差DEF = 10;
		private const double 体重DEF = 470;

		private const double 通過DEF = 6;
		private const double 着順DEF = 8;
		private const double 着順偏差DEF = 4.5;
		private const double 上りDEF = 36;
		private const double 斤量DEF = 58;
		private const double 日数DEF = 30 * 3;
		private const double 時間DEF = 15.78;
		private const double 時差DEF = 0.72;
	}
}