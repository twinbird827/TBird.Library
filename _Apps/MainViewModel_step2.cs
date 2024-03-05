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
using System.Windows.Input;
using AngleSharp.Dom;

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

				// 産駒成績の更新
				await RefreshSanku(conn, true, await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 馬ID FROM t_orig WHERE 馬ID NOT IN (SELECT 馬ID FROM t_sanku)"));

				// ﾃﾞﾌｫﾙﾄ値の作製
				DEF = await conn.GetRow<float>(Arr(
					"SELECT",
					"AVG(着順 * (CASE WHEN ﾗﾝｸ2 = 'RANK1' THEN 0.50 WHEN ﾗﾝｸ2 = 'RANK2' THEN 0.75 WHEN ﾗﾝｸ2 = 'RANK3' THEN 1.00 WHEN ﾗﾝｸ2 = 'RANK4' THEN 1.25 ELSE 1.50 END)) 着順,",
					"AVG(体重) 体重,",
					"AVG(距離) 距離,",
					"AVG(上り) 上り,",
					"AVG(斤量) 斤量"
				).GetString(" "));

				DEF["斤上"] = Get斤上(DEF["上り"], DEF["斤量"]);
				DEF["時間"] = 16.237541F;
				DEF["勝時差"] = 1.0F;
				DEF["勝上差"] = DEF["斤上"] - DEF["斤上"] * 0.9F;
				DEF["出走間隔"] = 40F;

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
						await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_model (ﾚｰｽID INTEGER,開催日数 INTEGER,枠番 INTEGER,馬番 INTEGER,着順 INTEGER,ﾗﾝｸ2 INTEGER,馬ID INTEGER,Features BLOB, PRIMARY KEY (ﾚｰｽID, 馬番))");
					}

					string[]? keys = null;
					await conn.BeginTransaction();
					foreach (var ins in racarr)
					{
						keys = keys ?? ins.Keys.Where(x => !new[] { "ﾚｰｽID", "開催日数", "枠番", "馬番", "着順", "ﾗﾝｸ2" }.Contains(x)).ToArray();

						await conn.ExecuteNonQueryAsync(
							$"INSERT INTO t_model (ﾚｰｽID,開催日数,枠番,馬番,着順,ﾗﾝｸ2,馬ID,Features) VALUES (?, ?, ?, ?, ?, ?, ?)",
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["ﾚｰｽID"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["開催日数"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["枠番"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["馬番"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["着順"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["ﾗﾝｸ2"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Int64, ins["馬ID"]),
							SQLiteUtil.CreateParameter(System.Data.DbType.Binary, keys.SelectMany(x => BitConverter.GetBytes(ins[x].GetSingle())).ToArray())
						);
					}
					conn.Commit();

					AddLog($"Step5 Proccess ﾚｰｽID: {raceid}");

					Progress.Value += 1;
				}

				MessageService.Info("Step5 Completed!!");
			}
		});
		private Dictionary<string, float> DEF;

		private float GetSingle(float x, float y, float def, Func<float, float, float> func)
		{
			return !float.IsNaN(x) && !float.IsNaN(y) ? func(x, y) : def;
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
			return Median(arr.Select(x => x[n].GetSingle()), DEF[n]);
		}

		private float Median(IEnumerable<Dictionary<string, object>> arr, float def, Func<Dictionary<string, object>, float> sel)
		{
			return Median(arr.Select(x => sel(x)), def);
		}

		private async Task<IEnumerable<Dictionary<string, object>>> CreateRaceModel(SQLiteControl conn, string raceid, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			// 同ﾚｰｽの平均を取りたいときに使用する
			var 同ﾚｰｽ = await conn.GetRows("SELECT * FROM t_orig WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(System.Data.DbType.String, raceid)
			);

			// ﾚｰｽ毎の纏まり
			var racarr = await 同ﾚｰｽ.Select(src => ToModel(conn, src, ﾗﾝｸ2, 馬性, 調教場所, 追切).TryCatch()).WhenAll();

			var drops = Arr("距離", "調教場所", "枠番", "馬番", "馬ID", "着順", "ﾚｰｽID", "開催日数", "ﾗﾝｸ2"); ;
			var keys = racarr.First().Keys.Where(y => !drops.Contains(y)).ToArray();

			// 他の馬との比較
			racarr.ForEach(dic =>
			{
				keys.ForEach(key =>
				{
					dic[$"{key}S1"] = 他馬比較(dic, racarr, key, ret => ret.Average());
					dic[$"{key}S2"] = 他馬比較(dic, racarr, key, ret => ret.Percentile(25));
					dic[$"{key}S3"] = 他馬比較(dic, racarr, key, ret => ret.Percentile(75));
				});
			});

			return racarr;
		}

		private float 他馬比較(Dictionary<string, object> dic, Dictionary<string, object>[] racarr, string key, Func<IEnumerable<float>, float> func)
		{
			var x1 = dic[key].GetSingle();
			var x2 = GetSingle(racarr.Select(x => x[key].GetSingle()), DEF[key], func);
			return GetSingle(x1, x2, DEF[key], (x, y) => x - y);
		}

		private async Task<Dictionary<string, object>> ToModel(SQLiteControl conn, Dictionary<string, object> src, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
		{
			var 馬情報全 = await conn.GetRows(
					$" SELECT t_orig.*, t_top.ﾀｲﾑ TOPﾀｲﾑ, t_top.上り TOP上り, t_top.斤量 TOP斤量" +
					$" FROM t_orig" +
					$" LEFT OUTER JOIN t_orig t_top ON t_orig.ﾚｰｽID = t_top.ﾚｰｽID AND t_orig.開催日数 = t_top.開催日数 AND t_top.着順 = 1" +
					$" WHERE t_orig.馬ID = ? AND t_orig.開催日数 < ? ORDER BY t_orig.開催日数 DESC",
					SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
					SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["開催日数"])
			);

			var 馬情報直 = 馬情報全.Take(5).ToArray();

			var dic = new Dictionary<string, object>();
			// ﾍｯﾀﾞ情報
			dic["ﾚｰｽID"] = src["ﾚｰｽID"].GetInt64();
			dic["開催日数"] = src["開催日数"].GetInt64();
			//dic["ﾗﾝｸ1"] = ﾗﾝｸ1.IndexOf(src["ﾗﾝｸ1"]);
			dic["ﾗﾝｸ2"] = ﾗﾝｸ2.IndexOf(src["ﾗﾝｸ2"]);

			// 予測したいﾃﾞｰﾀ
			dic["着順"] = src["着順"].GetInt64();

			// 馬毎に違う情報
			dic["枠番"] = src["枠番"].GetInt64();
			dic["馬番"] = src["馬番"].GetInt64();
			dic["馬ID"] = src["馬ID"].GetInt64();
			dic["馬性"] = 馬性.IndexOf(src["馬性"]);
			dic["馬齢"] = src["馬齢"].GetSingle();
			dic["斤量"] = src["斤量"].GetSingle();
			dic["斤量平全"] = Median(馬情報全, "斤量");
			dic["斤量平直"] = Median(馬情報直, "斤量");

			dic["体重"] = float.TryParse($"{src["体重"]}", out float tmp_taiju) ? tmp_taiju : Median(馬情報直, "体重");
			//dic["増減"] = src["増減"].GetDouble();
			//dic["増減割"] = dic["増減"] / dic["体重"];
			dic["斤量割"] = dic["斤量"].GetSingle() / dic["体重"].GetSingle();
			dic["調教場所"] = 調教場所.IndexOf(src["調教場所"]);
			//dic["一言"] = 一言.IndexOf(src["一言"]);
			dic["追切"] = 追切.IndexOf(src["追切"]);

			var analysis = await Arr(
				GetAnalysis(conn, 200, src, "馬ID", new[] { "馬ID", "開催場所", "馬場", "馬場状態" }),
				GetAnalysis(conn, 100, src, "騎手ID", new[] { "騎手ID", "開催場所", "馬場", "馬場状態" }),
				GetAnalysis(conn, 100, src, "調教師ID", new[] { "調教師ID" }),
				GetAnalysis(conn, 100, src, "馬主ID", new[] { "馬主ID" })
			).WhenAll();

			dic.AddRange(analysis.SelectMany(x => x));

			// 得意距離、及び今回のﾚｰｽ距離との差
			dic["距離"] = src["距離"].GetSingle();
			dic["距離得"] = Median(馬情報全, "距離");
			dic["距離差"] = dic["距離得"].GetSingle() - dic["距離得"].GetSingle();

			// 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			Func<object, double> func_tuka = v => $"{v}".Split('-').Take(2).Select(x => x.GetDouble()).Average(6F);
			dic["通過全"] = Median(馬情報全, 5, x => (float)func_tuka(x["通過"]));
			dic["通過直"] = Median(馬情報直, 5, x => (float)func_tuka(x["通過"]));

			// 通過順の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			//dic["上り全"] = Avg2(馬情報全, x => x["上り"].GetDouble(), 上りDEF);
			//dic["上り直"] = Avg2(馬情報直, x => x["上り"].GetDouble(), 上りDEF);
			//dic["上小全"] = 馬情報全.MinOrDefault(x => x["上り"].GetDouble(), 上りDEF);
			//dic["上小直"] = 馬情報直.MinOrDefault(x => x["上り"].GetDouble(), 上りDEF);
			//dic["上大全"] = 馬情報全.MaxOrDefault(x => x["上り"].GetDouble(), 上りDEF);
			//dic["上大直"] = 馬情報直.MaxOrDefault(x => x["上り"].GetDouble(), 上りDEF);

			dic["斤上り全"] = Median(馬情報全, DEF["斤上"], x => Get斤上(x));
			dic["斤上り直"] = Median(馬情報直, DEF["斤上"], x => Get斤上(x));
			dic["斤上小全"] = GetSingle(馬情報全.Select(x => Get斤上(x)), DEF["斤上"] * 0.90F, ret => ret.Min());
			dic["斤上小直"] = GetSingle(馬情報直.Select(x => Get斤上(x)), DEF["斤上"] * 0.90F, ret => ret.Min());
			dic["斤上大全"] = GetSingle(馬情報全.Select(x => Get斤上(x)), DEF["斤上"] * 1.10F, ret => ret.Max());
			dic["斤上大直"] = GetSingle(馬情報直.Select(x => Get斤上(x)), DEF["斤上"] * 1.10F, ret => ret.Max());

			// ﾀｲﾑの平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
			Func<object, float> func_time = v => $"{v}".Split(':')[0].GetSingle() * 60 + $"{v}".Split(':')[1].GetSingle();
			dic["時間全"] = Median(馬情報全, DEF["時間"], x => x["距離"].GetSingle() / func_time(x["ﾀｲﾑ"]));
			dic["時間直"] = Median(馬情報直, DEF["時間"], x => x["距離"].GetSingle() / func_time(x["ﾀｲﾑ"]));

			dic["斤時全"] = dic["斤上り全"].GetSingle() * dic["時間全"].GetSingle();
			dic["斤時直"] = dic["斤上り直"].GetSingle() * dic["時間直"].GetSingle();

			// 1着との差(時間)
			dic["勝時差全"] = GetSingle(馬情報全.Select(x => func_time(x["ﾀｲﾑ"]) - func_time(x["TOPﾀｲﾑ"])), DEF["勝時差"], ret => ret.Median());
			dic["勝時差直"] = GetSingle(馬情報直.Select(x => func_time(x["ﾀｲﾑ"]) - func_time(x["TOPﾀｲﾑ"])), DEF["勝時差"], ret => ret.Median());

			dic["勝上差全"] = GetSingle(馬情報全.Select(x => Get斤上(x) - Get斤上(x, "TOP上り", "TOP斤量")), DEF["勝上差"], ret => ret.Median());
			dic["勝上差直"] = GetSingle(馬情報直.Select(x => Get斤上(x) - Get斤上(x, "TOP上り", "TOP斤量")), DEF["勝上差"], ret => ret.Median());

			// 着順平均
			dic["着順全"] = Median(馬情報全, DEF["着順"], x => GET着順(x));
			dic["着順直"] = Median(馬情報直, DEF["着順"], x => GET着順(x));

			dic["出走間隔"] = dic["開催日数"].GetSingle() - (0 < 馬情報直.Length ? 馬情報直[0]["開催日数"].GetSingle() : dic["開催日数"].GetSingle() - DEF["出走間隔"]);

			var 芝距 = $"(CASE WHEN IFNULL(AVG(芝距),0) = 0 THEN {dic["距離"]} ELSE AVG(芝距) END)";
			var ダ距 = $"(CASE WHEN IFNULL(AVG(ダ距),0) = 0 THEN {dic["距離"]} ELSE AVG(ダ距) END)";

			// 産駒成績
			var 産駒 = Arr(
				$"SELECT",
				$"AVG(順位), IFNULL(SUM(勝馬頭数)/SUM(出走頭数),0), IFNULL(SUM(勝利回数)/SUM(出走回数),0), AVG(EI), SUM(賞金),",
				$"{dic["距離"]} - ", $"{src["馬場"]}" == "芝" ? 芝距 : ダ距, ",",
				$"{src["馬場"]}" == "芝" ? "IFNULL(SUM(芝勝)/SUM(芝出),0)" : "IFNULL(SUM(ダ勝)/SUM(ダ出),0)"
			).GetString(" ");

			var 全父 = Arr(産駒,
				$"FROM t_ketto",
				$"LEFT JOIN t_sanku ON t_sanku.馬ID = t_ketto.父ID",
				$"WHERE t_ketto.馬ID = ? AND t_sanku.年度 <= ?"
			).GetString(" ");

			dic.AddRange(await conn.GetRow(
				r => Enumerable.Range(0, r.FieldCount).ToDictionary(i => $"全父{i.ToString(3)}", i => (object)r.Get<float>(i)),
				全父,
				SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
				SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["ﾚｰｽID"].ToString().Left(4).GetInt32() - 1)
			));

			var 直父 = Arr(全父, "AND t_sanku.年度 >= ?").GetString(" ");

			dic.AddRange(await conn.GetRow(
				r => Enumerable.Range(0, r.FieldCount).ToDictionary(i => $"直父{i.ToString(3)}", i => (object)r.Get<float>(i)),
				直父,
				SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
				SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["ﾚｰｽID"].ToString().Left(4).GetInt32() - 1),
				SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["ﾚｰｽID"].ToString().Left(4).GetInt32() - 4)
			));

			// 産駒BMS成績
			var 全母父 = Arr(産駒,
				$"FROM t_ketto a",
				$"LEFT JOIN t_ketto b ON b.馬ID = a.母ID",
				$"LEFT JOIN t_sanku ON t_sanku.馬ID = b.父ID",
				$"WHERE a.馬ID = ? AND t_sanku.年度 <= ?"
			).GetString(" ");

			dic.AddRange(await conn.GetRow(
				r => Enumerable.Range(0, r.FieldCount).ToDictionary(i => $"全母父{i.ToString(3)}", i => (object)r.Get<float>(i)),
				全母父,
				SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
				SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["ﾚｰｽID"].ToString().Left(4).GetInt32() - 1)
			));

			var 直母父 = Arr(全母父, "AND t_sanku.年度 >= ?").GetString(" ");

			dic.AddRange(await conn.GetRow(
				r => Enumerable.Range(0, r.FieldCount).ToDictionary(i => $"直母父{i.ToString(3)}", i => (object)r.Get<float>(i)),
				直母父,
				SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
				SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["ﾚｰｽID"].ToString().Left(4).GetInt32() - 1),
				SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["ﾚｰｽID"].ToString().Left(4).GetInt32() - 4)
			));

			// 産駒BMS成績
			return dic;
		}

		private float GET着順(Dictionary<string, object> x)
		{
			var 着順 = x["着順"].GetSingle();
			switch ($"{x["ﾗﾝｸ2"]}")
			{
				case "RANK1":
					return 着順 * 0.50F;
				case "RANK2":
					return 着順 * 0.75F;
				case "RANK3":
					return 着順 * 1.00F;
				case "RANK4":
					return 着順 * 1.25F;
				default:
					return 着順 * 1.50F;
			}
		}

		private float Get斤上(float 上り, float 斤量) => 上り.GetSingle() * 600F / (斤量.GetSingle() + 545F);

		private float Get斤上(Dictionary<string, object> src, string k1, string k2) => Get斤上(src[k1].GetSingle(), src[k2].GetSingle());

		private float Get斤上(Dictionary<string, object> src) => Get斤上(src, "上り", "斤量");

		private async Task<Dictionary<string, object>> GetAnalysis(SQLiteControl conn, int num, Dictionary<string, object> src, string keyname, string[] headnames)
		{
			var ranks = new[] { 1, 3, 5 };
			var now = $"{src["開催日数"]}".GetSingle();
			var keyvalue = $"{src[keyname]}";
			var select = Arr(
				$"IFNULL(MEDIAN(着順), {DEF["着順"]}",
				$"IFNULL(LOWER_QUARTILE(着順), {DEF["着順"] * 0.9}",
				$"IFNULL(UPPER_QUARTILE(着順), {DEF["着順"] * 1.1}"
			);
			var count = ranks.Length * select.Length * headnames.Length;
			var index = 0;
			var with = new List<string>();
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

					var where = Arr(
						$"FROM t_orig WHERE",
						$"{keyname} = '{keyvalue}' AND",
						$"開催日数  < {now}        AND",
						$"{{0}}",
						$"ﾗﾝｸ2     <= 'RANK{i}'",
						keyname == headname ? string.Empty : $"AND {headname} = '{src[headname]}'"
					).GetString(" ");

					with.Add($"TA{index.ToString(5)} AS (SELECT ({val}) 着順 {string.Format(where, $"                            ")})");
					with.Add($"TN{index.ToString(5)} AS (SELECT ({val}) 着順 {string.Format(where, $"開催日数  > {now - num}  AND")})");

					whe.Add(Arr($"(SELECT", select.Select((x, j) => $"{x} A_{key}_{index.ToString(5)}_{j.ToString(2)}").GetString(","), $"FROM TA{index.ToString(5)})").GetString(" "));
					whe.Add(Arr($"(SELECT", select.Select((x, j) => $"{x} N_{key}_{index.ToString(5)}_{j.ToString(2)}").GetString(","), $"FROM TN{index.ToString(5)})").GetString(" "));

					index += 1;
				}
			}

			return await conn.GetRow(
				r => Enumerable.Range(0, count).ToDictionary(i => r.GetName(i), i => (object)r.Get<object>(i).GetSingle()),
				Arr($"WITH", with.GetString(","), "SELECT * FROM", whe.GetString(" CROSS JOIN ")).GetString(" ")
			);
		}

		private async Task RefreshKetto(SQLiteControl conn, bool ischecked)
		{
			// ﾃｰﾌﾞﾙ作成
			await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_ketto (馬ID,父ID,母ID, PRIMARY KEY (馬ID))");

			var count = 0;

			await conn.BeginTransaction();
			foreach (var key in await conn.GetRows(r => r.Get<string>(0), $"SELECT DISTINCT 馬ID FROM t_orig WHERE 馬ID NOT IN (SELECT 馬ID FROM t_ketto)"))
			{
				var url = $"https://db.netkeiba.com/horse/ped/{key}/";

				using (var ped = await AppUtil.GetDocument(url))
				{
					if (ped.GetElementsByClassName("blood_table detail").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
					{
						Func<IElement[], int, string> func = (tags, i) => tags
							.Skip(i).Take(1)
							.Select(x => x.GetHrefAttribute("href"))
							.Select(x => !string.IsNullOrEmpty(x) ? x.Split('/')[2] : string.Empty)
							.FirstOrDefault() ?? string.Empty;
						var rowspan16 = table.GetElementsByTagName("td").Where(x => x.GetAttribute("rowspan").GetInt32() == 16).ToArray();
						var f = func(rowspan16, 0);
						var m = func(rowspan16, 1);

						await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
							SQLiteUtil.CreateParameter(System.Data.DbType.String, key),
							SQLiteUtil.CreateParameter(System.Data.DbType.String, f),
							SQLiteUtil.CreateParameter(System.Data.DbType.String, m)
						);

						var rowspan08 = table.GetElementsByTagName("td").Where(x => x.GetAttribute("rowspan").GetInt32() == 8).ToArray();
						var ff = func(rowspan08, 0);
						var fm = func(rowspan08, 1);
						var mf = func(rowspan08, 2);
						var mm = func(rowspan08, 3);

						if (!string.IsNullOrEmpty(f))
						{
							await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
								SQLiteUtil.CreateParameter(System.Data.DbType.String, f),
								SQLiteUtil.CreateParameter(System.Data.DbType.String, ff),
								SQLiteUtil.CreateParameter(System.Data.DbType.String, fm)
							);
						}

						if (!string.IsNullOrEmpty(m))
						{
							await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
								SQLiteUtil.CreateParameter(System.Data.DbType.String, m),
								SQLiteUtil.CreateParameter(System.Data.DbType.String, mf),
								SQLiteUtil.CreateParameter(System.Data.DbType.String, mm)
							);
						}

						if (1000 < count++)
						{
							count = 0;
							conn.Commit();
							await conn.BeginTransaction();
						}
					}
				}
			}
			conn.Commit();

		}

		private async Task RefreshSanku(SQLiteControl conn, bool transaction, IEnumerable<string> keys)
		{
			// ﾃｰﾌﾞﾙ作成
			await conn.ExecuteNonQueryAsync(Arr("CREATE TABLE IF NOT EXISTS t_sanku (馬ID,年度,順位 REAL,出走頭数 REAL,勝馬頭数 REAL,出走回数 REAL,勝利回数 REAL,重出 REAL,重勝 REAL,特出 REAL,特勝 REAL,平出 REAL,平勝 REAL,芝出 REAL,芝勝 REAL,ダ出 REAL,ダ勝 REAL,EI REAL,賞金 REAL,芝距 REAL,ダ距 REAL,",
				"PRIMARY KEY (馬ID,年度))").GetString(" ")
			);

			var sql = Arr(
				$"WITH w_ketto AS (SELECT 父ID,母ID FROM t_ketto WHERE 馬ID IN ({keys.Select(x => $"'{x}'").GetString(",")}))",
				$"SELECT DISTINCT 父ID FROM w_ketto",
				transaction ? "WHERE 父ID NOT IN (SELECT 馬ID FROM t_sanku)" : string.Empty,
				$"UNION",
				$"SELECT DISTINCT 父ID FROM t_ketto WHERE 馬ID IN (SELECT 母ID FROM w_ketto)",
				transaction ? "AND 父ID NOT IN (SELECT 馬ID FROM t_sanku)" : string.Empty
			).GetString(" ");

			using (var fathers = await conn.ExecuteReaderAsync(sql))
			{
				foreach (var data in await conn.GetRows(x => x.Get<string>(0), sql))
				{
					var url = $"https://db.netkeiba.com/?pid=horse_sire&id={data}&course=1&mode=1&type=0";

					using (var ped = await AppUtil.GetDocument(url))
					{
						if (ped.GetElementsByClassName("nk_tb_common race_table_01").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
						{
							if (transaction) await conn.BeginTransaction();
							foreach (var row in table.Rows.Skip(3))
							{
								var dic = new Dictionary<string, object>();

								dic["馬ID"] = data;
								dic["年度"] = row.Cells[0].GetInnerHtml();
								dic["順位"] = row.Cells[1].GetInnerHtml().GetSingle();
								dic["出走頭数"] = row.Cells[2].GetInnerHtml().GetSingle();
								dic["勝馬頭数"] = row.Cells[3].GetInnerHtml().GetSingle();
								dic["出走回数"] = row.Cells[4].GetHrefInnerHtml().GetSingle();
								dic["勝利回数"] = row.Cells[5].GetHrefInnerHtml().GetSingle();
								dic["重出"] = row.Cells[6].GetHrefInnerHtml().GetSingle();
								dic["重勝"] = row.Cells[7].GetHrefInnerHtml().GetSingle();
								dic["特出"] = row.Cells[8].GetHrefInnerHtml().GetSingle();
								dic["特勝"] = row.Cells[9].GetHrefInnerHtml().GetSingle();
								dic["平出"] = row.Cells[10].GetHrefInnerHtml().GetSingle();
								dic["平勝"] = row.Cells[11].GetHrefInnerHtml().GetSingle();
								dic["芝出"] = row.Cells[12].GetHrefInnerHtml().GetSingle();
								dic["芝勝"] = row.Cells[13].GetHrefInnerHtml().GetSingle();
								dic["ダ出"] = row.Cells[14].GetHrefInnerHtml().GetSingle();
								dic["ダ勝"] = row.Cells[15].GetHrefInnerHtml().GetSingle();
								dic["EI"] = row.Cells[17].GetInnerHtml().GetSingle();
								dic["賞金"] = row.Cells[18].GetInnerHtml().Replace(",", "").GetSingle();
								dic["芝距"] = row.Cells[19].GetInnerHtml().Replace(",", "").GetSingle();
								dic["ダ距"] = row.Cells[20].GetInnerHtml().Replace(",", "").GetSingle();

								await conn.ExecuteNonQueryAsync($"REPLACE INTO t_sanku ({dic.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", dic.Count).GetString(",")})",
									dic.Values.Select((x, i) => SQLiteUtil.CreateParameter(i < 2 ? System.Data.DbType.String : System.Data.DbType.Single, x)).ToArray()
								);
							}
							if (transaction) conn.Commit();
						}
					}
				}
			}
		}
	}
}