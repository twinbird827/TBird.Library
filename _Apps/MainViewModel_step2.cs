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

				// 血統情報の作成
				await RefreshKetto(conn, S2Overwrite.IsChecked);

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
						await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_model (ﾚｰｽID INTEGER,開催日数 INTEGER,枠番 INTEGER,馬番 INTEGER,着順 INTEGER,Features BLOB, PRIMARY KEY (ﾚｰｽID, 馬番))");
					}

					string[]? keys = null;
					await conn.BeginTransaction();
					foreach (var ins in racarr)
					{
						keys = keys ?? ins.Keys.Where(x => !new[] { "ﾚｰｽID", "開催日数", "枠番", "馬番", "着順" }.Contains(x)).ToArray();

						await conn.ExecuteNonQueryAsync(
							$"INSERT INTO t_model (ﾚｰｽID,開催日数,枠番,馬番,着順,Features) VALUES (?, ?, ?, ?, ?, ?)",
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["ﾚｰｽID"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["開催日数"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["枠番"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["馬番"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["着順"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Binary, keys.SelectMany(x => BitConverter.GetBytes((float)ins[x])).ToArray())
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
					var tmp = racarr.Select(x => x[key]).ToArray();
					dic[$"{key}S1"] = dic[key] - tmp.Average();
					dic[$"{key}S2"] = dic[key] - tmp.Minimum();
					dic[$"{key}S3"] = dic[key] - tmp.Maximum();
					dic[$"{key}S5"] = dic[key] - tmp.Percentile(25);
					dic[$"{key}S6"] = dic[key] - tmp.Percentile(50);
					dic[$"{key}S7"] = dic[key] - tmp.Percentile(75);
				});
			});

			return racarr;
		}

		private async Task<Dictionary<string, double>> ToModel(SQLiteControl conn, Dictionary<string, object> src, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			var 馬情報全 = await conn.GetRows(
					$" SELECT t_orig.*, t_top.ﾀｲﾑ TOPﾀｲﾑ, t_top.上り TOP上り, t_top.斤量 TOP斤量" +
					$" FROM t_orig" +
					$" LEFT OUTER JOIN t_orig t_top ON t_orig.ﾚｰｽID = t_top.ﾚｰｽID AND t_orig.開催日数 = t_top.開催日数 AND t_top.着順 = 1" +
					$" WHERE t_orig.馬ID = ? AND t_orig.開催日数 < ? ORDER BY t_orig.開催日数 DESC",
					SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["開催日数"])
			);

			var 馬情報直 = 馬情報全.Take(3).ToArray();

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
			dic["斤量平全"] = Avg1(馬情報全, "斤量", 斤量DEF);
			dic["斤量平直"] = Avg1(馬情報直, "斤量", 斤量DEF);

			//dic["単勝"] = src["単勝"].GetDouble();
			//dic["人気"] = src["人気"].GetDouble();
			dic["体重"] = double.TryParse($"{src["体重"]}", out double tmp_taiju) ? tmp_taiju : (0 < 馬情報直.Length ? 馬情報直[0]["体重"].GetDouble() : 体重DEF);
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
			dic["得意距離全"] = 馬情報全.Select(x => x["距離"].GetDouble()).Average(dic["距離"]);
			dic["得意距離直"] = 馬情報直.Select(x => x["距離"].GetDouble()).Average(dic["距離"]);
			dic["距離差全"] = dic["得意距離全"] - dic["距離"];
			dic["距離差直"] = dic["得意距離直"] - dic["距離"];

			// 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			Func<object, double> func_tuka = v => $"{v}".Split('-').Take(2).Select(x => x.GetDouble()).Average(通過DEF);
			dic["通過全"] = Avg2(馬情報全, x => func_tuka(x["通過"]), 着順DEF);
			dic["通過直"] = Avg2(馬情報直, x => func_tuka(x["通過"]), 着順DEF);

			// 通過順の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			dic["上り全"] = Avg2(馬情報全, x => x["上り"].GetDouble(), 上りDEF);
			dic["上り直"] = Avg2(馬情報直, x => x["上り"].GetDouble(), 上りDEF);
			dic["上小全"] = 馬情報全.MinOrDefault(x => x["上り"].GetDouble(), 上りDEF);
			dic["上小直"] = 馬情報直.MinOrDefault(x => x["上り"].GetDouble(), 上りDEF);
			dic["上大全"] = 馬情報全.MaxOrDefault(x => x["上り"].GetDouble(), 上りDEF);
			dic["上大直"] = 馬情報直.MaxOrDefault(x => x["上り"].GetDouble(), 上りDEF);

			dic["斤上り全"] = Avg2(馬情報全, x => Get斤上(x), 上りDEF);
			dic["斤上り直"] = Avg2(馬情報直, x => Get斤上(x), 上りDEF);
			dic["斤上小全"] = 馬情報全.MinOrDefault(x => Get斤上(x), 上りDEF);
			dic["斤上小直"] = 馬情報直.MinOrDefault(x => Get斤上(x), 上りDEF);
			dic["斤上大全"] = 馬情報全.MaxOrDefault(x => Get斤上(x), 上りDEF);
			dic["斤上大直"] = 馬情報直.MaxOrDefault(x => Get斤上(x), 上りDEF);

			// ﾀｲﾑの平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			Func<object, double> func_time = v => $"{v}".Split(':')[0].GetDouble() * 60 + $"{v}".Split(':')[1].GetDouble();
			dic["時間全"] = Avg2(馬情報全, x => x["距離"].GetDouble() / func_time(x["ﾀｲﾑ"]), 時間DEF);
			dic["時間直"] = Avg2(馬情報直, x => x["距離"].GetDouble() / func_time(x["ﾀｲﾑ"]), 時間DEF);

			// 1着との差(時間)
			dic["勝馬時差全"] = 馬情報全.Select(x => func_time(x["ﾀｲﾑ"]) - func_time(x["TOPﾀｲﾑ"])).Average(時差DEF);
			dic["勝馬時差直"] = 馬情報直.Select(x => func_time(x["ﾀｲﾑ"]) - func_time(x["TOPﾀｲﾑ"])).Average(時差DEF);

			dic["勝馬上り差全"] = 馬情報全.Select(x => x["上り"].GetDouble() - x["TOP上り"].GetDouble()).Average(時差DEF);
			dic["勝馬上り差直"] = 馬情報直.Select(x => x["上り"].GetDouble() - x["TOP上り"].GetDouble()).Average(時差DEF);

			dic["勝馬斤上差全"] = 馬情報全.Select(x => Get斤上(x) - Get斤上(x, "TOP上り", "TOP斤量")).Average(時差DEF);
			dic["勝馬斤上差直"] = 馬情報直.Select(x => x["上り"].GetDouble() - x["TOP上り"].GetDouble()).Average(時差DEF);

			// 着順平均
			dic["着順全"] = Avg2(馬情報全, x => GET着順(x), 着順DEF);
			dic["着順直"] = Avg2(馬情報直, x => GET着順(x), 着順DEF);

			for (var i = 0; i < 4; i++)
			{
				if (i < 馬情報直.Length)
				{
					dic[$"前{i + 1}_着順"] = GET着順(馬情報直[i]);
					dic[$"前{i + 1}_上り"] = 馬情報直[i]["上り"].GetDouble();
					dic[$"前{i + 1}_斤上"] = Get斤上(馬情報直[i]);
					dic[$"前{i + 1}_斤量"] = 馬情報直[i]["斤量"].GetDouble();
					dic[$"前{i + 1}_日数"] = dic["開催日数"] - 馬情報直[i]["開催日数"].GetDouble();
					dic[$"前{i + 1}_時間"] = 馬情報直[i]["距離"].GetDouble() / func_time(馬情報直[i]["ﾀｲﾑ"]);
					dic[$"前{i + 1}_時差"] = func_time(馬情報直[i]["ﾀｲﾑ"]) - func_time(馬情報直[i]["TOPﾀｲﾑ"]);
					dic[$"前{i + 1}_上差"] = 馬情報直[i]["上り"].GetDouble() - 馬情報直[i]["TOP上り"].GetDouble();
					dic[$"前{i + 1}_斤上差"] = Get斤上(馬情報直[i]) - Get斤上(馬情報直[i], "TOP上り", "TOP斤量");
				}
				else
				{
					dic[$"前{i + 1}_着順"] = i == 0 ? 着順DEF : dic[$"前{i}_着順"];
					dic[$"前{i + 1}_上り"] = i == 0 ? 上りDEF : dic[$"前{i}_上り"];
					dic[$"前{i + 1}_斤上"] = i == 0 ? 上りDEF : dic[$"前{i}_斤上"];
					dic[$"前{i + 1}_斤量"] = i == 0 ? 斤量DEF : dic[$"前{i}_斤量"];
					dic[$"前{i + 1}_日数"] = i == 0 ? 日数DEF : dic[$"前{i}_日数"] * 2;
					dic[$"前{i + 1}_時間"] = i == 0 ? 時間DEF : dic[$"前{i}_時間"];
					dic[$"前{i + 1}_時差"] = i == 0 ? 時差DEF : dic[$"前{i}_時差"];
					dic[$"前{i + 1}_上差"] = i == 0 ? 時差DEF : dic[$"前{i}_上差"];
					dic[$"前{i + 1}_斤上差"] = i == 0 ? 上りDEF : dic[$"前{i}_斤上差"];
				}
			}

			dic.AddRange((await conn.GetRows(r => Enumerable.Range(0, 12 * 14).ToDictionary(i => $"C{i.ToString(4)}", i => r.GetValue(i).GetDouble()), Arr(
				$"SELECT F.着AVLG F着AVLG,F.着MEDN F着MEDN,F.着AVPV F着AVPV,F.着AVMV F着AVMV,F.着LOQU F着LOQU,F.着UPQU F着UPQU,F.上AVLG F上AVLG,F.上MEDN F上MEDN,F.上AVPV F上AVPV,F.上AVMV F上AVMV,F.上LOQU F上LOQU,F.上UPQU F上UPQU,FF.着AVLG FF着AVLG,FF.着MEDN FF着MEDN,FF.着AVPV FF着AVPV,FF.着AVMV FF着AVMV,FF.着LOQU FF着LOQU,FF.着UPQU FF着UPQU,FF.上AVLG FF上AVLG,FF.上MEDN FF上MEDN,FF.上AVPV FF上AVPV,FF.上AVMV FF上AVMV,FF.上LOQU FF上LOQU,FF.上UPQU FF上UPQU,FFF.着AVLG FFF着AVLG,FFF.着MEDN FFF着MEDN,FFF.着AVPV FFF着AVPV,FFF.着AVMV FFF着AVMV,FFF.着LOQU FFF着LOQU,FFF.着UPQU FFF着UPQU,FFF.上AVLG FFF上AVLG,FFF.上MEDN FFF上MEDN,FFF.上AVPV FFF上AVPV,FFF.上AVMV FFF上AVMV,FFF.上LOQU FFF上LOQU,FFF.上UPQU FFF上UPQU,FFM.着AVLG FFM着AVLG,FFM.着MEDN FFM着MEDN,FFM.着AVPV FFM着AVPV,FFM.着AVMV FFM着AVMV,FFM.着LOQU FFM着LOQU,FFM.着UPQU FFM着UPQU,FFM.上AVLG FFM上AVLG,FFM.上MEDN FFM上MEDN,FFM.上AVPV FFM上AVPV,FFM.上AVMV FFM上AVMV,FFM.上LOQU FFM上LOQU,FFM.上UPQU FFM上UPQU,FM.着AVLG FM着AVLG,FM.着MEDN FM着MEDN,FM.着AVPV FM着AVPV,FM.着AVMV FM着AVMV,FM.着LOQU FM着LOQU,FM.着UPQU FM着UPQU,FM.上AVLG FM上AVLG,FM.上MEDN FM上MEDN,FM.上AVPV FM上AVPV,FM.上AVMV FM上AVMV,FM.上LOQU FM上LOQU,FM.上UPQU FM上UPQU,FMF.着AVLG FMF着AVLG,FMF.着MEDN FMF着MEDN,FMF.着AVPV FMF着AVPV,FMF.着AVMV FMF着AVMV,FMF.着LOQU FMF着LOQU,FMF.着UPQU FMF着UPQU,FMF.上AVLG FMF上AVLG,FMF.上MEDN FMF上MEDN,FMF.上AVPV FMF上AVPV,FMF.上AVMV FMF上AVMV,FMF.上LOQU FMF上LOQU,FMF.上UPQU FMF上UPQU,FMM.着AVLG FMM着AVLG,FMM.着MEDN FMM着MEDN,FMM.着AVPV FMM着AVPV,FMM.着AVMV FMM着AVMV,FMM.着LOQU FMM着LOQU,FMM.着UPQU FMM着UPQU,FMM.上AVLG FMM上AVLG,FMM.上MEDN FMM上MEDN,FMM.上AVPV FMM上AVPV,FMM.上AVMV FMM上AVMV,FMM.上LOQU FMM上LOQU,FMM.上UPQU FMM上UPQU,M.着AVLG M着AVLG,M.着MEDN M着MEDN,M.着AVPV M着AVPV,M.着AVMV M着AVMV,M.着LOQU M着LOQU,M.着UPQU M着UPQU,M.上AVLG M上AVLG,M.上MEDN M上MEDN,M.上AVPV M上AVPV,M.上AVMV M上AVMV,M.上LOQU M上LOQU,M.上UPQU M上UPQU,MF.着AVLG MF着AVLG,MF.着MEDN MF着MEDN,MF.着AVPV MF着AVPV,MF.着AVMV MF着AVMV,MF.着LOQU MF着LOQU,MF.着UPQU MF着UPQU,MF.上AVLG MF上AVLG,MF.上MEDN MF上MEDN,MF.上AVPV MF上AVPV,MF.上AVMV MF上AVMV,MF.上LOQU MF上LOQU,MF.上UPQU MF上UPQU,MFF.着AVLG MFF着AVLG,MFF.着MEDN MFF着MEDN,MFF.着AVPV MFF着AVPV,MFF.着AVMV MFF着AVMV,MFF.着LOQU MFF着LOQU,MFF.着UPQU MFF着UPQU,MFF.上AVLG MFF上AVLG,MFF.上MEDN MFF上MEDN,MFF.上AVPV MFF上AVPV,MFF.上AVMV MFF上AVMV,MFF.上LOQU MFF上LOQU,MFF.上UPQU MFF上UPQU,MFM.着AVLG MFM着AVLG,MFM.着MEDN MFM着MEDN,MFM.着AVPV MFM着AVPV,MFM.着AVMV MFM着AVMV,MFM.着LOQU MFM着LOQU,MFM.着UPQU MFM着UPQU,MFM.上AVLG MFM上AVLG,MFM.上MEDN MFM上MEDN,MFM.上AVPV MFM上AVPV,MFM.上AVMV MFM上AVMV,MFM.上LOQU MFM上LOQU,MFM.上UPQU MFM上UPQU,MM.着AVLG MM着AVLG,MM.着MEDN MM着MEDN,MM.着AVPV MM着AVPV,MM.着AVMV MM着AVMV,MM.着LOQU MM着LOQU,MM.着UPQU MM着UPQU,MM.上AVLG MM上AVLG,MM.上MEDN MM上MEDN,MM.上AVPV MM上AVPV,MM.上AVMV MM上AVMV,MM.上LOQU MM上LOQU,MM.上UPQU MM上UPQU,MMF.着AVLG MMF着AVLG,MMF.着MEDN MMF着MEDN,MMF.着AVPV MMF着AVPV,MMF.着AVMV MMF着AVMV,MMF.着LOQU MMF着LOQU,MMF.着UPQU MMF着UPQU,MMF.上AVLG MMF上AVLG,MMF.上MEDN MMF上MEDN,MMF.上AVPV MMF上AVPV,MMF.上AVMV MMF上AVMV,MMF.上LOQU MMF上LOQU,MMF.上UPQU MMF上UPQU,MMM.着AVLG MMM着AVLG,MMM.着MEDN MMM着MEDN,MMM.着AVPV MMM着AVPV,MMM.着AVMV MMM着AVMV,MMM.着LOQU MMM着LOQU,MMM.着UPQU MMM着UPQU,MMM.上AVLG MMM上AVLG,MMM.上MEDN MMM上MEDN,MMM.上AVPV MMM上AVPV,MMM.上AVMV MMM上AVMV,MMM.上LOQU MMM上LOQU,MMM.上UPQU MMM上UPQU",
				$"FROM t_ketto I",
				$"LEFT JOIN t_ketto F   ON I.F  = F.馬ID",
				$"LEFT JOIN t_ketto FF  ON F.F  = FF.馬ID",
				$"LEFT JOIN t_ketto FFF ON FF.F = FFF.馬ID",
				$"LEFT JOIN t_ketto FFM ON FF.M = FFM.馬ID",
				$"LEFT JOIN t_ketto FM  ON F.M  = FM.馬ID",
				$"LEFT JOIN t_ketto FMF ON FM.F = FMF.馬ID",
				$"LEFT JOIN t_ketto FMM ON FM.M = FMM.馬ID",
				$"LEFT JOIN t_ketto M   ON I.M  = M.馬ID",
				$"LEFT JOIN t_ketto MF  ON M.F  = MF.馬ID",
				$"LEFT JOIN t_ketto MFF ON MF.F = MFF.馬ID",
				$"LEFT JOIN t_ketto MFM ON MF.M = MFM.馬ID",
				$"LEFT JOIN t_ketto MM  ON M.M  = MM.馬ID",
				$"LEFT JOIN t_ketto MMF ON MM.F = MMF.馬ID",
				$"LEFT JOIN t_ketto MMM ON MM.M = MMM.馬ID",
				$"WHERE t_ketto.馬ID = key"
			).GetString(",")))[0]);

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

		private double Get斤上(Dictionary<string, object> src, string k1, string k2) => src[k1].GetDouble().Multiply(600D).Divide(src[k2].GetDouble().Add(545D));

		private double Get斤上(Dictionary<string, object> src) => Get斤上(src, "上り", "斤量");

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
						$"                                     IFNULL(AVG({val}), {着順DEF})           R{i}A1_{key},",
						$"                                  IFNULL(MEDIAN({val}), {着順DEF})           R{i}A2_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) + IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}A4_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) - IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}A5_{key},",
						$"                          IFNULL(LOWER_QUARTILE({val}), {着順DEF})           R{i}A6_{key},",
						$"                          IFNULL(UPPER_QUARTILE({val}), {着順DEF})           R{i}A7_{key} ",
						$"FROM t_orig WHERE",
						$"t_orig.{keyname} = '{keyvalue}' AND",
						$"t_orig.開催日数  < {now}        AND",
						$"t_orig.ﾗﾝｸ2     <= 'RANK{i}'",
						keyname == headname ? string.Empty : $"AND t_orig.{headname} = '{src[headname]}'",
						$") R{i}A_{key} "
					).GetString(" "));

					whe.Add(Arr(
						$"( SELECT",
						$"                                     IFNULL(AVG({val}), {着順DEF})           R{i}N1_{key},",
						$"                                  IFNULL(MEDIAN({val}), {着順DEF})           R{i}N2_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) + IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}N4_{key},",
						$"IFNULL(AVG({val}), {着順DEF}) - IFNULL(VARIANCE({val}), {着順偏差DEF}      ) R{i}N5_{key},",
						$"                          IFNULL(LOWER_QUARTILE({val}), {着順DEF})           R{i}N6_{key},",
						$"                          IFNULL(UPPER_QUARTILE({val}), {着順DEF})           R{i}N7_{key} ",
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

		private async Task RefreshKetto(SQLiteControl conn, bool ischecked)
		{
			var overwrite = ischecked || !await conn.ExistsColumn("t_ketto", "馬ID");

			if (overwrite)
			{
				// ﾃｰﾌﾞﾙ作成
				await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_ketto");
				await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_ketto (馬ID,F,M,着AVLG,着MEDN,着AVPV,着AVMV,着LOQU,着UPQU,上AVLG,上MEDN,上AVPV,上AVMV,上LOQU,上UPQU, PRIMARY KEY (馬ID))");
			}

			var drops = await conn.GetRows(tmp => tmp.Get<string>(0), "SELECT DISTINCT 馬ID FROM t_ketto");

			await conn.BeginTransaction();
			using (var keys = await conn.ExecuteReaderAsync("SELECT DISTINCT 馬ID FROM t_orig"))
			{
				while (await keys.ReadAsync())
				{
					var key = keys.Get<string>(0);
					// 重複ﾁｪｯｸ
					if (drops.Contains(key)) continue;

					// F,Mを取得する
					var dic = new Dictionary<string, object>();

					dic["馬ID"] = key;
					dic["F"] = key;
					dic["M"] = key;

					var 着順 = "着順 * (CASE WHEN ﾗﾝｸ2 = 'RANK1' THEN 0.50 WHEN ﾗﾝｸ2 = 'RANK2' THEN 0.75 WHEN ﾗﾝｸ2 = 'RANK3' THEN 1.00 WHEN ﾗﾝｸ2 = 'RANK4' THEN 1.25 ELSE 1.50 END)";
					var 上り = "上り";

					var sql = Arr(
						$"SELECT",
						$"                                      IFNULL(AVG({着順}), {着順DEF})           着AVLG,",
						$"                                   IFNULL(MEDIAN({着順}), {着順DEF})           着MEDN,",
						$"IFNULL(AVG({着順}), {着順DEF}) + IFNULL(VARIANCE({着順}), {着順偏差DEF}      ) 着AVPV,",
						$"IFNULL(AVG({着順}), {着順DEF}) - IFNULL(VARIANCE({着順}), {着順偏差DEF}      ) 着AVMV,",
						$"                           IFNULL(LOWER_QUARTILE({着順}), {着順DEF})           着LOQU,",
						$"                           IFNULL(UPPER_QUARTILE({着順}), {着順DEF})           着UPQU,",
						$"                                      IFNULL(AVG({上り}), {上りDEF})           上AVLG,",
						$"                                   IFNULL(MEDIAN({上り}), {上りDEF})           上MEDN,",
						$"IFNULL(AVG({上り}), {上りDEF}) + IFNULL(VARIANCE({上り}), {上り偏差DEF}      ) 上AVPV,",
						$"IFNULL(AVG({上り}), {上りDEF}) - IFNULL(VARIANCE({上り}), {上り偏差DEF}      ) 上AVMV,",
						$"                           IFNULL(LOWER_QUARTILE({上り}), {上りDEF})           上LOQU,",
						$"                           IFNULL(UPPER_QUARTILE({上り}), {上りDEF})           上UPQU ",
						$"FROM  t_orig",
						$"WHERE t_orig.馬ID = '{key}'"
					).GetString(" ");

					dic.AddRange((await conn.GetRows(sql))[0]);

					await conn.ExecuteNonQueryAsync(
						$"INSERT INTO t_ketto ({dic.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", dic.Keys.Count)})",
						dic.Values.Select(x => SQLiteUtil.CreateParameter(System.Data.DbType.Object, x)).ToArray()
					);
				}
			}

		}

		//private const double 着差DEF = 10;
		private const double 体重DEF = 470;

		private const double 通過DEF = 6;
		private const double 着順DEF = 8;
		private const double 着順偏差DEF = 4.5;
		private const double 上りDEF = 36;
		private const double 上り偏差DEF = 2.5;
		private const double 斤量DEF = 58;
		private const double 日数DEF = 30 * 3;
		private const double 時間DEF = 15.78;
		private const double 時差DEF = 0.72;
	}
}