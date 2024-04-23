using AngleSharp.Dom;
using AngleSharp.Text;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public CheckboxItemModel S2Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

		public IRelayCommand S2EXEC => RelayCommand.Create(async _ =>
		{
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
					"SELECT DISTINCT ﾚｰｽID FROM t_orig WHERE 開催日数 >= ? ORDER BY ﾚｰｽID DESC",
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, target)
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
				await RefreshKetto(conn);

				// 産駒成績の更新
				await RefreshSanku(conn);

				// ﾚｰｽ情報の初期化
				await InitializeModelBase(conn);

				foreach (var raceid in rac)
				{
					MessageService.Debug($"ﾚｰｽID:開始:{raceid}");

					// ﾚｰｽ毎の纏まり
					var racarr = await CreateRaceModel(conn, "t_orig", raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切);
					var head1 = Arr("ﾚｰｽID", "開催日数", "枠番", "馬番", "着順", "ﾗﾝｸ2", "馬ID");
					var head2 = Arr("着順", "単勝", "人気");

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
							$"INSERT INTO t_model ({head1.GetString(",")},単勝,Features) VALUES ({Enumerable.Repeat("?", head1.Length).GetString(",")}, ?, ?)",
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
		private Dictionary<string, float> DEF;
		private Dictionary<long, float> TOU;
		private Dictionary<object, Dictionary<string, float>> OIK;

		private async Task InitializeModelBase(SQLiteControl conn)
		{
			TOU = await conn.GetRows("SELECT ﾚｰｽID, COUNT(馬番) 頭数 FROM t_orig GROUP BY ﾚｰｽID").RunAsync(arr =>
			{
				return arr.ToDictionary(x => x["ﾚｰｽID"].GetInt64(), x => x["頭数"].GetSingle());
			});

			// ﾃﾞﾌｫﾙﾄ値の作製
			DEF = await conn.GetRow<float>(Arr(
				$"WITH w_tou AS (SELECT ﾚｰｽID, COUNT(馬番) 頭数 FROM t_orig GROUP BY ﾚｰｽID)",
				$"SELECT",
				$"AVG((頭数 / 着順) * {着順CASE}) 着順,",
				$"AVG(着順) 着順SRC,",
				$"AVG(体重) 体重,",
				$"AVG(単勝) 単勝,",
				$"AVG(距離) 距離,",
				$"AVG(上り) 上り,",
				$"AVG(賞金) 賞金,",
				$"AVG(斤量) 斤量",
				$"FROM t_orig LEFT JOIN w_tou ON w_tou.ﾚｰｽID = t_orig.ﾚｰｽID"
			).GetString(" "));

			DEF["斤上"] = Get斤上(DEF["上り"], DEF["斤量"]);
			DEF["時間"] = 16.237541F;
			DEF["勝時差"] = 1.0F;
			DEF["勝上差"] = DEF["斤上"] - DEF["斤上"] * 0.9F;
			DEF["出走間隔"] = 40F;
			DEF.AddRange(await conn.GetRow<float>(Arr(
				$"SELECT",
				$"    AVG(順位) 順位,",
				$"    AVG(出走頭数) 出走頭数,",
				$"    AVG(勝馬頭数) 勝馬頭数,",
				$"    AVG(勝馬頭数 / 出走頭数) AS 勝馬率,",
				$"    AVG(出走回数) 出走回数,",
				$"    AVG(勝利回数) 勝利回数,",
				$"    AVG(勝利回数 / 出走回数) AS 勝利率,",
				$"    AVG(重出) 重出,",
				$"    AVG(重勝) 重勝,",
				$"    AVG(IFNULL(重勝 / 重出, 0)) 重勝率,",
				$"    AVG(特出) 特出,",
				$"    AVG(特勝) 特勝,",
				$"    AVG(IFNULL(特勝 / 特出, 0)) 特勝率,",
				$"    AVG(平出) 平出,",
				$"    AVG(平勝) 平勝,",
				$"    AVG(IFNULL(平勝 / 平出, 0)) 平勝率,",
				$"    AVG((芝出+ダ出)/2) 場出,",
				$"    AVG((芝勝+ダ勝)/2) 場勝,",
				$"    AVG((IFNULL(芝勝/芝出,0)+IFNULL(ダ勝/ダ出,0))/2) 場勝率,",
				$"    AVG(EI) EI,",
				$"    AVG(賞金) 産賞金,",
				$"    AVG((芝距+ダ距)/2) 場距",
				$"FROM t_sanku"
			).GetString(" ")));

			OIK = await conn.GetRows(Arr(
				$"SELECT",
				$"    追切場所,",
				$"    IFNULL(AVG(CASE WHEN CAST(追切時間1 AS REAL) = 0 THEN NULL ELSE 追切時間1 END), 0) 追切時間1,",
				$"    IFNULL(AVG(CASE WHEN CAST(追切時間2 AS REAL) = 0 THEN NULL ELSE 追切時間2 END), 0) 追切時間2,",
				$"    IFNULL(AVG(CASE WHEN CAST(追切時間3 AS REAL) = 0 THEN NULL ELSE 追切時間3 END), 0) 追切時間3,",
				$"    IFNULL(AVG(CASE WHEN CAST(追切時間4 AS REAL) = 0 THEN NULL ELSE 追切時間4 END), 0) 追切時間4,",
				$"    IFNULL(AVG(CASE WHEN CAST(追切時間5 AS REAL) = 0 THEN NULL ELSE 追切時間5 END), 0) 追切時間5",
				$"FROM",
				$"    t_orig",
				$"GROUP BY",
				$"    追切場所"
			).GetString(" ")).RunAsync(val =>
			{
				return val.ToDictionary(
					x => x["追切場所"],
					x => x.Where(x => x.Key != "追切場所").ToDictionary(y => y.Key, y => y.Value.GetSingle())
				);
			});
		}

		private float GetSingle(IEnumerable<float> arr, float def, Func<IEnumerable<float>, float> func)
		{
			return arr.Where(x => !float.IsNaN(x)).Any() ? func(arr.Where(x => !float.IsNaN(x))) : def;
		}

		private float Median(IEnumerable<float> arr, float def)
		{
			return GetSingle(arr, def, ret => ret.Average());
		}

		private float Median(IEnumerable<Dictionary<string, object>> arr, string n)
		{
			return Median(arr, n, DEF[n]);
		}

		private float Median(IEnumerable<Dictionary<string, object>> arr, string n, float def)
		{
			return Median(arr.Select(x => x[n].GetSingle()), def);
		}

		private float Std(IEnumerable<float> arr) => arr.Where(x => !float.IsNaN(x)).Run(xxx => 1 < xxx.Count() ? (float)xxx.StandardDeviation() : 0F);

		private async Task<IEnumerable<Dictionary<string, object>>> CreateRaceModel(SQLiteControl conn, string tablename, string raceid, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			// 同ﾚｰｽの平均を取りたいときに使用する
			var 同ﾚｰｽ = await conn.GetRows($"SELECT * FROM {tablename} WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(System.Data.DbType.String, raceid)
			);

			TOU[raceid.GetInt64()] = 同ﾚｰｽ.Count;

			// ﾚｰｽ毎の纏まり
			var racarr = await 同ﾚｰｽ.Select(src => ToModel(conn, src, ﾗﾝｸ2, 馬性, 調教場所, 追切).TryCatch()).WhenAll();

			var drops = Arr("距離", "調教場所", "枠番", "馬番", "馬ID", "着順", "単勝", "ﾚｰｽID", "開催日数", "ﾗﾝｸ2"); ;
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

						dic[$"{key}A1"] = val - arr.Average();
						dic[$"{key}A2"] = val - arr.Percentile(25);
						dic[$"{key}A3"] = val - arr.Percentile(75);
						dic[$"{key}A4"] = val - arr.Max();
						dic[$"{key}A5"] = val - arr.Min();
						dic[$"{key}A7"] = val * std;
						dic[$"{key}B1"] = val == 0 ? 0F : arr.Average() / val * 100;
						dic[$"{key}B2"] = val == 0 ? 0F : arr.Percentile(25) / val * 100;
						dic[$"{key}B3"] = val == 0 ? 0F : arr.Percentile(75) / val * 100;
						dic[$"{key}B4"] = val == 0 ? 0F : arr.Max() / val * 100;
						dic[$"{key}B5"] = val == 0 ? 0F : arr.Min() / val * 100;
						dic[$"{key}B6"] = val == 0 ? 0F : arr.Sum() / val;
					}
					catch
					{
						throw;
					}
				});
			});

			return racarr;
		}

		private async Task<Dictionary<string, object>> ToModel(SQLiteControl conn, Dictionary<string, object> src, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			var 馬情報 = await conn.GetRows(
					$" SELECT t_orig.*, t_top.ﾀｲﾑ TOPﾀｲﾑ, t_top.上り TOP上り, t_top.斤量 TOP斤量" +
					$" FROM t_orig" +
					$" LEFT OUTER JOIN t_orig t_top ON t_orig.ﾚｰｽID = t_top.ﾚｰｽID AND t_orig.開催日数 = t_top.開催日数 AND t_top.着順 = 1" +
					$" WHERE t_orig.馬ID = ? AND t_orig.開催日数 < ? ORDER BY t_orig.開催日数 DESC",
					SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["開催日数"])
			).RunAsync(arr =>
			{
				return Arr(arr).Concat(Arr(1, 2, 3).Select(i => arr.Take(i).ToList())).ToArray();
			});

			var dic = new Dictionary<string, object>();
			// ﾍｯﾀﾞ情報
			dic["ﾚｰｽID"] = src["ﾚｰｽID"].GetInt64();
			dic["開催日数"] = src["開催日数"].GetInt64();
			//dic["ﾗﾝｸ1"] = ﾗﾝｸ1.IndexOf(src["ﾗﾝｸ1"]);
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

			馬情報.ForEach((arr, i) => dic[$"斤量平{i}"] = Median(arr, "斤量"));

			馬情報.ForEach((arr, i) => dic[$"賞金平{i}"] = Median(arr, "賞金"));

			馬情報.ForEach((arr, i) => dic[$"単勝平{i}"] = Median(arr, "単勝"));

			馬情報.ForEach((arr, i) => dic[$"ﾀｲﾑ指数平{i}"] = Median(arr, "ﾀｲﾑ指数"));

			馬情報.ForEach((arr, i) =>
			{
				Arr("追切時間1", "追切時間2", "追切時間3", "追切時間4", "追切時間5").ForEach(oik =>
				{
					dic[$"{oik}平{i}"] = Median(arr.Select(tmp =>
					{
						var avg = OIK[tmp["追切場所"]][oik];
						var val = tmp[oik].GetSingle();
						return val - avg;
					}), 0F);
				});
			});

			dic["体重"] = Median(馬情報[0], "体重");
			//dic["増減"] = src["増減"].GetDouble();
			//dic["増減割"] = dic["増減"] / dic["体重"];
			dic["斤量割"] = dic["斤量"].GetSingle() / dic["体重"].GetSingle();
			dic["調教場所"] = 調教場所.IndexOf(src["調教場所"]);
			//dic["一言"] = 一言.IndexOf(src["一言"]);
			dic["追切"] = 追切.IndexOf(src["追切"]);

			var tgt = Arr("開催場所", "馬場", "馬場状態");
			Func<List<Dictionary<string, object>>, bool, int, IEnumerable<List<Dictionary<string, object>>>> CREATE情報 = (arr, istgt, take) =>
			{
				var tgtlst = istgt ? tgt.Select(ttt => arr.Where(x => x[ttt] == src[ttt]).ToList()) : Enumerable.Empty<List<Dictionary<string, object>>>();
				var tklst1 = arr.Take(take).ToList();
				var tklst2 = tgtlst.Select(tmp => tmp.Take(take).ToList());
				return Arr(arr, tklst1).Concat(tgtlst).Concat(tklst2);
			};
			Action<string, List<Dictionary<string, object>>, int> ACTION情報 = (key, arr, i) =>
			{
				var A = arr.Select(x => x["着順"].GetSingle()).ToArray();
				var B = arr.Select(x => GET着順(x)).ToArray();
				var C = arr.Select(x => x["着順"].GetSingle() / x["単勝"].GetSingle(DEF["単勝"])).ToArray();

				//dic[$"{key}A0{i.ToString(2)}"] = GetSingle(A, DEF["着順SRC"], l => l.Average());
				//dic[$"{key}A0{i.ToString(2)}"] = GetSingle(A, DEF["着順SRC"], l => l.Percentile(25));
				//dic[$"{key}A0{i.ToString(2)}"] = GetSingle(A, DEF["着順SRC"], l => l.Percentile(75));
				dic[$"{key}B0{i.ToString(2)}"] = GetSingle(B, DEF["着順"], l => l.Average());
				dic[$"{key}B0{i.ToString(2)}"] = GetSingle(B, DEF["着順"], l => l.Percentile(25));
				dic[$"{key}B0{i.ToString(2)}"] = GetSingle(B, DEF["着順"], l => l.Percentile(75));
			};

			// 着順平均
			馬情報[0].Run(arr => CREATE情報(arr, true, 5)).ForEach((arr, i) =>
			{
				ACTION情報("馬ID", arr, i);
			});

			Arr("騎手ID", "調教師ID", "馬主ID").ForEach(async key =>
			{
				var 情報 = await conn.GetRows(
					$"SELECT ﾚｰｽID, ﾗﾝｸ2, 着順, 単勝, 開催場所, 馬場, 馬場状態 FROM t_orig WHERE {key} = ? AND 開催日数 < ? AND 開催日数 > ?",
					SQLiteUtil.CreateParameter(System.Data.DbType.String, src[key]),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["開催日数"].GetInt64()),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["開催日数"].GetInt64() - 365)
				).RunAsync(arr => CREATE情報(arr, key == "騎手ID", 30));

				情報.ForEach((arr, i) => ACTION情報(key, arr, i));
			});

			Arr(
				("馬ID", "騎手ID"), ("馬ID", "調教師ID")/*, ("馬ID", "馬主ID")*/
			// , ("騎手ID", "調教師ID"), ("騎手ID", "馬主ID")
			// , ("調教師ID", "馬主ID")
			).ForEach(x =>
			{
				dic[$"{x.Item1}{x.Item2}2"] = dic[$"{x.Item1}B000"].GetSingle() + dic[$"{x.Item2}B000"].GetSingle();
				dic[$"{x.Item1}{x.Item2}3"] = dic[$"{x.Item1}B000"].GetSingle() * dic[$"{x.Item2}B000"].GetSingle();
			});

			// 得意距離、及び今回のﾚｰｽ距離との差
			dic["距離"] = src["距離"].GetSingle();

			馬情報.ForEach((arr, i) =>
			{
				dic[$"距離得{i}"] = Median(arr, "距離");
				dic[$"距離差{i}"] = dic["距離"].GetSingle() - dic[$"距離得{i}"].GetSingle();
			});

			// 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			Func<object, double> func_tuka = v => $"{v}".Split('-').Take(2).Select(x => x.GetDouble()).Average(TOU[dic["ﾚｰｽID"].GetInt64()] / 2);
			馬情報.ForEach((arr, i) => dic[$"通過{i}"] = Median(arr.Select(x => (float)func_tuka(x["通過"])), TOU[dic["ﾚｰｽID"].GetInt64()] / 2));

			// 上り×斤量
			馬情報.ForEach((arr, i) =>
			{
				dic[$"斤上{i}"] = Median(arr, "上り");
				dic[$"斤上{i}"] = Median(arr.Select(x => Get斤上(x)), DEF["斤上"]);
			});

			// ﾀｲﾑの平均、ﾀｲﾑ平均×上り×斤量
			Func<object, float> func_time = v => $"{v}".Split(':')[0].GetSingle() * 60 + $"{v}".Split(':')[1].GetSingle();
			馬情報.ForEach((arr, i) =>
			{
				dic[$"時間{i}"] = Median(arr.Select(x => x["距離"].GetSingle() / func_time(x["ﾀｲﾑ"])), DEF["時間"]);
				dic[$"斤間{i}"] = dic[$"斤上{i}"].GetSingle() * dic[$"時間{i}"].GetSingle();
			});

			// 1着との差(時間)
			馬情報.ForEach((arr, i) => dic[$"勝時差{i}"] = Median(arr.Select(x => func_time(x["ﾀｲﾑ"]) - func_time(x["TOPﾀｲﾑ"])), DEF["勝時差"]));
			// 1着との差(上り×斤量)
			馬情報.ForEach((arr, i) => dic[$"勝上差{i}"] = Median(arr.Select(x => Get斤上(x) - Get斤上(x, "TOP上り", "TOP斤量")), DEF["勝上差"]));

			// 出走間隔
			馬情報.ForEach((arr, i) =>
			{
				dic[$"出走間隔{i}"] = dic["開催日数"].GetSingle() - GetSingle(arr.Select(x => x["開催日数"].GetSingle()), DEF["出走間隔"] * (i + 1), x => x.Max());

			});

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
						$"FROM w_titi, t_sanku WHERE w_titi.父ID = t_sanku.馬ID AND CAST(t_sanku.年度 AS INTEGER) < {src["ﾚｰｽID"].ToString().Left(4)}"
					).GetString(" ")
				).RunAsync(arr =>
				{
					return Arr(arr, arr.Where(x => x["年度"].GetInt64() > (src["ﾚｰｽID"].ToString().Left(4).GetInt64() - 4L)))
						.SelectMany(lst => Enumerable.Range(1, 1).Select(i => arr.Where(x => x["LAYER"].GetInt32() <= i * 2).ToList()))
						.ToArray();
				});
				産駒情報.ForEach((arr, i) =>
				{
					dic[$"産駒順位{i}"] = Median(arr, "順位");
					dic[$"産駒出走頭数{i}"] = Median(arr, "出走頭数");
					dic[$"産駒勝馬頭数{i}"] = Median(arr, "勝馬頭数");
					dic[$"産駒勝馬率{i}"] = Median(arr, "勝馬率");
					dic[$"産駒出走回数{i}"] = Median(arr, "出走回数");
					dic[$"産駒勝利回数{i}"] = Median(arr, "勝利回数");
					dic[$"産駒勝利率{i}"] = Median(arr, "勝利率");
					//dic[$"産駒重出{i}"] = Median(arr, "重出");
					//dic[$"産駒重勝{i}"] = Median(arr, "重勝");
					//dic[$"産駒重勝率{i}"] = Median(arr, "重勝率");
					//dic[$"産駒特出{i}"] = Median(arr, "特出");
					//dic[$"産駒特勝{i}"] = Median(arr, "特勝");
					//dic[$"産駒特勝率{i}"] = Median(arr, "特勝率");
					dic[$"産駒平出{i}"] = Median(arr, "平出");
					dic[$"産駒平勝{i}"] = Median(arr, "平勝");
					dic[$"産駒平勝率{i}"] = Median(arr, "平勝率");
					dic[$"産駒場出{i}"] = Median(arr, "場出");
					dic[$"産駒場勝{i}"] = Median(arr, "場勝");
					dic[$"産駒場勝率{i}"] = Median(arr, "場勝率");
					dic[$"産駒EI{i}"] = Median(arr, "EI");
					dic[$"産駒賞金{i}"] = Median(arr, "産賞金");
					dic[$"産駒距離差{i}"] = Median(arr, "距離差", dic["距離"].GetSingle() - DEF["場距"]);
				});
			}

			return dic;
		}

		private float GET着順(Dictionary<string, object> x)
		{
			var 頭数 = TOU[x["ﾚｰｽID"].GetInt64()];
			var 着順 = x["着順"].GetSingle();
			var RANK = $"{x["ﾗﾝｸ2"]}" switch
			{
				"RANK1" => 着順RANK1,
				"RANK2" => 着順RANK2,
				"RANK3" => 着順RANK3,
				"RANK4" => 着順RANK4,
				"RANK5" => 着順RANK5,
				_ => 着順RANK5
			};

			return (頭数 / 着順) * RANK;
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

		private async Task RefreshKetto(SQLiteControl conn, IEnumerable<string> keys)
		{
			// ﾃｰﾌﾞﾙ作成
			await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_ketto (馬ID, 父ID, 母ID, PRIMARY KEY (馬ID))");

			var kettolocker = Locker.GetNewLockKey();
			var newkeys = await keys.Select(async uma =>
			{
				if (await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) CNT FROM t_ketto WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.String, uma)) > 0)
				{
					return null;
				}
				else
				{
					return uma;
				}
			}).WhenAll().RunAsync(arr =>
			{
				return arr.Where(x => x != null);
			}).RunAsync(arr => arr.Select(uma =>
			{
				return GetKetto($"{uma}");
			}));

			foreach (var chunk in newkeys.Chunk(100))
			{
				await conn.BeginTransaction();
				foreach (var ketto in chunk)
				{
					await foreach (var dic in ketto)
					{
						await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
							SQLiteUtil.CreateParameter(System.Data.DbType.String, dic["馬ID"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.String, dic["父ID"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.String, dic["母ID"])
						);
					}
				}
				conn.Commit();
			}
			//var count = 0;

			//await conn.BeginTransaction();
			//foreach (var key in keys)
			//{
			//	if (await conn.GetRow("SELECT COUNT(*) CNT FROM t_ketto WHERE 馬ID = ?", SQLiteUtil.CreateParameter(System.Data.DbType.String, key)).RunAsync(x => x["CNT"].GetInt32() > 0))
			//	{
			//		continue;
			//	}

			//	using (var ped = await AppUtil.GetDocument(false, url))
			//	{
			//		if (ped.GetElementsByClassName("blood_table detail").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
			//		{
			//			Func<IElement[], int, string> func = (tags, i) => tags
			//				.Skip(i).Take(1)
			//				.Select(x => x.GetHrefAttribute("href"))
			//				.Select(x => !string.IsNullOrEmpty(x) ? x.Split('/')[2] : string.Empty)
			//				.FirstOrDefault() ?? string.Empty;
			//			var rowspan16 = table.GetElementsByTagName("td").Where(x => x.GetAttribute("rowspan").GetInt32() == 16).ToArray();
			//			var f = func(rowspan16, 0);
			//			var m = func(rowspan16, 1);

			//			await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
			//				SQLiteUtil.CreateParameter(System.Data.DbType.String, key),
			//				SQLiteUtil.CreateParameter(System.Data.DbType.String, f),
			//				SQLiteUtil.CreateParameter(System.Data.DbType.String, m)
			//			);

			//			var rowspan08 = table.GetElementsByTagName("td").Where(x => x.GetAttribute("rowspan").GetInt32() == 8).ToArray();
			//			var ff = func(rowspan08, 0);
			//			var fm = func(rowspan08, 1);
			//			var mf = func(rowspan08, 2);
			//			var mm = func(rowspan08, 3);

			//			if (!string.IsNullOrEmpty(f))
			//			{
			//				await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
			//					SQLiteUtil.CreateParameter(System.Data.DbType.String, f),
			//					SQLiteUtil.CreateParameter(System.Data.DbType.String, ff),
			//					SQLiteUtil.CreateParameter(System.Data.DbType.String, fm)
			//				);
			//			}

			//			if (!string.IsNullOrEmpty(m))
			//			{
			//				await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
			//					SQLiteUtil.CreateParameter(System.Data.DbType.String, m),
			//					SQLiteUtil.CreateParameter(System.Data.DbType.String, mf),
			//					SQLiteUtil.CreateParameter(System.Data.DbType.String, mm)
			//				);
			//			}

			//			if (1000 < count++)
			//			{
			//				count = 0;
			//				conn.Commit();
			//				await conn.BeginTransaction();
			//			}
			//		}
			//	}
			//}
			//conn.Commit();
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

		private async Task RefreshSanku(SQLiteControl conn, IEnumerable<string> keys)
		{
			var newkeys = await conn.ExistsColumn("t_sanku", "馬ID").RunAsync(async exists =>
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
			});

			var sankulocker = Locker.GetNewLockKey();
			var sankuarrs = keys.Select(uma =>
			{
				return GetSanku(uma);
			});

			var create = false;
			foreach (var chunk in sankuarrs.Chunk(100))
			{
				if (create) await conn.BeginTransaction();
				foreach (var ketto in chunk)
				{
					await foreach (var dic in ketto)
					{
						if (!create)
						{
							create = true;

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
				}
				conn.Commit();
			}

			//var sql = Arr(
			//	$"WITH w_ketto AS (SELECT 父ID, 母ID FROM t_ketto WHERE 馬ID IN ({keys.Select(x => $"'{x}'").GetString(",")}))",
			//	$"SELECT DISTINCT 父ID FROM w_ketto WHERE 父ID NOT IN (SELECT 馬ID FROM t_sanku)",
			//	$"UNION",
			//	$"SELECT DISTINCT 父ID FROM t_ketto WHERE 馬ID IN (SELECT 母ID FROM w_ketto) AND 父ID NOT IN (SELECT 馬ID FROM t_sanku)"
			//).GetString(" ");

			//using (var fathers = await conn.ExecuteReaderAsync(sql))
			//{
			//	foreach (var data in await conn.GetRows(x => x.Get<string>(0), sql))
			//	{
			//		var url = $"https://db.netkeiba.com/?pid=horse_sire&id={data}&course=1&mode=1&type=0";

			//		using (var ped = await AppUtil.GetDocument(false, url))
			//		{
			//			if (ped.GetElementsByClassName("nk_tb_common race_table_01").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
			//			{
			//				if (transaction) await conn.BeginTransaction();
			//				foreach (var row in table.Rows.Skip(3))
			//				{
			//					var dic = new Dictionary<string, object>();

			//					dic["馬ID"] = data;
			//					dic["年度"] = row.Cells[0].GetInnerHtml();
			//					dic["順位"] = row.Cells[1].GetInnerHtml().GetSingle();
			//					dic["出走頭数"] = row.Cells[2].GetInnerHtml().GetSingle();
			//					dic["勝馬頭数"] = row.Cells[3].GetInnerHtml().GetSingle();
			//					dic["出走回数"] = row.Cells[4].GetHrefInnerHtml().GetSingle();
			//					dic["勝利回数"] = row.Cells[5].GetHrefInnerHtml().GetSingle();
			//					dic["重出"] = row.Cells[6].GetHrefInnerHtml().GetSingle();
			//					dic["重勝"] = row.Cells[7].GetHrefInnerHtml().GetSingle();
			//					dic["特出"] = row.Cells[8].GetHrefInnerHtml().GetSingle();
			//					dic["特勝"] = row.Cells[9].GetHrefInnerHtml().GetSingle();
			//					dic["平出"] = row.Cells[10].GetHrefInnerHtml().GetSingle();
			//					dic["平勝"] = row.Cells[11].GetHrefInnerHtml().GetSingle();
			//					dic["芝出"] = row.Cells[12].GetHrefInnerHtml().GetSingle();
			//					dic["芝勝"] = row.Cells[13].GetHrefInnerHtml().GetSingle();
			//					dic["ダ出"] = row.Cells[14].GetHrefInnerHtml().GetSingle();
			//					dic["ダ勝"] = row.Cells[15].GetHrefInnerHtml().GetSingle();
			//					dic["EI"] = row.Cells[17].GetInnerHtml().GetSingle();
			//					dic["賞金"] = row.Cells[18].GetInnerHtml().Replace(",", "").GetSingle();
			//					dic["芝距"] = row.Cells[19].GetInnerHtml().Replace(",", "").GetSingle();
			//					dic["ダ距"] = row.Cells[20].GetInnerHtml().Replace(",", "").GetSingle();

			//					await conn.ExecuteNonQueryAsync($"REPLACE INTO t_sanku ({dic.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", dic.Count).GetString(",")})",
			//						dic.Values.Select((x, i) => SQLiteUtil.CreateParameter(i < 2 ? System.Data.DbType.String : System.Data.DbType.Single, x)).ToArray()
			//					);
			//				}
			//				if (transaction) conn.Commit();
			//			}
			//		}
			//	}
			//}
		}

		private const float 着順RANK1 = 9.00F;
		private const float 着順RANK2 = 6.00F;
		private const float 着順RANK3 = 4.00F;
		private const float 着順RANK4 = 1.00F;
		private const float 着順RANK5 = 1.00F;
		private readonly string 着順CASE = $"(CASE ﾗﾝｸ2 WHEN 'RANK1' THEN {着順RANK1} WHEN 'RANK2' THEN {着順RANK2} WHEN 'RANK3' THEN {着順RANK3} WHEN 'RANK4' THEN {着順RANK4} ELSE {着順RANK5} END)";

		private const float 着順LQ = 0.50F;
		private const float 着順UQ = 2.00F;

	}
}