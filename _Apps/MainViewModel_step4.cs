using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Web;
using TBird.Wpf;
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
			using var selenium = TBirdSeleniumFactory.GetDisposer();
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			var ranks = AppUtil.RankAges;

			try
			{
				await CreatePredictionFile("Best",
					ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 1)),
					ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 2)),
					ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 6)),
					ranks.ToDictionary(rank => rank, rank => new BinaryClassificationPredictionFactory(mlContext, rank, 7)),
					ranks.ToDictionary(rank => rank, rank => new RegressionPredictionFactory(mlContext, rank, 1))
				);
			}
			catch (Exception e)
			{
				MessageService.Info(e.ToString());
			}

			//System.Diagnostics.Process.Start("EXPLORER.EXE", Path.GetFullPath("result"));
		});

		private async Task CreatePredictionFile(string tag,
				Dictionary<string, BinaryClassificationPredictionFactory> 以内1,
				Dictionary<string, BinaryClassificationPredictionFactory> 以内2,
				Dictionary<string, BinaryClassificationPredictionFactory> 着外1,
				Dictionary<string, BinaryClassificationPredictionFactory> 着外2,
				Dictionary<string, RegressionPredictionFactory> 着順1
			)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var pays = Payment.GetDefaults();

				var ﾗﾝｸ2 = AppUtil.Getﾗﾝｸ2(conn);
				var 馬性 = await AppUtil.Get馬性(conn);
				var 調教場所 = await AppUtil.Get調教場所(conn);
				var 追切 = await AppUtil.Get追切(conn);

				var lists = Arr(tag)
					.ToDictionary(x => x, x => new List<List<object>>());

				var headers = Arr("ﾚｰｽID", "ﾗﾝｸ1", "ﾚｰｽ名", "開催場所", "R", "枠番", "馬番", "馬名", "着順")
					.Concat(Arr(nameof(以内1), nameof(以内2), nameof(着外1), nameof(着外2), nameof(着順1)))
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

				var iHeaders = 0;
				var iBinaries1 = 0;
				var iBinaries2 = 0;
				var iRegressions = 0;
				var iScores = 0;

				// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
				await conn.BeginTransaction();
				await conn.ExecuteNonQueryAsync("DELETE FROM t_shutuba WHERE 着順 IS NULL");
				await conn.ExecuteNonQueryAsync("DELETE FROM t_shutuba WHERE 着順 = ''");
				await conn.ExecuteNonQueryAsync("DELETE FROM t_shutuba WHERE 着順 = 0");
				conn.Commit();

				var racebases = S4Text.Split('\n')
					.Select(x => Regex.Match(x, @"\d{12}").Value.Left(10))
					.SelectMany(x => Enumerable.Range(1, 12).Select(i => $"{x}{i.ToString(2)}"))
					.OrderBy(x => x)
					.ToArray();

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = racebases.Length;

				foreach (var raceid in racebases)
				{
					var racearr = await raceid.Run(async id =>
					{
						return await conn.GetRows(
							"SELECT * FROM t_shutuba WHERE ﾚｰｽID = ?",
							SQLiteUtil.CreateParameter(DbType.String, id)
						).RunAsync(lst =>
							lst.Select(x => x.ToDictionary(y => y.Key, y => y.Value.Str())).ToList()
						).RunAsync(async lst => lst.Any() ? lst : await GetRaceShutubas(raceid).RunAsync(async arr =>
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

								arr.ForEach(row => SetOikiris(oikiri, row));

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
						}));
					});

					if (!racearr.Any()) continue;

					await conn.BeginTransaction();
					foreach (var x in racearr)
					{
						var sql = "REPLACE INTO t_shutuba (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}
					conn.Commit();

					// 馬ID
					var 馬IDs = racearr.Select(x => x["馬ID"]).Distinct();

					// 血統情報の作成
					await RefreshKetto(conn, 馬IDs, false);

					// 産駒成績の更新
					await RefreshSanku(conn, 馬IDs, false);

					// ﾚｰｽ情報の初期化
					await InitializeModelBase(conn);

					var arr = new List<List<object>>();

					// ﾓﾃﾞﾙﾃﾞｰﾀ作成
					foreach (var m in await CreateRaceModel(conn, "t_shutuba", raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切))
					{
						var tmp = new List<object>();
						var src = racearr.First(x => x["馬ID"].GetInt64() == (long)m["馬ID"]);

						var features = (AppSetting.Instance.Features ?? throw new ArgumentNullException()).Select(x => m.SINGLE(x)).ToArray();

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

						var binaries1 = Arr(以内1, 以内2)
							.Select(x => (object)x[src["ﾗﾝｸ1"]].Predict(features, src["ﾚｰｽID"].GetInt64()))
							.ToArray();
						tmp.AddRange(binaries1);

						iBinaries1 = binaries1.Length;

						var binaries2 = Arr(着外1, 着外2)
							.Select(x => (object)x[src["ﾗﾝｸ1"]].Predict(features, src["ﾚｰｽID"].GetInt64()))
							.ToArray();
						tmp.AddRange(binaries2);

						iBinaries2 = binaries2.Length;

						var regressions = Arr(着順1)
							.Select(x => (object)x[src["ﾗﾝｸ1"]].Predict(features, src["ﾚｰｽID"].GetInt64()))
							.ToArray();
						tmp.AddRange(regressions);

						iRegressions = regressions.Length;

						var scores = Arr(
							// 合計値1
							binaries1.Concat(binaries2).Sum(x => x.GetSingle()),
							// 合計値2
							Enumerable.Range(0, 2).Sum(i => Math.Max(binaries1[i].GetSingle(), binaries2[i].GetSingle())),
							// 着順付き1
							binaries1.Concat(binaries2).Concat(regressions).Sum(x => x.GetSingle()),
							// 着順付き2
							Enumerable.Range(0, 2).Sum(i => Math.Max(binaries1[i].GetSingle(), binaries2[i].GetSingle())) + regressions.Sum(x => x.GetSingle())
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

						await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_payout (ﾚｰｽID,key,val, PRIMARY KEY (ﾚｰｽID,key))");

						// 支払情報を出力
						var payoutDetail = await conn.GetRows("SELECT * FROM t_payout WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid.ToString())).RunAsync(async rows =>
						{
							if (rows.Any())
							{
								return rows.ToDictionary(x => $"{x["key"]}", x => $"{x["val"]}");
							}
							else
							{
								return await GetPayout(raceid.ToString());
							}
						});

						for (var j = scoremaxlen; j < scoremaxlen + (scoremaxlen - iHeaders); j++)
						{
							arr.First().AddRange(pays.Select(x => x.func(arr, payoutDetail, j)));
						}

						lists[tag].AddRange(arr);

						await conn.BeginTransaction();
						foreach (var x in payoutDetail)
						{
							await conn.ExecuteNonQueryAsync("REPLACE INTO t_payout (ﾚｰｽID,key,val) VALUES (?,?,?)",
								SQLiteUtil.CreateParameter(DbType.String, raceid.ToString()),
								SQLiteUtil.CreateParameter(DbType.String, x.Key),
								SQLiteUtil.CreateParameter(DbType.String, x.Value)
							);
						}
						conn.Commit();
					}

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
					var path = Path.Combine(AppSetting.Instance.NetkeibaResult, $"{DateTime.Now.ToString("yyyyMMddHHmmss")}_{x.Key}.csv");
					FileUtil.BeforeCreate(path);
					await File.AppendAllLinesAsync(path, list.Select(x => x.GetString(",")), Encoding.GetEncoding("Shift_JIS")); ;
				}).WhenAll();
			}
		}
	}
}