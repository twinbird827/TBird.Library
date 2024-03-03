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
using Tensorflow;

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
			var BinaryClassification6 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(6).Path, out DataViewSchema BinaryClassification6Schema);
			var BinaryClassification7 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(7).Path, out DataViewSchema BinaryClassification7Schema);
			var BinaryClassification8 = mlContext.Model.Load(AppSetting.Instance.GetBinaryClassificationResult(8).Path, out DataViewSchema BinaryClassification8Schema);
			var Regression = mlContext.Model.Load(AppSetting.Instance.GetRegressionResult(1).Path, out DataViewSchema RegressionSchema);

			await CreatePredictionFile("Best",
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification1),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification2),
				mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(BinaryClassification3),
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
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外3,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外2,
				PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction> 着外1,
				PredictionEngine<RegressionSource, RegressionPrediction> 着順1
			)
		{
			var path = Path.Combine("result", DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + tag + ".csv");

			FileUtil.BeforeCreate(path);
			using (var conn = CreateSQLiteControl())
			{
                (int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[] pays = new (int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[]
				{
                    // 単1の予想結果
					(100, "単1", (arr, payoutDetail, j) => Get三連単(payoutDetail,
                        arr.Where(x => x[j].GetInt32() == 1),
                        arr.Where(x => x[j].GetInt32() == 2),
                        arr.Where(x => x[j].GetInt32() == 3))
                    ),
                    // 単4の予想結果
                    (400, "単4", (arr, payoutDetail, j) => Get三連単(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4))
                    ),
                    // 単6の予想結果
                    (600, "単6", (arr, payoutDetail, j) => Get三連単(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5))
                    ),
                    // 複1の予想結果
                    (100, "複1", (arr, payoutDetail, j) => Get三連複(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 3),
                        arr.Where(x => x[j].GetInt32() <= 3),
                        arr.Where(x => x[j].GetInt32() <= 3))
                    ),
                    // 複2の予想結果
                    (200, "複2", (arr, payoutDetail, j) => Get三連複(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() <= 4))
                    ),
                    // 複3aの予想結果
                    (300, "複3a", (arr, payoutDetail, j) => Get三連複(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() <= 2),
                        arr.Where(x => x[j].GetInt32() <= 5))
                    ),
                    // 複3bの予想結果
                    (300, "複3b", (arr, payoutDetail, j) => Get三連複(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 4),
                        arr.Where(x => x[j].GetInt32() <= 4),
                        arr.Where(x => x[j].GetInt32() <= 4))
                    ),
                    // ワ1の予想結果
                    (100, "ワ1", (arr, payoutDetail, j) => Getワイド(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 2))
                    ),
                    // ワ3の予想結果
                    (300, "ワ3", (arr, payoutDetail, j) => Getワイド(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 3))
                    ),
                    // ワ6の予想結果
                    (600, "ワ6", (arr, payoutDetail, j) => Getワイド(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 4))
                    ),
                    // 連1の予想結果
                    (100, "連1", (arr, payoutDetail, j) => Get馬連(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 2))
                    ),
                    // 連3の予想結果
                    (300, "連3", (arr, payoutDetail, j) => Get馬連(payoutDetail,
                        arr.Where(x => x[j].GetInt32() <= 3))
                    )
				};

                var ﾗﾝｸ2 = await AppUtil.Getﾗﾝｸ2(conn);
				var 馬性 = await AppUtil.Get馬性(conn);
				var 調教場所 = await AppUtil.Get調教場所(conn);
				var 追切 = await AppUtil.Get追切(conn);

				var list = new List<List<object>>();
				var headers = new[] { "ﾚｰｽID", "ﾗﾝｸ1", "ﾚｰｽ名", "開催場所", "R", "枠番", "馬番", "馬名", "着順" }
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内1)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内2)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内3)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外1)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外2)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外3)))
					.Concat(RegressionPrediction.GetHeaders(nameof(着順1)))
					.Concat(new[] { "加1a", "加1b", "加2a", "加2b", "全a", "全b" })
					.ToArray();

				// 各列のﾍｯﾀﾞを挿入
				list.Add(headers
					.Concat(headers.Skip(9).Select(x => $"{x}_予想"))
					.Concat(headers.Skip(9).SelectMany(x => pays.Select(p => $"{x}_{p.head}")))
					.Select(x => (object)x)
					.ToList()
				);
				var raceids = GetRaceIds().ToArray();

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = raceids.Length;

				var iHeaders = 0;
				var iBinaries1 = 0;
				var iBinaries2 = 0;
				var iRegressions = 0;
				var iScores = 0;

				foreach (var raceid in raceids)
				{
					await conn.BeginTransaction();

					// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
					var racearr = await GetRaces2(raceid);
					if (!racearr.Any()) continue;

					// 産駒成績の更新
					await RefreshSanku(conn, false, racearr.Select(x => x["馬ID"]));

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
					foreach (var m in await CreateRaceModel(conn, raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切))
					{
						// 不要なﾃﾞｰﾀ
						var drops = new[] { "着順", "単勝", "人気", "ﾗﾝｸ2" };

						var tmp = new List<object>();
						var src = racearr.First(x => x["馬ID"].GetSingle() == (float)m["馬ID"]);

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

						var binaries1 = Arr(以内1, 以内2, 以内3)
							.Select(x => x.Predict(binaryClassificationSource))
							.ToArray();
						tmp.AddRange(binaries1);

						iBinaries1 = binaries1.Length;

						var binaries2 = Arr(着外1, 着外2, 着外3)
                            .Select(x => x.Predict(binaryClassificationSource))
                            .Select(x => { x.Score = x.Score * -1; return x; })
                            .ToArray();
						tmp.AddRange(binaries2);

						iBinaries2 = binaries2.Length;

						var regressions = Arr(着順1)
							.Select(x => x.Predict(regressionSource))
							.ToArray();
						tmp.AddRange(regressions);

						iRegressions = regressions.Length;

						var scores = Arr(
							// 合計値1
							binaries1.Concat(binaries2).Sum(x => x.GetScore1()),
							binaries1.Concat(binaries2).Sum(x => x.GetScore2()),
							// 合計値2
							Enumerable.Range(0, 3).Sum(i => Math.Max(binaries1[i].GetScore1(), binaries2[i].GetScore1())),
							Enumerable.Range(0, 3).Sum(i => Math.Max(binaries1[i].GetScore1(), binaries2[i].GetScore2())),
							// 着順付き
							binaries1.Concat(binaries2).Sum(x => x.GetScore1()) + regressions.Sum(x => 1 / x.Score * 16),
							binaries1.Concat(binaries2).Sum(x => x.GetScore2()) + regressions.Sum(x => 1 / x.Score * 16)
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
							if ((iHeaders + iBinaries1 + iBinaries2) <= j && j <= (iHeaders + iBinaries1 + iBinaries2))
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

						// 支払情報を出力
						var payoutDetail = await GetPayout(raceid);

                        for (var j = scoremaxlen; j < scoremaxlen + (scoremaxlen - iHeaders); j++)
						{
							arr.First().AddRange(pays.Select(x => x.func(arr, payoutDetail, j)));
						}

						list.AddRange(arr);

					}

					// 挿入したﾃﾞｰﾀは確定情報じゃないのでﾛｰﾙﾊﾞｯｸする
					conn.Rollback();

					AddLog($"End Step4 Race: {raceid}");

					Progress.Value += 1;
				}

                var payouts = pays.Select(x => x.pay).ToArray();
                var retidx = payouts.Length;
                var result1 = Enumerable.Repeat("", iHeaders + (iBinaries1 + iBinaries2 + iRegressions + iScores) * 2).OfType<object>()
					.Concat(Enumerable.Range(iHeaders + (iBinaries1 + iBinaries2 + iRegressions + iScores) * 2, (iBinaries1 + iBinaries2 + iRegressions + iScores) * retidx)
					.Select(i => (double)list.Where(x => i < x.Count && 0 < x[i].GetInt32()).Count() / (double)list.Where(x => i < x.Count).Count()).OfType<object>())
					.ToList();

				var result2 = Enumerable.Repeat("", iHeaders + (iBinaries1 + iBinaries2 + iRegressions + iScores) * 2).OfType<object>()
					.Concat(Enumerable.Range(iHeaders + (iBinaries1 + iBinaries2 + iRegressions + iScores) * 2, (iBinaries1 + iBinaries2 + iRegressions + iScores) * retidx)
						.Select((i, idx) => list.Where(x => i < x.Count).Sum(x => x[i].GetDouble()) / list.Where(x => i < x.Count).Sum(x => payouts[(idx % retidx)]))
						.OfType<object>()
					).ToList();

				list.add(result1);
				list.add(result2);

				// ﾌｧｲﾙ書き込み
				await File.AppendAllLinesAsync(path, list.Select(x => x.GetString(",")), Encoding.GetEncoding("Shift_JIS")); ;
			}

		}

		private object Get三連単(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2, IEnumerable<List<object>> arr3)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());
			var karr = arr3.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j => karr.Where(k => k != i && j != k).Select(k => $"{i}-{j}-{k}"))).ToArray();

			return payoutDetail.ContainsKey("三連単")
				? Arr(0).Concat(payoutDetail["三連単"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum()
				: 0;
		}

		private object Get三連複(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2, IEnumerable<List<object>> arr3)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());
			var karr = arr3.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j => karr.Where(k => k != i && j != k).SelectMany(k =>
			{
				return new[]
				{
					$"{i}-{j}-{k}",
					$"{i}-{k}-{j}",
					$"{j}-{i}-{k}",
					$"{j}-{k}-{i}",
					$"{k}-{j}-{i}",
					$"{k}-{i}-{j}",
				};
			}))).ToArray();

			return payoutDetail.ContainsKey("三連複")
				? Arr(0).Concat(payoutDetail["三連複"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum()
				: 0;
		}

		private object Getワイド(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr1.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j =>
			{
				return new[]
				{
					$"{i}-{j}",
					$"{j}-{i}",
				};
			})).ToArray();

			return payoutDetail.ContainsKey("ワイド")
				? Arr(0).Concat(payoutDetail["ワイド"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum()
				: 0;
		}

		private object Get馬連(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr1.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j =>
			{
				return new[]
				{
					$"{i}-{j}",
					$"{j}-{i}",
				};
			})).ToArray();

			return payoutDetail.ContainsKey("馬連")
				? Arr(0).Concat(payoutDetail["馬連"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum()
				: 0;
		}

		private object Get馬単(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).Select(j => $"{i}-{j}")).ToArray();

			return payoutDetail.ContainsKey("馬単")
				? Arr(0).Concat(payoutDetail["馬単"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum()
				: 0;
		}

	}
}