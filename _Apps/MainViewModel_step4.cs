using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Core;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using TBird.DB;
using System.IO;
using System.Diagnostics.SymbolStore;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public string S4Text
		{
			get => _S4Text;
			set => SetProperty(ref _S4Text, value);
		}
		private string _S4Text = string.Empty;

		public IRelayCommand S4EXEC => RelayCommand.Create(async _ =>
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			var BinaryClassification1 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(1).Path, out DataViewSchema BinaryClassification1Schema);
			var BinaryClassification2 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(2).Path, out DataViewSchema BinaryClassification2Schema);
			var BinaryClassification3 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(3).Path, out DataViewSchema BinaryClassification3Schema);
			var BinaryClassification4 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(4).Path, out DataViewSchema BinaryClassification4Schema);
			var BinaryClassification5 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(5).Path, out DataViewSchema BinaryClassification5Schema);
			var BinaryClassification6 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(6).Path, out DataViewSchema BinaryClassification6Schema);
			var BinaryClassification7 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(7).Path, out DataViewSchema BinaryClassification7Schema);
			var BinaryClassification8 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(8).Path, out DataViewSchema BinaryClassification8Schema);
			var Regression = mlContext.Model.Load(AppSetting.Instance.GetRegressionResult(1).Path, out DataViewSchema RegressionSchema);

			await CreatePredictionFile("Best",
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification1),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification2),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification3),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification4),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification5),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification6),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification7),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification8),
				mlContext.Model.CreatePredictionEngine<RegressionSource, RegressionPrediction>(Regression)
			).TryCatch();

			System.Diagnostics.Process.Start("EXPLORER.EXE", Path.GetFullPath("result"));
		});

		private IEnumerable<string> GetRaceIds()
		{
			var raceids = S4Text.Split('\n').Select(x => Regex.Match(x, @"\d{12}").Value).SelectMany(x => Enumerable.Range(1, 12).Select(i => x.Left(10) + i.ToString(2)));
			return raceids.OrderBy(s => s);
		}

		private async Task CreatePredictionFile(string tag,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内1,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内2,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内3,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内4,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 以内5,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外3,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外5,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外7,
				PredictionEngine<RegressionSource, RegressionPrediction> 着順1
			)
		{
			var path = Path.Combine("result", DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + tag + ".csv");
			var payout = Path.Combine("result", DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + tag + "_payout.csv");

			FileUtil.BeforeCreate(path);

			using (var pw = new FileAppendWriter(payout, Encoding.GetEncoding("Shift_JIS")))
			using (var conn = CreateSQLiteControl())
			{
				Func<string, Task<IEnumerable<string>>> get_distinct = async x => (await conn.GetRows($"SELECT DISTINCT {x} FROM t_orig ORDER BY {x}")).Select(y => $"{y[x]}");

				//var ｸﾗｽ = new List<string>(await get_distinct("ｸﾗｽ"));
				var ﾗﾝｸ1 = new List<string>(await get_distinct("ﾗﾝｸ1"));
				var ﾗﾝｸ2 = new List<string>(await get_distinct("ﾗﾝｸ2"));
				//var 回り = new List<string>(await get_distinct("回り"));
				//var 天候 = new List<string>(await get_distinct("天候"));
				//var 馬場 = new List<string>(await get_distinct("馬場"));
				//var 馬場状態 = new List<string>(await get_distinct("馬場状態"));
				var 馬性 = new List<string>(await get_distinct("馬性"));
				var 調教場所 = new List<string>(await get_distinct("調教場所"));
				var 一言 = new List<string>(await get_distinct("一言"));
				var 追切 = new List<string>(await get_distinct("追切"));

				var list = new List<string>();
				var headers = new[] { "ﾚｰｽID", "ﾗﾝｸ1", "ﾚｰｽ名", "開催場所", "R", "枠番", "馬番", "馬名", "着順" }
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内1)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内2)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内3)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内4)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内5)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外3)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外5)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外7)))
					.Concat(RegressionPrediction.GetHeaders(nameof(着順1)))
					.Concat(new[] { "ｽｺｱ1a", "ｽｺｱ2a", "ｽｺｱ3a", "ｽｺｱ4a", "ｽｺｱ1b", "ｽｺｱ2b", "ｽｺｱ3b", "ｽｺｱ4b" })
					.ToArray();

				await pw.WriteLineAsync("ﾚｰｽID,三連複,三連単");

				// 各列のﾍｯﾀﾞを挿入
				list.Add(headers
					.Concat(headers.Skip(9).Select(x => $"{x}_予想"))
					.Concat(headers.Skip(9).SelectMany(x => new[] { $"{x}_単6", $"{x}_単10", $"{x}_単15", $"{x}_複3", $"{x}_複7", /*$"{x}_複4",*/ $"{x}_複10" }))
					.GetString(",")
				);
				var raceids = GetRaceIds().ToArray();

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = raceids.Length;

				foreach (var raceid in raceids)
				{
					await conn.BeginTransaction();

					// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
					var racearr = await GetRaces2(raceid);
					if (!racearr.Any()) continue;

					// 三連単と三連複の支払情報を出力
					var payoutDetail = await GetPayout(raceid);
					await pw.WriteLineAsync(payoutDetail.Keys.Select(key => payoutDetail[key]).GetString(","));

					// 元ﾃﾞｰﾀにﾚｰｽﾃﾞｰﾀがあれば削除してから取得したﾚｰｽﾃﾞｰﾀを挿入する
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

					var arr = new List<List<object>>();

					var iHeaders = 0;
					var iBinaries1 = 0;
					var iBinaries2 = 0;
					var iRegressions = 0;
					var iScores = 0;

					// ﾓﾃﾞﾙﾃﾞｰﾀ作成
					foreach (var m in await CreateRaceModel(conn, raceid, ﾗﾝｸ1, ﾗﾝｸ2, 馬性, 調教場所, 一言, 追切))
					{
						// 不要なﾃﾞｰﾀ
						var drops = new[] { "着順", "単勝", "人気" };

						var tmp = new List<object>();
						var src = racearr.First(x => x["馬ID"].GetDouble() == m["馬ID"]);

						if (src["ﾗﾝｸ1"] == "新馬") continue;

						var binaryClassificationSource = new BinaryClassificationSource()
						{
							Features = m.Keys.Where(x => !drops.Contains(x)).Select(x => (float)m[x]).ToArray()
						};
						var regressionSource = new RegressionSource()
						{
							Features = m.Keys.Where(x => !drops.Contains(x)).Select(x => (float)m[x]).ToArray()
						};

						// 共通ﾍｯﾀﾞ
						tmp.Add(src["ﾚｰｽID"]);
						tmp.Add(src["ﾗﾝｸ1"]);
						tmp.Add(src["ﾚｰｽ名"]);
						tmp.Add(src["開催場所"]);
						tmp.Add(src["ﾚｰｽID"].Right(2));
						tmp.Add(m["枠番"]);
						tmp.Add(m["馬番"]);
						tmp.Add(src["馬名"]);
						tmp.Add(src["着順"]);

						iHeaders = tmp.Count;

						var binaries1 = Arr(
							以内1.Predict(binaryClassificationSource),
							以内2.Predict(binaryClassificationSource),
							以内3.Predict(binaryClassificationSource),
							以内4.Predict(binaryClassificationSource),
							以内5.Predict(binaryClassificationSource)
						);
						tmp.AddRange(binaries1);

						iBinaries1 = binaries1.Length;

						var binaries2 = Arr(
							着外3.Predict(binaryClassificationSource),
							着外5.Predict(binaryClassificationSource),
							着外7.Predict(binaryClassificationSource)
						);
						tmp.AddRange(binaries2);

						iBinaries2 = binaries2.Length;

						var regressions = Arr(
							着順1.Predict(regressionSource)
						);
						tmp.AddRange(regressions);

						iRegressions = regressions.Length;

						Func<Func<BinaryClassificationPrediction, float>, Func<RegressionPrediction, float>, float> func_score = (bsum, rsum) =>
						{
							return binaries1.Sum(x => bsum(x)) - binaries2.Sum(x => bsum(x)) + regressions.Sum(x => rsum(x));
						};

						var scores = Arr(
							// 着順の重み低め＝α
							// 単純な加減算
							func_score(x => x.Score, x => 1 / x.Score * 200),
							// Probabilityをかけてみる
							func_score(x => x.Score * x.Probability, x => 1 / x.Score * 100),
							// Labelの有無で倍率計算する
							func_score(x => x.Score * (x.PredictedLabel ? 2 : 1), x => 1 / x.Score * 200),
							// Probabilityをかけてみる
							func_score(x => x.Score * x.Probability * (x.PredictedLabel ? 2 : 1), x => 1 / x.Score * 100),
							// 着順の重み高め＝β
							// 単純な加減算
							func_score(x => x.Score, x => 1 / x.Score * 400),
							// Probabilityをかけてみる
							func_score(x => x.Score * x.Probability, x => 1 / x.Score * 200),
							// Labelの有無で倍率計算する
							func_score(x => x.Score * (x.PredictedLabel ? 2 : 1), x => 1 / x.Score * 400),
							// Probabilityをかけてみる
							func_score(x => x.Score * x.Probability * (x.PredictedLabel ? 2 : 1), x => 1 / x.Score * 200)
						);
						tmp.AddRange(scores.Select(x => (object)x));

						iScores = scores.Length;

						arr.Add(tmp);
					}

					if (arr.Any())
					{
						var scoremaxlen = (iHeaders + iBinaries1 + iBinaries2 + iRegressions + iScores);
						for (var j = iHeaders; j < scoremaxlen; j++)
						{
							var n = 1;
							if ((iHeaders + iBinaries1) <= j && j <= (iHeaders + iBinaries1 + iBinaries2))
							{
								arr
									.OrderBy(x => x[j].ToString().GetDouble())
									.ThenBy(x => x[iHeaders + iBinaries1 + iBinaries2].ToString().GetDouble())
									.ForEach(x => x.Add(n++));
							}
							else
							{
								arr
									.OrderByDescending(x => x[j].ToString().GetDouble())
									.ThenBy(x => x[iHeaders + iBinaries1 + iBinaries2].ToString().GetDouble())
									.ForEach(x => x.Add(n++));
							}
						}

						for (var j = scoremaxlen; j < scoremaxlen + (scoremaxlen - iHeaders); j++)
						{
							{
								// 単6の予想結果
								var b1 = arr.Any(x => x[8].GetInt32() == 1 && (x[j].GetInt32() == 1 || x[j].GetInt32() == 2));
								var b2 = arr.Any(x => x[8].GetInt32() == 2 && (x[j].GetInt32() == 1 || x[j].GetInt32() == 2));
								var b3 = arr.Any(x => x[8].GetInt32() == 3 && (x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5));
								arr.First().Add(b1 && b2 && b3 ? 1 : 0);
							}
							{
								// 単10の予想結果
								var b1 = arr.Any(x => x[8].GetInt32() == 1 && (x[j].GetInt32() == 1 || x[j].GetInt32() == 2));
								var b2 = arr.Any(x => x[8].GetInt32() == 2 && (x[j].GetInt32() == 1 || x[j].GetInt32() == 2 || x[j].GetInt32() == 3));
								var b3 = arr.Any(x => x[8].GetInt32() == 3 && (x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5));
								arr.First().Add(b1 && b2 && b3 ? 1 : 0);
							}
							{
								// 単15の予想結果
								var b1 = arr.Any(x => x[8].GetInt32() == 1 && (x[j].GetInt32() == 1 || x[j].GetInt32() == 2 || x[j].GetInt32() == 3));
								var b2 = arr.Any(x => x[8].GetInt32() == 2 && (x[j].GetInt32() == 1 || x[j].GetInt32() == 2 || x[j].GetInt32() == 3));
								var b3 = arr.Any(x => x[8].GetInt32() == 3 && (x[j].GetInt32() == 1 || x[j].GetInt32() == 2 || x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5));
								arr.First().Add(b1 && b2 && b3 ? 1 : 0);
							}
							{
								// 複3の予想結果
								var b4 = arr.Any(x => x[8].GetInt32() <= 3 && x[j].GetInt32() == 1);
								var b5 = arr.Any(x => x[8].GetInt32() <= 3 && x[j].GetInt32() == 2);
								var b6 = arr.Any(x => x[8].GetInt32() <= 3 && (x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5));
								arr.First().Add(b4 && b5 && b6 ? 1 : 0);
							}
							{
								// 複7の予想結果
								var b7 = arr.Count(x => x[8].GetInt32() <= 3 && x[j].GetInt32() <= 3);
								var b8 = arr.Count(x => x[8].GetInt32() <= 3 && 3 < x[j].GetInt32() && x[j].GetInt32() <= 5);
								arr.First().Add(b7 == 3 || (b7 == 2 && b8 == 1) ? 1 : 0);
							}
							//{
							//	// 複4の予想結果
							//	var b7 = arr.Count(x => x[8].GetInt32() <= 3 && x[j].GetInt32() <= 4);
							//	arr.First().Add(b7 == 3 ? 1 : 0);
							//}
							{
								// 複10の予想結果
								var b7 = arr.Count(x => x[8].GetInt32() <= 3 && x[j].GetInt32() <= 5);
								arr.First().Add(b7 == 3 ? 1 : 0);
							}
						}

						list.AddRange(arr.Select(x => x.GetString(",")));

					}

					// 挿入したﾃﾞｰﾀは確定情報じゃないのでﾛｰﾙﾊﾞｯｸする
					conn.Rollback();

					AddLog($"End Step4 Race: {raceid}");

					Progress.Value += 1;
				}

				// ﾌｧｲﾙ書き込み
				await File.AppendAllLinesAsync(path, list, Encoding.GetEncoding("Shift_JIS")); ;
			}

		}
	}
}