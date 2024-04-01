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
using Microsoft.CodeAnalysis.CSharp.Syntax;

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

			var ranks = new[] { "RANK1", "RANK2", "RANK3", "RANK4", "RANK5" };

			await CreatePredictionFile("Best",
				ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 1)),
				ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 2)),
				ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 3)),
				ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 6)),
				ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 7)),
				ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 8)),
				ranks.ToDictionary(rank => rank, rank => new RegressionPredictionFactory(mlContext, rank, 1))
			).TryCatch();

			System.Diagnostics.Process.Start("EXPLORER.EXE", Path.GetFullPath("result"));
		});

		private IEnumerable<string> GetRaceIds()
		{
			var raceids = S4Text.Split('\n').Select(x => Regex.Match(x, @"\d{12}").Value).SelectMany(x => Enumerable.Range(1, 12).Select(i => x.Left(10) + i.ToString(2)));
			return raceids.OrderBy(s => s);
		}

		private async Task CreatePredictionFile(string tag,
				Dictionary<string, BinaryClassificationPredictionFactory> 以内1,
				Dictionary<string, BinaryClassificationPredictionFactory> 以内2,
				Dictionary<string, BinaryClassificationPredictionFactory> 以内3,
				Dictionary<string, BinaryClassificationPredictionFactory> 着外1,
				Dictionary<string, BinaryClassificationPredictionFactory> 着外2,
				Dictionary<string, BinaryClassificationPredictionFactory> 着外3,
				Dictionary<string, RegressionPredictionFactory> 着順1
			)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var pays = new (int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[]
				{
     //               // 単1の予想結果
					//(100, "単1", (arr, payoutDetail, j) => Get三連単(payoutDetail,
					//	arr.Where(x => x[j].GetInt32() == 1),
					//	arr.Where(x => x[j].GetInt32() == 2),
					//	arr.Where(x => x[j].GetInt32() == 3))
					//),
                    // 単4の予想結果
                    (400, "単4", (arr, payoutDetail, j) => Get三連単(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4))
					),
     //               // 単6の予想結果
     //               (600, "単6", (arr, payoutDetail, j) => Get三連単(payoutDetail,
					//	arr.Where(x => x[j].GetInt32() <= 2),
					//	arr.Where(x => x[j].GetInt32() <= 2),
					//	arr.Where(x => x[j].GetInt32() == 3 || x[j].GetInt32() == 4 || x[j].GetInt32() == 5))
					//),
     //               // 複1の予想結果
     //               (100, "複1", (arr, payoutDetail, j) => Get三連複(payoutDetail,
					//	arr.Where(x => x[j].GetInt32() <= 3),
					//	arr.Where(x => x[j].GetInt32() <= 3),
					//	arr.Where(x => x[j].GetInt32() <= 3))
					//),
                    // 複2の予想結果
                    (200, "複2", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複3の予想結果
                    (300, "複3", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 1),
						arr.Where(x => x[j].GetInt32() <= 4),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複4の予想結果
                    (400, "複4", (arr, payoutDetail, j) => Get三連複(payoutDetail,
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
     //               // ワ6の予想結果
     //               (600, "ワ6", (arr, payoutDetail, j) => Getワイド(payoutDetail,
					//	arr.Where(x => x[j].GetInt32() <= 4))
					//),
                    // 連1の予想結果
                    (100, "連1", (arr, payoutDetail, j) => Get馬連(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2))
					),
                    // 単勝1の予想結果
                    (100, "勝1", (arr, payoutDetail, j) => Get単勝(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 1))
					),
     //               // 連3の予想結果
     //               (300, "連3", (arr, payoutDetail, j) => Get馬連(payoutDetail,
					//	arr.Where(x => x[j].GetInt32() <= 3))
					//)
				};

				var ﾗﾝｸ2 = await AppUtil.Getﾗﾝｸ2(conn);
				var 馬性 = await AppUtil.Get馬性(conn);
				var 調教場所 = await AppUtil.Get調教場所(conn);
				var 追切 = await AppUtil.Get追切(conn);

				var lists = Arr(tag)
					.ToDictionary(x => x, x => new List<List<object>>());

				var headers = Arr("ﾚｰｽID", "ﾗﾝｸ1", "ﾚｰｽ名", "開催場所", "R", "枠番", "馬番", "馬名", "着順")
					.Concat(Arr(nameof(以内1), nameof(以内2), nameof(以内3), nameof(着外1), nameof(着外2), nameof(着外3), nameof(着順1)))
					.Concat(Arr("加1", "加2", "全1", "全2"))
					.ToArray();

				// 各列のﾍｯﾀﾞを挿入
				lists.ForEach(x =>
				{
					x.Value.Add(headers
						.Concat(headers.Skip(9).Select(x => $"{x}_予想"))
						.Concat(headers.Skip(9).SelectMany(x => pays.Select(p => $"{x}_{p.head}")))
						.Select(x => (object)x)
						.ToList()
					);
				});

				var raceids = GetRaceIds().ToArray();

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = raceids.Length;

				var iHeaders = 0;
				var iBinaries1 = 0;
				var iBinaries2 = 0;
				var iRegressions = 0;
				var iScores = 0;

				// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
				var racearrs = await raceids.Select(raceid => GetRaceShutubas(raceid).RunAsync(async arr =>
				{
					if (arr.Count == 0) return;

					// 着順情報取得
					var tyaku = await GetTyakujun(raceid);

					// 追切情報取得
					var oikiri = await GetOikiris(raceid);

					await arr.Select(async row =>
					{
						var tya = tyaku.FirstOrDefault(x => x["枠番"] == row["枠番"] && x["馬番"] == row["馬番"]);
						row["着順"] = tya != null ? tya["着順"] : string.Empty;

						var oik = oikiri.FirstOrDefault(x => x["枠番"] == row["枠番"] && x["馬番"] == row["馬番"]);
						row["一言"] = oik != null ? oik["一言"] : string.Empty;
						row["追切"] = oik != null ? oik["追切"] : string.Empty;

						var ban = await conn
							.GetRows<string>("SELECT 馬主名, 馬主ID FROM t_orig WHERE 馬ID = ? LIMIT 1", SQLiteUtil.CreateParameter(DbType.String, row["馬ID"]))
							.RunAsync(async tmp =>
							{
								if (0 < tmp.Count)
								{
									return tmp[0];
								}
								else
								{
									return await GetBanushi(row["馬ID"]);
								}
							});
						row["馬主名"] = ban["馬主名"];
						row["馬主ID"] = ban["馬主ID"];
					}).WhenAll();
				})).WhenAll();

				// 馬ID
				var 馬IDs = racearrs.SelectMany(arr => arr.Select(x => x["馬ID"]).Distinct());

				// 血統情報の作成
				await RefreshKetto(conn, 馬IDs);

				// 産駒成績の更新
				await RefreshSanku(conn, true, 馬IDs);

				foreach (var racearr in racearrs)
				{
					if (!racearr.Any()) continue;

					var raceid = racearr.First()["ﾚｰｽID"];

					await conn.BeginTransaction();

					// 元ﾃﾞｰﾀにﾚｰｽﾃﾞｰﾀがあれば削除してから取得したﾚｰｽﾃﾞｰﾀを挿入する
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

					var arr = new List<List<object>>();

					// ﾚｰｽ情報の初期化
					await InitializeModelBase(conn);

					// ﾓﾃﾞﾙﾃﾞｰﾀ作成
					foreach (var m in await CreateRaceModel(conn, raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切))
					{
						var tmp = new List<object>();
						var src = racearr.First(x => x["馬ID"].GetInt64() == (long)m["馬ID"]);

						var features = (AppSetting.Instance.Features ?? throw new ArgumentNullException()).Select(x => m[x].GetSingle()).ToArray();

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
							.Select(x => (object)x[src["ﾗﾝｸ2"]].Predict(features, src["ﾚｰｽID"].GetInt64()))
							.ToArray();
						tmp.AddRange(binaries1);

						iBinaries1 = binaries1.Length;

						var binaries2 = Arr(着外1, 着外2, 着外3)
							.Select(x => (object)x[src["ﾗﾝｸ2"]].Predict(features, src["ﾚｰｽID"].GetInt64()))
							.ToArray();
						tmp.AddRange(binaries2);

						iBinaries2 = binaries2.Length;

						var regressions = Arr(着順1)
							.Select(x => (object)x[src["ﾗﾝｸ2"]].Predict(features, src["ﾚｰｽID"].GetInt64()))
							.ToArray();
						tmp.AddRange(regressions);

						iRegressions = regressions.Length;

						var scores = Arr(
							// 合計値1
							binaries1.Concat(binaries2).Sum(x => x.GetSingle()),
							// 合計値2
							Enumerable.Range(0, 3).Sum(i => Math.Max(binaries1[i].GetSingle(), binaries2[i].GetSingle())),
							// 着順付き1
							binaries1.Concat(binaries2).Concat(regressions).Sum(x => x.GetSingle()),
							// 着順付き2
							Enumerable.Range(0, 3).Sum(i => Math.Max(binaries1[i].GetSingle(), binaries2[i].GetSingle())) + regressions.Sum(x => x.GetSingle())
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
							arr.OrderByDescending(x => x[j].GetDouble()).ForEach(x => x.Add(n++));
						}

						// 支払情報を出力
						var payoutDetail = await GetPayout(raceid);

						for (var j = scoremaxlen; j < scoremaxlen + (scoremaxlen - iHeaders); j++)
						{
							arr.First().AddRange(pays.Select(x => x.func(arr, payoutDetail, j)));
						}

						lists[tag].AddRange(arr);
					}

					conn.Rollback();

					AddLog($"End Step4 Race: {raceid}");

					Progress.Value += 1;
				}

				await lists.Select(async x =>
				{
					var list = x.Value;
					Func<int, IEnumerable<List<object>>> func = i => list.Where(x => i < x.Count);

					var payouts = pays.Select(x => x.pay).ToArray();
					var retidx = payouts.Length;
					var minidx = iHeaders + (iBinaries1 + iBinaries2 + iRegressions + iScores) * 2;
					var maxidx = (iBinaries1 + iBinaries2 + iRegressions + iScores) * retidx;

					// 勝率
					var result1 = Arr(
						Enumerable.Repeat("", minidx)
							.OfType<object>(),
						Enumerable.Range(minidx, maxidx)
							.Select(i => Calc(func(i).Count(x => 0 < x[i].GetInt32()), func(i).Count(), (x, y) => x / y))
							.OfType<object>()
					).SelectMany(obj => obj).ToList();

					// 回収率
					var result2 = Arr(
						Enumerable.Repeat("", minidx)
							.OfType<object>(),
						Enumerable.Range(minidx, maxidx)
							.Select((i, idx) => Calc(func(i).Average(x => x[i].GetDouble()), payouts[(idx % retidx)], (x, y) => x / y))
							.OfType<object>()
					).SelectMany(obj => obj).ToList();

					list.add(result1);
					list.add(result2);

					// ﾌｧｲﾙ書き込み
					var path = Path.Combine("result", $"{DateTime.Now.ToString("yyyyMMddHHmmss")}_{x.Key}.csv");
					FileUtil.BeforeCreate(path);
					await File.AppendAllLinesAsync(path, list.Select(x => x.GetString(",")), Encoding.GetEncoding("Shift_JIS")); ;
				}).WhenAll();
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

		private object Get単勝(Dictionary<string, string> payoutDetail, IEnumerable<List<object>> arr1)
		{
			var arr = arr1.Select(x => x[6].GetInt32().ToString());

			return payoutDetail.ContainsKey("単勝")
				? Arr(0).Concat(payoutDetail["単勝"].Split(";").Where(x => arr.Contains(x.Split(",")[0])).Select(x => x.Split(",")[1].GetInt32())).Sum()
				: 0;
		}

	}
}