using AngleSharp.Dom;
using AngleSharp.Text;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Web;
using TBird.Wpf;
using TorchSharp.Modules;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public CheckboxItemModel S2Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

		public IRelayCommand S2EXEC => RelayCommand.Create(async _ =>
		{
			using var selenium = TBirdSeleniumFactory.GetDisposer();
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var create = S2Overwrite.IsChecked || !await conn.ExistsColumn("t_model", "着順");

				var drops = await conn.ExistsColumn("t_model", "着順") && !create
					? await conn.GetRows(r => $"{r.GetValue(0)}", "SELECT DISTINCT ﾚｰｽID FROM t_model")
					: Enumerable.Empty<string>();

				var maxdate = await conn.ExecuteScalarAsync("SELECT MAX(開催日数) FROM t_orig");
				var mindate = await conn.ExecuteScalarAsync("SELECT MIN(開催日数) FROM t_orig");
				var target = maxdate.GetDouble().Subtract(mindate.GetDouble()).Multiply(0.3).Add(mindate.GetDouble());
				var racbase = await conn.GetRows(r => r.Get<string>(0),
					"SELECT DISTINCT ﾚｰｽID FROM t_orig WHERE 開催日数 >= ? AND 回り <> '障' AND t_orig.馬齢 > 2 ORDER BY ﾚｰｽID DESC",
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, target)
				);

				var rac = racbase
					.Where(id => !drops.Contains(id))
					.ToArray();

				var ﾗﾝｸ2 = await AppUtil.Getﾗﾝｸ2(conn);
				var 馬性 = await AppUtil.Get馬性(conn);
				var 調教場所 = await AppUtil.Get調教場所(conn);
				var 追切 = await AppUtil.Get追切(conn);

				// 血統情報の作成
				await RefreshKetto(conn);

				// 産駒成績の更新
				await RefreshSanku(conn);

				// ﾚｰｽ情報の初期化
				await InitializeModelBase(conn);

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = rac.Length;

				foreach (var raceid in rac)
				{
					MessageService.Debug($"ﾚｰｽID:開始:{raceid}");

					// ﾚｰｽ毎の纏まり
					var racarr = await CreateRaceModel(conn, "t_orig", raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切);
					var head1 = Arr("ﾚｰｽID", "開催日数", "枠番", "馬番", "着順", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID");
					var head2 = Arr("ﾚｰｽID", "開催日数", "着順", "単勝", "人気", "距離", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID");

					AppSetting.Instance.Features = null;

					if (create)
					{
						create = false;

						// ﾃｰﾌﾞﾙ作成
						await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
						await conn.ExecuteNonQueryAsync(Arr(
							"CREATE TABLE IF NOT EXISTS t_model (",
							head1.Select(x => $"{x} INTEGER").GetString(","),
							",単勝 REAL,Features BLOB, PRIMARY KEY (ﾚｰｽID, 馬番))").GetString(" "));

						await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_model_index00 ON t_model (開催日数, ﾗﾝｸ2, ﾚｰｽID)");
					}

					await conn.BeginTransaction();
					foreach (var ins in racarr)
					{
						AppSetting.Instance.Features = AppSetting.Instance.Features ?? ins.Keys.Where(x => !head2.Contains(x)).ToArray();

						var prms1 = head1.Select(x => SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins[x]));
						var prms2 = SQLiteUtil.CreateParameter(System.Data.DbType.Single, ins["単勝"]);
						var prms3 = SQLiteUtil.CreateParameter(System.Data.DbType.Binary,
							AppSetting.Instance.Features.SelectMany(x => BitConverter.GetBytes(ins[x].GetSingle())).ToArray()
						);

						await conn.ExecuteNonQueryAsync(
							$"REPLACE INTO t_model ({head1.GetString(",")},単勝,Features) VALUES ({Enumerable.Repeat("?", head1.Length).GetString(",")}, ?, ?)",
							prms1.Concat(Arr(prms2)).Concat(Arr(prms3)).ToArray()
						);
					}
					conn.Commit();

					AddLog($"Step5 Proccess ﾚｰｽID: {raceid}");

					Progress.Value += 1;
				}
				AppSetting.Instance.Save();

				MessageService.Info("Step5 Completed!!");
			}
		});
		private Dictionary<string, Dictionary<string, float>> DEF = new();
		private Dictionary<long, float> TOU = new();
		private Dictionary<object, Dictionary<string, object>> TOP = new();

		private async Task InitializeModelBase(SQLiteControl conn)
		{
			TOU = await conn.GetRows("SELECT ﾚｰｽID, COUNT(馬番) 頭数 FROM t_orig GROUP BY ﾚｰｽID").RunAsync(arr =>
			{
				return arr.ToDictionary(x => x["ﾚｰｽID"].GetInt64(), x => x["頭数"].GetSingle());
			});

			// ﾃﾞﾌｫﾙﾄ値の作製
			DEF.Clear();
			AppUtil.ﾗﾝｸ2.Keys.ForEach(async rank =>
			{
				var dic = await conn.GetRow<float>(Arr(
						$"SELECT",
						$"AVG(t_orig.着順) 着順,",
						$"AVG(t_orig.体重) 体重,",
						$"AVG(t_orig.単勝) 単勝,",
						$"AVG(t_orig.距離) 距離,",
						$"AVG(t_orig.上り) 上り,",
						$"AVG(t_orig.ﾀｲﾑ指数) ﾀｲﾑ指数,",
						$"AVG(t_orig.ﾀｲﾑ指数 - (SELECT MAX(w_tou.ﾀｲﾑ指数) FROM t_orig w_tou WHERE w_tou.ﾚｰｽID = t_orig.ﾚｰｽID AND w_tou.着順 = 1)) ﾀｲﾑ差,",
						$"AVG(t_orig.ﾀｲﾑ変換 - (SELECT MIN(w_tou.ﾀｲﾑ変換) FROM t_orig w_tou WHERE w_tou.ﾚｰｽID = t_orig.ﾚｰｽID AND w_tou.着順 = 1)) 勝時差,",
						$"0 賞金,",
						$"AVG(t_orig.斤量) 斤量",
						$"FROM t_orig",
						$"WHERE t_orig.回り <> '障' AND t_orig.馬齢 > 2 AND t_orig.ﾗﾝｸ1 = ?"
					).GetString(" "), SQLiteUtil.CreateParameter(DbType.String, rank == "新馬" ? "未勝利" : rank));

				dic["斤上"] = Get斤上(dic["上り"], dic["斤量"]);
				dic["時間"] = 16.237541F;
				dic["勝上差"] = dic["斤上"] - dic["斤上"] * 0.9F;
				dic["出走間隔"] = 40F;

				DEF.Add(rank, dic);
			});

			TOP = await conn.GetRows(Arr(
				$"SELECT ﾚｰｽID, MIN(CAST(ﾀｲﾑ変換 AS REAL)) ﾀｲﾑ変換, MIN(CAST(上り AS REAL)) 上り, MAX(CAST(斤量 AS REAL)) 斤量, MAX(ﾀｲﾑ指数) ﾀｲﾑ指数 FROM t_orig WHERE 着順 = 1 AND 回り <> '障' GROUP BY ﾚｰｽID"
			).GetString(" ")).RunAsync(val =>
			{
				return val.ToDictionary(
					x => x["ﾚｰｽID"],
					x => x.Where(x => x.Key != "ﾚｰｽID").ToDictionary(y => y.Key, y => y.Value)
				);
			});
		}

		private float GetSingle(IEnumerable<float> arr, float def, Func<IEnumerable<float>, float> func)
		{
			return arr.Where(x => !float.IsNaN(x)).Any() ? func(arr.Where(x => !float.IsNaN(x))) : def;
		}

		private float Median(IEnumerable<float> arr, float def)
		{
			return GetSingle(arr, def, ret => ret.Percentile(50));
		}

		private float Median(IEnumerable<Dictionary<string, object>> arr, string rank, string n)
		{
			return Median(arr, n, DEF[rank][n]);
		}

		private float Median(IEnumerable<Dictionary<string, object>> arr, string n, float def)
		{
			return Median(arr.Select(x => x[n].GetSingle()), def);
		}

		private float Std(IEnumerable<float> arr) => arr.Where(x => !float.IsNaN(x)).Run(xxx => 1 < xxx.Count() ? (float)xxx.StandardDeviation() : 0F);

		private float Var(IEnumerable<float> arr) => arr.Where(x => !float.IsNaN(x)).Run(xxx => 1 < xxx.Count() ? (float)xxx.Variance() : 0F);

		private async Task<IEnumerable<Dictionary<string, object>>> CreateRaceModel(SQLiteControl conn, string tablename, string raceid, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			// 同ﾚｰｽの平均を取りたいときに使用する
			var 同ﾚｰｽ = await conn.GetRows($"SELECT {tablename}.*, a.父ID 父ID, b.父ID 母父ID FROM {tablename} LEFT JOIN t_ketto a ON {tablename}.馬ID = a.馬ID LEFT JOIN t_ketto b ON a.母ID = b.馬ID WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(System.Data.DbType.String, raceid)
			);

			TOU[raceid.GetInt64()] = 同ﾚｰｽ.Count;

			// ﾚｰｽ毎の纏まり
			var racarr = await 同ﾚｰｽ.Select(src => ToModel(conn, src, ﾗﾝｸ2, 馬性, 調教場所, 追切).TryCatch()).WhenAll();

			var drops = Arr("距離", "調教場所", "枠番", "馬番", "馬ID", "着順", "単勝", "ﾚｰｽID", "開催日数", "ﾗﾝｸ1", "ﾗﾝｸ2"); ;
			var keys = racarr.First().Keys.Where(y => !drops.Contains(y)).ToArray();

			// 他の馬との比較
			racarr.ForEach(dic =>
			{
				keys.ForEach(key =>
				{
					try
					{
						var val = dic[key].GetSingle();
						var arr = racarr.Select(x => x[key].GetSingle()).Where(x => !float.IsNaN(x)).ToArray();
						var std = Std(arr);

						//dic[$"{key}A1"] = val - arr.Average();
						//dic[$"{key}A2"] = val - arr.Percentile(25);
						//dic[$"{key}A3"] = val - arr.Percentile(75);
						//dic[$"{key}A4"] = val - arr.Percentile(50);
						//dic[$"{key}A5"] = val - arr.Max();
						//dic[$"{key}A6"] = val - arr.Min();
						//dic[$"{key}A7"] = val * std;
						//dic[$"{key}B1"] = val == 0 ? 0F : arr.Average() / val * 100;
						//dic[$"{key}B2"] = val == 0 ? 0F : arr.Percentile(25) / val * 100;
						//dic[$"{key}B3"] = val == 0 ? 0F : arr.Percentile(75) / val * 100;
						//dic[$"{key}B4"] = val == 0 ? 0F : arr.Percentile(50) / val * 100;
						//dic[$"{key}B5"] = val == 0 ? 0F : arr.Sum() / val;
						//dic[$"{key}B6"] = val == 0 ? 0F : arr.Max() / val * 100;
						//dic[$"{key}B7"] = val == 0 ? 0F : arr.Min() / val * 100;

						//dic[$"{key}C1"] = val - arr.Percentile(10);
						dic[$"{key}C2"] = val - arr.Percentile(30);
						dic[$"{key}C3"] = val - arr.Percentile(50);
						dic[$"{key}C4"] = val - arr.Percentile(70);
						//dic[$"{key}C9"] = Arr(dic[$"{key}C2"], dic[$"{key}C3"], dic[$"{key}C4"]).Average(x => x.GetSingle());

						//dic[$"{key}C5"] = val - arr.Percentile(90);

						//dic[$"{key}D1"] = val - arr.Percentile(20);
						//dic[$"{key}D2"] = val - arr.Percentile(40);
						//dic[$"{key}D3"] = val - arr.Percentile(60);
						//dic[$"{key}D4"] = val - arr.Percentile(80);

					}
					catch
					{
						throw;
					}
				});
			});

			return racarr;
		}

		//private float Get追切時間(Dictionary<string, object> tmp, int j)
		//{
		//	var avg = OIK[tmp["追切場所"]][$"追切時間{j}"];
		//	var ksh = $"{tmp[$"追切騎手"]}" == "助手" ? 0.95F : 1.0F;
		//	var tsu = $"{tmp[$"追切強さ"]}" == "馬也" ? 0.95F : 1.0F;
		//	var bab = $"{tmp[$"追切馬場"]}" switch
		//	{
		//		"良" => 1.05F,
		//		"稍" => 1.00F,
		//		_ => 0.95F
		//	};
		//	var val = tmp[$"追切時間{j}"].GetSingle().Run(jik => jik == 0 ? avg + 1 : jik * ksh * tsu * bab);
		//	return val - avg;
		//}

		private async Task<Dictionary<string, object>> ToModel(SQLiteControl conn, Dictionary<string, object> src, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			var dic = new Dictionary<string, object>();
			var rnk = src["ﾗﾝｸ1"].Str();

			// 通過順変換ﾌｧﾝｸｼｮﾝ
			Func<object, double> func_tuka = v => v.Str().Split('-').Skip(1).Take(1).Select(x => x.GetDouble()).Average(TOU[src["ﾚｰｽID"].GetInt64()] / 2).Run(i =>
			{
				var tou = TOU[src["ﾚｰｽID"].GetInt64()];
				var x1 = tou / 4;
				var x2 = x1 * 2;
				var x3 = x1 * 3;
				return i <= x1 ? 0 : i <= x2 ? 2 : i <= x3 ? 4 : 6;
			});

			var 過去SQL = Arr(
				$" SELECT * FROM t_orig"
			).GetString(" ");
			var 馬情報 = await conn.GetRows(
					過去SQL + $" WHERE t_orig.馬ID = ? AND t_orig.開催日数 < ? AND t_orig.回り <> '障' ORDER BY t_orig.開催日数 DESC",
					SQLiteUtil.CreateParameter(DbType.String, src["馬ID"]),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"])
			).RunAsync(arr =>
			{
				return Arr(100, 1, 3, 5).Select(i => arr.Take(i).ToList()).ToArray();
			});

			// ﾍｯﾀﾞ情報
			dic["ﾚｰｽID"] = src["ﾚｰｽID"].GetInt64();
			dic["開催日数"] = src["開催日数"].GetInt64();
			dic["ﾗﾝｸ1"] = AppUtil.ﾗﾝｸ2.Keys.IndexOf(rnk);
			dic["ﾗﾝｸ2"] = ﾗﾝｸ2.IndexOf(src["ﾗﾝｸ2"]);

			// 予測したいﾃﾞｰﾀ
			dic["着順"] = src["着順"].GetInt64();
			dic["単勝"] = src["単勝"].GetSingle();

			// 馬毎に違う情報
			dic["枠番"] = src["枠番"].GetInt64();
			dic["馬番"] = src["馬番"].GetInt64();
			dic["馬ID"] = src["馬ID"].GetInt64();
			dic["馬性"] = 馬性.IndexOf(src["馬性"]);
			dic["馬齢"] = src["馬齢"].GetSingle();
			dic["斤量"] = src["斤量"].GetSingle();

			//dic["追切騎手"] = src["追切騎手"].Str() == "助手" ? 0 : 1;

			var ft = TOU[dic["ﾚｰｽID"].GetInt64()] / 3;
			//dic["追切騎手平"] = Median(馬情報[0].Select(x => x["追切騎手"].Str() == "助手" ? 0F : 1F), 0F);
			dic["枠"] = dic["馬番"].GetSingle().Run(x => x <= ft ? 0 : x <= ft * 2 ? 1 : 2);

			//Enumerable.Range(1, 5).ForEach(j =>
			//{
			//	dic[$"追切時間{j}"] = Get追切時間(src, j);
			//});

			//Enumerable.Range(1, 5).ForEach(j =>
			//{
			//	dic[$"追切基準{j}"] = src[$"追切基準{j}"].Str() switch
			//	{
			//		"TokeiColor01" => 2F,
			//		"TokeiColor02" => 1F,
			//		_ => 0F
			//	};
			//});
			//dic[$"追切基準S"] = Enumerable.Range(1, 5).Select(j => src[$"追切基準{j}"].Str() switch
			//{
			//	"TokeiColor01" => 10F,
			//	"TokeiColor02" => 5F,
			//	_ => 0F
			//}).Sum();
			dic[$"追切基準S"] = Arr(1, 2, 3, 4, 5).Select(j =>
			{
				if (float.TryParse(src[$"追切時間{j}"].Str(), out float tmp))
				{
					return src[$"追切基準{j}"].Str() switch
					{
						"TokeiColor01" => 10F,
						"TokeiColor02" => 5F,
						_ => 0F
					};
				}
				else
				{
					return 2.5F;
				}
			}).Sum();
			dic[$"追切基準A"] = dic[$"追切基準S"].GetSingle() / 5F * 100F;

			//dic["体重"] = Median(馬情報[0], "体重");
			//dic["斤量割"] = dic["斤量"].GetSingle() / dic["体重"].GetSingle() * 100F;
			dic["調教場所"] = 調教場所.IndexOf(src["調教場所"]);
			dic["追切評価"] = 追切.IndexOf(src["追切評価"]);

			Func<List<Dictionary<string, object>>, string[], int[], int[], IEnumerable<List<Dictionary<string, object>>>> CREATE情報 = (arr, tgtarr, takarr, kyoarr) =>
			{
				var l0 = kyoarr.Select(kyo => arr.Where(x => Math.Abs(x["距離"].GetSingle() - src["距離"].GetSingle()) <= kyo)).ToArray();
				var l1 = l0.Concat(l0.SelectMany(l => tgtarr.Select(tgt => l.Where(x => x[tgt].Str() == src[tgt].Str())))).ToArray();
				var l2 = l1.SelectMany(l => takarr.Select(tak => l.Take(tak)));
				return l2.Select(l => l.ToList()).ToArray();
			};

			Action<string, List<Dictionary<string, object>>, int> ACTION情報 = (key, arr, i) =>
			{
				var KEY = $"{key}{i.ToString(2)}";
				var X = arr;
				Func<Dictionary<string, object>, float> func_kyori = tgt => Arr(tgt, src).Select(y => y["距離"].Single()).Run(arr => arr.Min() / arr.Max());

                dic[$"{KEY}距離"] = Median(X.Select(func_kyori), 0.75F);
                dic[$"{KEY}着順A"] = Median(X, rnk, "着順");
                dic[$"{KEY}着順B"] = Median(X.Select(x => GET着順(x, true)), 1F);
                dic[$"{KEY}着順C"] = Median(X.Select(x => GET着順(x, false)), 1F);
                dic[$"{KEY}着順D"] = Median(X.Select(x => GET着順(x, true) / func_kyori(x)), 1F);
                dic[$"{KEY}着順E"] = Median(X.Select(x => GET着順(x, false) / func_kyori(x)), 1F);
                dic[$"{KEY}ﾀｲﾑ差"] = Median(X.Select(x => x["ﾀｲﾑ指数"].GetSingle() - TOP[x["ﾚｰｽID"]]["ﾀｲﾑ指数"].GetSingle()), DEF[rnk]["ﾀｲﾑ差"]);
				dic[$"{KEY}勝時差"] = Median(X.Select(x => x["ﾀｲﾑ変換"].GetSingle() - TOP[x["ﾚｰｽID"]]["ﾀｲﾑ変換"].GetSingle()), DEF[rnk]["勝時差"]);
                dic[$"{KEY}ﾀｲﾑ指数"] = Median(X, rnk, "ﾀｲﾑ指数");
            };

			// 出遅れ率
			dic[$"出遅れ率"] = Calc(馬情報[0].Count(x => x["備考"].Str().Contains("出遅")), 馬情報[0].Count, (c1, c2) => c2 == 0 ? 0 : c1 / c2).GetSingle() * 100F;

			馬情報[0].Run(arr => CREATE情報(arr, Arr("馬場状態"), Arr(1, 3, 5, 10), Arr(3000))).ForEach((arr, i) =>
			{
				ACTION情報("馬ID", arr, i);
			});

			Arr("産父").ForEach(async key =>
			{
				var 情報 = await conn.GetRows(
					$"SELECT * FROM t_orig WHERE 馬ID IN (SELECT 父ID FROM t_ketto WHERE 馬ID = ?) AND 開催日数 < ? AND 開催日数 > ? AND 回り <> '障' ORDER BY 開催日数 DESC",
					SQLiteUtil.CreateParameter(DbType.String, src["父ID"]),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64()),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 365)
				).RunAsync(arr => CREATE情報(arr, Arr("馬場", "馬場状態"), Arr(500), Arr(3000)));

				情報.ForEach((arr, i) => ACTION情報(key, arr, i));
			});

			Arr("産母父").ForEach(async key =>
			{
				var 情報 = await conn.GetRows(
					$"SELECT * FROM t_orig WHERE 馬ID IN (SELECT b.父ID FROM t_ketto a INNER JOIN t_ketto b ON a.母ID = b.馬ID WHERE a.馬ID = ?) AND 開催日数 < ? AND 開催日数 > ? AND 回り <> '障' ORDER BY 開催日数 DESC",
					SQLiteUtil.CreateParameter(DbType.String, src["母父ID"]),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64()),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 365)
				).RunAsync(arr => CREATE情報(arr, Arr("馬場", "馬場状態"), Arr(500), Arr(3000)));

				情報.ForEach((arr, i) => ACTION情報(key, arr, i));
			});

			Arr("騎手ID").ForEach(async key =>
			{
				var 情報 = await conn.GetRows(
					過去SQL + $" WHERE t_orig.{key} = ? AND t_orig.開催日数 < ? AND t_orig.開催日数 > ? AND t_orig.回り <> '障' ORDER BY t_orig.開催日数 DESC",
					SQLiteUtil.CreateParameter(DbType.String, src[key]),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64()),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 365)
				).RunAsync(arr => CREATE情報(arr, Arr("開催場所", "馬場", "馬場状態"), Arr(500), Arr(3000)));

				情報.ForEach((arr, i) => ACTION情報(key, arr, i));
			});

			Arr("調教師ID", "馬主ID").ForEach(async key =>
			{
				var 情報 = await conn.GetRows(
					過去SQL + $" WHERE t_orig.{key} = ? AND t_orig.開催日数 < ? AND t_orig.開催日数 > ? AND t_orig.回り <> '障' ORDER BY t_orig.開催日数 DESC",
					SQLiteUtil.CreateParameter(DbType.String, src[key]),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64()),
					SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 365)
				).RunAsync(arr => CREATE情報(arr, new string[] { }, Arr(500), Arr(3000)));

				情報.ForEach((arr, i) => ACTION情報(key, arr, i));
			});

			dic["距離"] = src["距離"].GetSingle();

			馬情報.ForEach((arr, i) =>
			{
				//// 着順A
				//dic[$"着順A{i}"] = Median(arr, "着順", DEF["着順"]);

				//// 着順B
				//dic[$"着順B{i}"] = Median(arr.Select(x => GET着順(x)), 0F);

				// 斤量
				dic[$"斤量{i}"] = Median(arr, rnk, "斤量");

				// 賞金
				dic[$"賞金{i}"] = Median(arr, rnk, "賞金");

				//// ﾀｲﾑ指数
				//dic[$"ﾀｲﾑ指数{i}"] = Median(arr, rnk, "ﾀｲﾑ指数");

				//// ﾀｲﾑ差
				//dic[$"ﾀｲﾑ差{i}"] = Median(arr.Select(x => x["ﾀｲﾑ指数"].GetSingle() - TOP[x["ﾚｰｽID"]]["ﾀｲﾑ指数"].GetSingle()), DEF["ﾀｲﾑ差"]);

				//// 勝時差
				//dic[$"勝時差{i}"] = Median(arr.Select(x => x["ﾀｲﾑ変換"].GetSingle() - TOP[x["ﾚｰｽID"]]["ﾀｲﾑ変換"].GetSingle()), DEF["勝時差"]);

				//// 得意距離、及び今回のﾚｰｽ距離との差
				//dic[$"距離得{i}"] = Median(arr, rnk, "距離");
				//dic[$"距離差{i}"] = dic["距離"].GetSingle() - Median(arr, rnk, "距離");

				// 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
				dic[$"通過{i}"] = Median(arr.Select(x => (float)func_tuka(x["通過"])), TOU[dic["ﾚｰｽID"].GetInt64()] / 2);

				// 上り×斤量
				//dic[$"上り{i}"] = Median(arr, "上り");
				dic[$"斤上{i}"] = Median(arr.Select(x => Get斤上(x)), DEF[rnk]["斤上"]);
				dic[$"斤時{i}"] = dic[$"斤上{i}"].GetSingle() * (dic[$"通過{i}"].GetSingle() switch
				{
					< 2 => 0.95F,
					< 4 => 1.00F,
					_ => 1.05F
				});

				// 1着との差(上り×斤量)
				dic[$"勝上差{i}"] = Median(arr.Select(x => Get斤上(x) - Get斤上(TOP[x["ﾚｰｽID"]])), DEF[rnk]["勝上差"]);
			});

			// 出走間隔
			dic[$"出走間隔"] = GetSingle(馬情報[0].Select(x => x["開催日数"].GetSingle()), DEF[rnk]["出走間隔"], x => src["開催日数"].GetSingle() - x.Max());

			using (await Locker.LockAsync(Lock))
			{
				var 産駒馬場 = $"{src["馬場"]}" == "芝" ? "芝" : "ダ";
				var 産駒情報 = await conn.GetRows(Arr(
						$"WITH RECURSIVE",
						$"w_titi(父ID, LAYER) AS (",
						$"    VALUES('{src["馬ID"]}', 0)",
						$"    UNION ALL",
						$"    SELECT t.父ID, LAYER+1 FROM t_ketto t, w_titi WHERE t.馬ID = w_titi.父ID",
						$"    UNION ALL",
						$"    SELECT c.父ID, LAYER+1 FROM t_ketto t, t_ketto c, w_titi WHERE t.馬ID = w_titi.父ID AND t.母ID = c.馬ID",
						$")",
						$"SELECT",
						$"    w_titi.LAYER,",
						$"    t_sanku.順位,",
						$"    t_sanku.出走頭数,",
						$"    t_sanku.勝馬頭数,",
						$"    t_sanku.勝馬頭数 / t_sanku.出走頭数 AS 勝馬率,",
						$"    t_sanku.出走回数,",
						$"    t_sanku.勝利回数,",
						$"    t_sanku.勝利回数 / t_sanku.出走回数 AS 勝利率,",
						$"    t_sanku.重出,",
						$"    t_sanku.重勝,",
						$"    IFNULL(t_sanku.重勝 / t_sanku.重出, 0) 重勝率,",
						$"    t_sanku.特出,",
						$"    t_sanku.特勝,",
						$"    IFNULL(t_sanku.特勝 / t_sanku.特出, 0) 特勝率,",
						$"    t_sanku.平出,",
						$"    t_sanku.平勝,",
						$"    IFNULL(t_sanku.平勝 / t_sanku.平出, 0) 平勝率,",
						$"    t_sanku.{産駒馬場}出 場出,",
						$"    t_sanku.{産駒馬場}勝 場勝,",
						$"    IFNULL(t_sanku.{産駒馬場}勝 / t_sanku.{産駒馬場}出, 0) 場勝率,",
						$"    t_sanku.EI,",
						$"    t_sanku.賞金 産賞金,",
						$"    {dic["距離"]} - t_sanku.{産駒馬場}距 距離差,",
						$"    t_sanku.年度 年度",
						$"FROM w_titi, t_sanku WHERE w_titi.父ID = t_sanku.馬ID AND CAST(t_sanku.年度 AS INTEGER) < {src["ﾚｰｽID"].Str().Left(4)}"
					).GetString(" ")
				).RunAsync(arr =>
				{
					return Arr(arr, arr.Where(x => x["年度"].GetInt64() > (src["ﾚｰｽID"].Str().Left(4).GetInt64() - 4L)))
						.SelectMany(lst => Enumerable.Range(1, 1).Select(i => arr.Where(x => x["LAYER"].GetInt32() <= i * 2).ToList()))
						.ToArray();
				});
				産駒情報.Skip(1).ForEach((arr, i) =>
				{
					//dic[$"産駒出走頭数{i}"] = Median(arr, "出走頭数");
					//dic[$"産駒勝馬頭数{i}"] = Median(arr, "勝馬頭数");
					dic[$"産駒勝馬率{i}"] = Median(arr, "勝馬率", 0F) * 100F;
					//dic[$"産駒出走回数{i}"] = Median(arr, "出走回数");
					//dic[$"産駒勝利回数{i}"] = Median(arr, "勝利回数");
					dic[$"産駒勝利率{i}"] = Median(arr, "勝利率", 0F) * 100F;
					//dic[$"産駒重出{i}"] = Median(arr, "重出");
					//dic[$"産駒重勝{i}"] = Median(arr, "重勝");
					dic[$"産駒重勝率{i}"] = Median(arr, "重勝率", 0F) * 100F;
					//dic[$"産駒特出{i}"] = Median(arr, "特出");
					//dic[$"産駒特勝{i}"] = Median(arr, "特勝");
					//dic[$"産駒特勝率{i}"] = Median(arr, "特勝率") * 100F;
					//dic[$"産駒平出{i}"] = Median(arr, "平出");
					//dic[$"産駒平勝{i}"] = Median(arr, "平勝");
					dic[$"産駒平勝率{i}"] = Median(arr, "平勝率", 0F) * 100F;
					//dic[$"産駒場出{i}"] = Median(arr, "場出");
					//dic[$"産駒場勝{i}"] = Median(arr, "場勝");
					dic[$"産駒場勝率{i}"] = Median(arr, "場勝率", 0F) * 100F;
					dic[$"産駒EI{i}"] = Median(arr, "EI", 0F) * 100F;
					//dic[$"産駒賞金{i}"] = Median(arr, "産賞金");
					//dic[$"産駒距離差{i}"] = Median(arr, "距離差", dic["距離"].GetSingle() - DEF["場距"]);
				});
			}

			return dic;
		}

		private float Get斤上(float 上り, float 斤量) => 上り.GetSingle() * 600F / (斤量.GetSingle() + 545F);

		private float Get斤上(Dictionary<string, object> src, string k1, string k2) => Get斤上(src[k1].GetSingle(), src[k2].GetSingle());

		private float Get斤上(Dictionary<string, object> src) => Get斤上(src, "上り", "斤量");

		private async Task RefreshKetto(SQLiteControl conn)
		{
			var keys = await conn.ExistsColumn("t_ketto", "馬ID").RunAsync(exists =>
			{
				return exists
					? "SELECT DISTINCT 馬ID FROM t_orig WHERE NOT EXISTS (SELECT * FROM t_ketto WHERE t_orig.馬ID = t_ketto.馬ID)"
					: "SELECT DISTINCT 馬ID FROM t_orig";
			}).RunAsync(async sql => await conn.GetRows(r => r.Get<string>(0), sql));

			await RefreshKetto(conn, keys);
		}

		private async Task RefreshKetto(SQLiteControl conn, IEnumerable<string> keys, bool progress = true)
		{
			// ﾃｰﾌﾞﾙ作成
			await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_ketto (馬ID, 父ID, 母ID, PRIMARY KEY (馬ID))");

			var newkeys = await keys.WhereAsync(async uma =>
			{
				return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) CNT FROM t_ketto WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.String, uma)) == 0;
			}).RunAsync(arr => arr.ToArray());

			if (progress)
			{
				Progress.Minimum = 0;
				Progress.Maximum = newkeys.Length;
				Progress.Value = 0;
			}

			foreach (var chunk in newkeys.Chunk(100))
			{
				await conn.BeginTransaction();
				foreach (var ketto in chunk.Select(uma => GetKetto(uma)))
				{
					await foreach (var dic in ketto)
					{
						if (!string.IsNullOrEmpty(dic["馬ID"]))
						{
							await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
								SQLiteUtil.CreateParameter(DbType.String, dic["馬ID"]),
								SQLiteUtil.CreateParameter(DbType.String, dic["父ID"]),
								SQLiteUtil.CreateParameter(DbType.String, dic["母ID"])
							);
						}
					}
					if (progress) Progress.Value += 1;
				}
				conn.Commit();
			}
		}

		private async Task RefreshSanku(SQLiteControl conn)
		{
			var keys = await conn.ExistsColumn("t_sanku", "馬ID").RunAsync(exists =>
			{
				var sqlbase = "SELECT DISTINCT 馬ID FROM t_ketto";

				return exists
					? $"WITH w_ketto AS ({sqlbase}) SELECT * FROM w_ketto WHERE NOT EXISTS (SELECT * FROM t_sanku WHERE w_ketto.馬ID = t_sanku.馬ID)"
					: $"WITH w_ketto AS ({sqlbase}) SELECT * FROM w_ketto";
			}).RunAsync(async sql => await conn.GetRows(r => r.Get<string>(0), sql));

			await RefreshSanku(conn, keys);
		}

		private async Task RefreshSanku(SQLiteControl conn, IEnumerable<string> keys, bool progress = true)
		{
			var existssanku = await conn.ExistsColumn("t_sanku", "馬ID");
			var newkeys = await existssanku.Run(async exists =>
			{
				var sql = Arr(
					$"WITH w_ketto AS (SELECT 父ID, 母ID FROM t_ketto WHERE 馬ID IN ({keys.Select(x => $"'{x}'").GetString(",")}))",
					$"SELECT DISTINCT 父ID FROM w_ketto WHERE 父ID NOT IN (SELECT 馬ID FROM t_sanku)",
					$"UNION",
					$"SELECT DISTINCT 父ID FROM t_ketto WHERE 馬ID IN (SELECT 母ID FROM w_ketto) AND 父ID NOT IN (SELECT 馬ID FROM t_sanku)"
				).GetString(" ");

				return exists
					? await conn.GetRows(r => r.Get<string>(0), sql)
					: keys;
			}).RunAsync(arr => arr.ToArray());

			if (progress)
			{
				Progress.Minimum = 0;
				Progress.Maximum = newkeys.Length;
				Progress.Value = 0;
			}

			foreach (var chunk in newkeys.Chunk(100))
			{
				if (existssanku) await conn.BeginTransaction();
				foreach (var sanku in chunk.Select(uma => GetSanku(uma)))
				{
					await foreach (var dic in sanku)
					{
						if (!existssanku)
						{
							existssanku = true;

							// ﾃｰﾌﾞﾙ作成
							await conn.ExecuteNonQueryAsync(Arr("CREATE TABLE IF NOT EXISTS t_sanku (馬ID,年度,順位 REAL,出走頭数 REAL,勝馬頭数 REAL,出走回数 REAL,勝利回数 REAL,重出 REAL,重勝 REAL,特出 REAL,特勝 REAL,平出 REAL,平勝 REAL,芝出 REAL,芝勝 REAL,ダ出 REAL,ダ勝 REAL,EI REAL,賞金 REAL,芝距 REAL,ダ距 REAL,",
								"PRIMARY KEY (馬ID,年度))").GetString(" ")
							);

							await conn.ExecuteNonQueryAsync(Arr(
								"CREATE TABLE IF NOT EXISTS t_sanku (",
								dic.Keys.Select(x => Arr("馬ID", "年度").Contains(x) ? x : $"{x} REAL").GetString(","),
								",PRIMARY KEY (馬ID,年度))").GetString(" "));

							await conn.BeginTransaction();
						}

						await conn.ExecuteNonQueryAsync(
							$"REPLACE INTO t_sanku ({dic.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", dic.Keys.Count).GetString(",")})",
							dic.Values.Select((x, i) => SQLiteUtil.CreateParameter(i < 2 ? DbType.String : DbType.Single, x)).ToArray()
						);
					}
					if (progress) Progress.Value += 1;
				}
				conn.Commit();
			}
		}

		private float GET着順(Dictionary<string, object> x, bool rank)
		{
			var 頭数 = TOU[x["ﾚｰｽID"].GetInt64()];
			var 着順 = x["着順"].GetSingle();

			var RANK = x["ﾗﾝｸ1"].Str() switch
			{
				"G1" => 着順G1______,
				"G2" => 着順G2______,
				"G3" => 着順G3______,
				"オープン" => 着順オープン,
				"3勝" => 着順3勝_____,
				"2勝" => 着順2勝_____,
				"1勝" => 着順1勝_____,
				"新馬" => 着順新馬____,
				_ => 着順未勝利__
			};

			return (着順 / 頭数).Pow(1.5F) * (rank ? (着順G1______ / RANK) : 1F);
			//return ((頭数 + 頭数固定) / 頭数倍率 / (累乗倍率 * 着順)) * RANK * 備考;
		}

		private readonly static float 頭数固定 = 15F;
		private readonly static float 着順倍率 = 2.0F / 3.0F;
		private readonly static float 着順新馬____ = 1.00F.Pow(1.750F);
		private readonly static float 着順未勝利__ = 1.25F.Pow(1.750F);
		private readonly static float 着順1勝_____ = 1.50F.Pow(1.750F);
		private readonly static float 着順2勝_____ = 1.75F.Pow(1.750F);
		private readonly static float 着順3勝_____ = 2.00F.Pow(1.750F);
		private readonly static float 着順オープン = 2.25F.Pow(1.750F);
		private readonly static float 着順G3______ = 2.50F.Pow(1.750F);
		private readonly static float 着順G2______ = 2.75F.Pow(1.750F);
		private readonly static float 着順G1______ = 3.00F.Pow(1.750F);
	}
}