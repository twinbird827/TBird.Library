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
			System.Diagnostics.Process.Start("EXPLORER.EXE", Path.GetFullPath("result"));

			// Initialize MLContext
			MLContext mlContext = new MLContext();

			foreach (var tag in new[] { "Best" }.Concat(TrainingTimeSecond.Select(x => x.ToString(4))))
			{
				var BinaryClassification1 = mlContext.Model.Load($@"model\BinaryClassification01_{tag}.zip", out DataViewSchema BinaryClassification1Schema);
				var BinaryClassification2 = mlContext.Model.Load($@"model\BinaryClassification02_{tag}.zip", out DataViewSchema BinaryClassification2Schema);
				var BinaryClassification3 = mlContext.Model.Load($@"model\BinaryClassification03_{tag}.zip", out DataViewSchema BinaryClassification3Schema);
				var BinaryClassification4 = mlContext.Model.Load($@"model\BinaryClassification04_{tag}.zip", out DataViewSchema BinaryClassification4Schema);
				var BinaryClassification5 = mlContext.Model.Load($@"model\BinaryClassification05_{tag}.zip", out DataViewSchema BinaryClassification5Schema);
				var BinaryClassification6 = mlContext.Model.Load($@"model\BinaryClassification06_{tag}.zip", out DataViewSchema BinaryClassification6Schema);
				var BinaryClassification7 = mlContext.Model.Load($@"model\BinaryClassification07_{tag}.zip", out DataViewSchema BinaryClassification7Schema);
				var BinaryClassification8 = mlContext.Model.Load($@"model\BinaryClassification08_{tag}.zip", out DataViewSchema BinaryClassification8Schema);
				var Regression = mlContext.Model.Load($@"model\Regression01_{tag}.zip", out DataViewSchema RegressionSchema);

				await CreatePredictionFile(tag,
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
			}
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

			FileUtil.BeforeCreate(path);

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
				var headers = new[] { "ﾗﾝｸ1", "開催場所", "ﾚｰｽID", "R", "ﾚｰｽ名", "枠番", "馬番", "馬名", "着順" }
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

				// 各列のﾍｯﾀﾞを挿入
				list.Add(headers
					.Concat(headers.Skip(9).Select(x => $"{x}_予想"))
					.Concat(headers.Skip(9).SelectMany(x => new[] { $"{x}_単6", $"{x}_単10", $"{x}_複3", $"{x}_複7" }))
					.GetString(",")
				);

				foreach (var raceid in GetRaceIds().ToArray())
				{

					await conn.BeginTransaction();

					// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
					var racearr = await GetRaces2(raceid);
					if (!racearr.Any()) continue;

					// 元ﾃﾞｰﾀにﾚｰｽﾃﾞｰﾀがあれば削除してから取得したﾚｰｽﾃﾞｰﾀを挿入する
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

					var arr = new List<List<object>>();

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

						tmp.Add(src["ﾗﾝｸ1"]);                               // 0
						tmp.Add(src["開催場所"]);                           // 1
						tmp.Add(src["ﾚｰｽID"]);                              // 2
						tmp.Add(src["ﾚｰｽID"].Right(2));                     // 3
						tmp.Add(src["ﾚｰｽ名"]);                              // 4
						tmp.Add(m["枠番"]);                                 // 5
						tmp.Add(m["馬番"]);                                 // 6
						tmp.Add(src["馬名"]);                               // 7
						tmp.Add(src["着順"]);                               // 8
						tmp.Add(以内1.Predict(binaryClassificationSource)); // 9
						tmp.Add(以内2.Predict(binaryClassificationSource)); // 10
						tmp.Add(以内3.Predict(binaryClassificationSource)); // 11
						tmp.Add(以内4.Predict(binaryClassificationSource)); // 12
						tmp.Add(以内5.Predict(binaryClassificationSource)); // 13
						tmp.Add(着外3.Predict(binaryClassificationSource)); // 14
						tmp.Add(着外5.Predict(binaryClassificationSource)); // 15
						tmp.Add(着外7.Predict(binaryClassificationSource)); // 16
						tmp.Add(着順1.Predict(regressionSource));            // 17
                                                                           // 18 単純に足すだけ
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.Sum(x => ((BinaryClassificationPrediction)x).Score) -
                            new[] { tmp[14], tmp[15], tmp[16] }.Sum(x => ((BinaryClassificationPrediction)x).Score) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 200)
                        );
                        // Probabilityをかけてみる							// 19
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability) -
                            new[] { tmp[14], tmp[15], tmp[16] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 100)
                        );
                        // 20 Labelの有無で倍率計算
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * (x.PredictedLabel ? 2 : 1)) -
                            new[] { tmp[14], tmp[15], tmp[16] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * (x.PredictedLabel ? 2 : 1)) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 200)
                        );
                        // Labelの有無で倍率計算 Probabilityをかけてみる							// 21
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability * (x.PredictedLabel ? 2 : 1)) -
                            new[] { tmp[14], tmp[15], tmp[16] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability * (x.PredictedLabel ? 2 : 1)) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 100)
                        );
                        // 18 単純に足すだけ
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.Sum(x => ((BinaryClassificationPrediction)x).Score) -
                            new[] { tmp[14], tmp[15], tmp[16] }.Sum(x => ((BinaryClassificationPrediction)x).Score) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 400)
                        );
                        // Probabilityをかけてみる							// 19
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability) -
                            new[] { tmp[14], tmp[15], tmp[16] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 200)
                        );
                        // 20 Labelの有無で倍率計算
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * (x.PredictedLabel ? 2 : 1)) -
                            new[] { tmp[14], tmp[15], tmp[16] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * (x.PredictedLabel ? 2 : 1)) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 400)
                        );
                        // Labelの有無で倍率計算 Probabilityをかけてみる							// 21
                        tmp.Add(
                            new[] { tmp[9], tmp[10], tmp[11], tmp[12], tmp[13] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability * (x.PredictedLabel ? 2 : 1)) -
                            new[] { tmp[14], tmp[15], tmp[16] }.OfType<BinaryClassificationPrediction>().Sum(x => x.Score * x.Probability * (x.PredictedLabel ? 2 : 1)) +
                            (1 / ((RegressionPrediction)tmp[17]).Score * 200)
                        );

                        arr.Add(tmp);
					}

					if (arr.Any())
					{
						for (var j = 9; j < 26; j++)
						{
							var n = 1;
							if (14 <= j && j <= 17)
							{
								arr
									.OrderBy(x => x[j].ToString().GetDouble())
									.ThenBy(x => x[17].ToString().GetDouble())
									.ForEach(x => x.Add(n++));
							}
							else
							{
								arr
									.OrderByDescending(x => x[j].ToString().GetDouble())
									.ThenBy(x => x[17].ToString().GetDouble())
									.ForEach(x => x.Add(n++));
							}
						}

						for (var j = 26; j < 26 + (26-9); j++)
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
						}

						list.AddRange(arr.Select(x => x.GetString(",")));

					}

					// 挿入したﾃﾞｰﾀは確定情報じゃないのでﾛｰﾙﾊﾞｯｸする
					conn.Rollback();
				}

				// ﾌｧｲﾙ書き込み
				await File.AppendAllLinesAsync(path, list, Encoding.GetEncoding("Shift_JIS")); ;
			}

		}
	}
}