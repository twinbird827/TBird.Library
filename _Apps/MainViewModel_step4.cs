using AngleSharp.Common;
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
using TBird.Wpf;

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

		public ComboboxViewModel S4Dates
		{
			get => _S4Dates;
			set => SetProperty(ref _S4Dates, value);
		}
		private ComboboxViewModel _S4Dates = new(Enumerable.Empty<ComboboxItemModel>());

		public IRelayCommand S4UPDATELIST => RelayCommand.Create(async _ =>
		{
			var dates = await Enumerable.Range(-1, 3)
				.Select(i => DateTime.Now.AddMonths(i))
				.Select(x => NetkeibaGetter.GetKaisaiDate(x.Year, x.Month))
				.WhenAll()
				.RunAsync(x => x.SelectMany(y => y));
			WpfUtil.ExecuteOnUI(() =>
			{
				S4Dates.Items.Clear();
				S4Dates.Items.AddRange(dates.Select(x => new ComboboxItemModel(x, x)));
			});
		});

		public IRelayCommand S4EXEC => RelayCommand.Create(async _ =>
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			var ranks = AppUtil.ﾗﾝｸ2Arr;

			try
			{
				using (var conn = AppUtil.CreateSQLiteControl())
				{
					var 馬性 = await AppUtil.Get馬性(conn);
					var 調教場所 = await AppUtil.Get調教場所(conn);
					var 追切 = await AppUtil.Get追切(conn);

					var bpf = AppUtil.OrderBys
						.SelectMany(i => Arr($"1-{i}", $"6-{i}"))
						.ToDictionary(i => i, i => ranks.ToDictionary(
							rank => rank,
							rank => AppSetting.Instance.GetBinaryClassificationResults(i, rank)
										.OrderByDescending(x => x.GetScore())
										.Take(4 * 3)
										.Select(x => new BinaryClassificationPredictionFactory(mlContext, x.Rank, x.Index, x))
										.ToArray()
						));
					var rpf = AppUtil.OrderBys
						.SelectMany(i => Arr($"{i}"))
						.ToDictionary(i => i, i => ranks.ToDictionary(
							rank => rank,
							rank => AppSetting.Instance.GetRegressionResults("1", rank)
										.OrderBy(x => Guid.NewGuid())
										.Take(3)
										.Select(x => new RegressionPredictionFactory(mlContext, x.Rank, x.Index, x))
										.ToArray()
						));

					var lists = AppUtil.OrderBys
						.ToDictionary(x => $"Best-{x}", x => new List<List<object>>());

					var headers = Arr("ﾚｰｽID", "ﾗﾝｸ1", "ﾚｰｽ名", "開催場所", "R", "枠番", "馬番", "馬名", "着順", "B1", "B1", "B1", "B1", "B1", "B1", "B1", "B1", "R1", "平均")
						.Select(x => (object)x)
						.ToList();

					// 各列のﾍｯﾀﾞを挿入
					lists.ForEach(x => x.Value.Add(headers));

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
						async Task<List<Dictionary<string, string>>> GetShutuba1()
						{
							//using (MessageService.Measure($"GetShutuba1:{raceid}"))
							{
								return await conn.GetRows<string>("SELECT * FROM t_shutuba WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
							}
						}
						async Task<List<Dictionary<string, string>>> GetShutuba2()
						{
							//using (MessageService.Measure($"GetShutuba2:{raceid}"))
							{
								var racearr = await NetkeibaGetter.GetRaceShutubas(raceid);

								if (!racearr.Any()) return racearr;

								// ﾚｰｽの開催日
								var racedat = racearr.Take(1).Select(x => DateTime.Parse(x["開催日"])).First();

								// 着順情報取得
								var tyaku = DateTime.Now < racedat.AddHours(17)
									? new Dictionary<string, string>()
									: await NetkeibaGetter.GetTyakujun(raceid).RunAsync(tya => tya.ToDictionary(x => $"{x["枠番"]},{x["馬番"]}", x => x["着順"]));

								// 追切情報取得
								var oikiri = await NetkeibaGetter.GetOikiris(raceid);

								foreach (var row in racearr)
								{
									row["着順"] = tyaku.Get($"{row["枠番"]},{row["馬番"]}", string.Empty);

									NetkeibaGetter.SetOikiris(oikiri, row);

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
												return await NetkeibaGetter.GetBanushi(row["馬ID"]);
											}
										});
									row["馬主名"] = ban["馬主名"];
									row["馬主ID"] = ban["馬主ID"];
								}

								return racearr;
							}
						}

						var racetmp = await GetShutuba1();

						var racearr = racetmp.Any() ? racetmp : await GetShutuba2();

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
						//await RefreshKetto(conn, 馬IDs, false);

						//// 産駒成績の更新
						//await RefreshSanku(conn, 馬IDs, false);

						// ﾚｰｽ情報の初期化
						//await InitializeModelBase(conn);

						foreach (var o in AppUtil.OrderBys)
						{
							var 以内 = bpf[$"1-{o}"];
							var 着外 = bpf[$"6-{o}"];
							var 着順 = rpf[$"{o}"];

							var arr = new List<List<object>>();

							using (MessageService.Measure($"CreateRaceModel:{raceid}"))
							{
								// ﾓﾃﾞﾙﾃﾞｰﾀ作成
								//foreach (var m in await CreateRaceModel(conn, "v_shutuba", raceid, 馬性, 調教場所, 追切))
								//{
								//	var tmp = new List<object>();
								//	var src = racearr.First(x => x["馬ID"].GetInt64() == (long)m["馬ID"]);

								//	var features = AppUtil.CreateFeatures(m);

								//	// 共通ﾍｯﾀﾞ
								//	tmp.Add(src["ﾚｰｽID"]);
								//	tmp.Add(src["ﾗﾝｸ1"]);
								//	tmp.Add(src["ﾚｰｽ名"]);
								//	tmp.Add(src["開催場所"]);
								//	tmp.Add(src["ﾚｰｽID"].Right(2));
								//	tmp.Add(m["枠番"]);
								//	tmp.Add(m["馬番"]);
								//	tmp.Add(src["馬名"]);
								//	tmp.Add(src["着順"]);

								//	IEnumerable<(int i, float predict)> GetPredicts<TSrc, TDst>(PredictionFactory<TSrc, TDst>[] factories, int len) where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
								//	{
								//		var predicts = factories
								//			.Select(x => x.Predict(features, src["ﾚｰｽID"].GetInt64()))
								//			.OrderByDescending(x => x)
								//			.Run(x => x.Count() <= len ? x : x.Skip(1))
								//			.Run(x => x.Count() <= len ? x : x.Skip(1))
								//			.ToArray();

								//		for (var i = 0; i < predicts.Length; i++)
								//		{
								//			yield return (i % len, predicts[i]);
								//		}
								//	}

								//	Dictionary<int, IEnumerable<float>> ToPredictDictionary(IEnumerable<(int i, float predict)> values) => values
								//		.Select(x => x.i)
								//		.Distinct()
								//		.ToDictionary(i => i, i => values.Where(x => x.i == i).Select(x => x.predict));

								//	var 以内s = ToPredictDictionary(GetPredicts(以内[src["ﾗﾝｸ2"]], 4).ToArray());
								//	var 着外s = ToPredictDictionary(GetPredicts(着外[src["ﾗﾝｸ2"]], 4).ToArray());
								//	var 着順s = ToPredictDictionary(GetPredicts(着順[src["ﾗﾝｸ2"]], 1).ToArray());

								//	var binaries1 = 以内s.Values.Select(x => (object)x.Average()).ToArray();
								//	tmp.AddRange(binaries1);

								//	var binaries2 = 着外s.Values.Select(x => (object)x.Average()).ToArray();
								//	tmp.AddRange(binaries2);

								//	var regressions = 着順s.Values.Select(x => (object)x.Average()).ToArray();
								//	tmp.AddRange(regressions);

								//	var scores = Arr(
								//		// 合計値1
								//		binaries1.Concat(binaries2).Average(x => x.GetSingle())
								//	);
								//	tmp.AddRange(scores.Select(x => (object)x));
								//	arr.Add(tmp);
								//}
							}

							if (arr.Any()) lists[$"Best-{o}"].AddRange(arr);
						}

						AddLog($"End Step4 Race: {raceid}");

						Progress.Value += 1;
					}

					await lists.Select(async x =>
					{
						var list = x.Value;

						// ﾌｧｲﾙ書き込み
						var path = Path.Combine(AppSetting.Instance.NetkeibaResult, $"{DateTime.Now.ToString("yyyyMMddHHmmss")}_{x.Key}.csv");
						FileUtil.BeforeCreate(path);
						await File.AppendAllLinesAsync(path, list.Select(x => x.GetString(",")), Encoding.GetEncoding("Shift_JIS")); ;
					}).WhenAll();
				}
			}
			catch (Exception e)
			{
				MessageService.Info(e.ToString());
			}
		});
	}
}