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
				var ﾗﾝｸ2 = await AppUtil.Getﾗﾝｸ2(conn);
				var 馬性 = await AppUtil.Get馬性(conn);
				var 調教場所 = await AppUtil.Get調教場所(conn);
				var 追切 = await AppUtil.Get追切(conn);

				var list = new List<string>();
				var headers = new[] { "ﾚｰｽID", "ﾗﾝｸ1", "ﾚｰｽ名", "開催場所", "R", "枠番", "馬番", "馬名", "着順" }
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内1)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内2)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(以内3)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外1)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外2)))
					.Concat(BinaryClassificationPrediction.GetHeaders(nameof(着外3)))
					.Concat(RegressionPrediction.GetHeaders(nameof(着順1)))
					.Concat(new[] { "高1a", "高1b", "高1c", "高1d", "高2a", "高2b", "高2c", "高2d", "高3a", "高3b", "高3c", "高3d", "加a", "加b", "加c", "加d", "全a", "全b", "全c", "全d" })
					.ToArray();

				// 各列のﾍｯﾀﾞを挿入
				list.Add(headers
					.Concat(headers.Skip(9).Select(x => $"{x}_予想"))
					.Concat(headers.Skip(9).SelectMany(x => new[] { $"{x}_単4", $"{x}_単6", $"{x}_複2", $"{x}_複3", $"{x}_ワ", $"{x}_馬連" }))
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
					foreach (var m in await CreateRaceModel(conn, raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切))
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
							以内3.Predict(binaryClassificationSource)
						);
						tmp.AddRange(binaries1);

						iBinaries1 = binaries1.Length;

						var binaries2 = Arr(
							着外1.Predict(binaryClassificationSource),
							着外2.Predict(binaryClassificationSource),
							着外3.Predict(binaryClassificationSource)
						).Select(x => { x.Score *= -1; return x; }).ToArray();
						tmp.AddRange(binaries2);

						iBinaries2 = binaries2.Length;

						var regressions = Arr(
							着順1.Predict(regressionSource)
						);
						tmp.AddRange(regressions);

						iRegressions = regressions.Length;

						var scores = Arr(
							// 以1着1の高い方
							Math.Max(binaries1[0].GetScore1(), binaries2[0].GetScore1()),
							Math.Max(binaries1[0].GetScore2(), binaries2[0].GetScore2()),
							Math.Max(binaries1[0].GetScore3(), binaries2[0].GetScore3()),
							Math.Max(binaries1[0].GetScore4(), binaries2[0].GetScore4()),
							// 以2着2の高い方
							Math.Max(binaries1[1].GetScore1(), binaries2[1].GetScore1()),
							Math.Max(binaries1[1].GetScore2(), binaries2[1].GetScore2()),
							Math.Max(binaries1[1].GetScore3(), binaries2[1].GetScore3()),
							Math.Max(binaries1[1].GetScore4(), binaries2[1].GetScore4()),
							// 以3着3の高い方
							Math.Max(binaries1[2].GetScore1(), binaries2[2].GetScore1()),
							Math.Max(binaries1[2].GetScore2(), binaries2[2].GetScore2()),
							Math.Max(binaries1[2].GetScore3(), binaries2[2].GetScore3()),
							Math.Max(binaries1[2].GetScore4(), binaries2[2].GetScore4()),
							// 合計値
							binaries1.Concat(binaries2).Sum(x => x.GetScore1()),
							binaries1.Concat(binaries2).Sum(x => x.GetScore2()),
							binaries1.Concat(binaries2).Sum(x => x.GetScore3()),
							binaries1.Concat(binaries2).Sum(x => x.GetScore4()),
							// 着順付き
							binaries1.Concat(binaries2).Sum(x => x.GetScore1()) + regressions.Sum(x => 1 / x.Score * 16),
							binaries1.Concat(binaries2).Sum(x => x.GetScore2()) + regressions.Sum(x => 1 / x.Score * 16),
							binaries1.Concat(binaries2).Sum(x => x.GetScore3()) + regressions.Sum(x => 1 / x.Score * 16),
							binaries1.Concat(binaries2).Sum(x => x.GetScore4()) + regressions.Sum(x => 1 / x.Score * 16)
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

						// 三連単と三連複の支払情報を出力
						var payoutDetail = await GetPayout(raceid);

						for (var j = scoremaxlen; j < scoremaxlen + (scoremaxlen - iHeaders); j++)
						{
							// 単4の予想結果
							arr.First().Add(Get三連単(payoutDetail,
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4))
							);
							// 単6の予想結果
							arr.First().Add(Get三連単(payoutDetail,
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5))
							);
							// 複2の予想結果
							arr.First().Add(Get三連複(payoutDetail,
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4))
							);
							// 複3の予想結果
							arr.First().Add(Get三連複(payoutDetail,
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() <= 2),
								arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5))
							);
							// ワイドの予想結果
							arr.First().Add(Getワイド(payoutDetail,
								arr.Where(x => x[j].GetInt32() <= 3)
							));
							// 馬連の予想結果
							arr.First().Add(Get馬連(payoutDetail,
								arr.Where(x => x[j].GetInt32() <= 2)
							));
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

		private object Get三連単(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2, IEnumerable<List<object>> arr3)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());
			var karr = arr3.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).SelectMany(j => karr.Where(k => k != i && j != k).Select(k => $"{i}-{j}-{k}"))).ToArray();

			return Arr(0).Concat(payoutDetail["三連単"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum();
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

			return Arr(0).Concat(payoutDetail["三連複"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum();
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

			return Arr(0).Concat(payoutDetail["ワイド"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum();
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

			return Arr(0).Concat(payoutDetail["馬連"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum();
		}

		private object Get馬単(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1, IEnumerable<List<object>> arr2)
		{
			var iarr = arr1.Select(x => x[6].GetInt32());
			var jarr = arr2.Select(x => x[6].GetInt32());

			var arr = iarr.SelectMany(i => jarr.Where(j => j != i).Select(j => $"{i}-{j}")).ToArray();

			return Arr(0).Concat(payoutDetail["馬単"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum();
		}

	}
}