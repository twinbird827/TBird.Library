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

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public CheckboxItemModel S2Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

		public IRelayCommand S2EXEC => RelayCommand.Create(async _ =>
		{
			using (var conn = CreateSQLiteControl())
			{
				var rac = await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT ﾚｰｽID FROM t_orig ORDER BY ﾚｰｽID DESC");

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = rac.Count;

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

				// 数値型のﾘｽﾄ取得
				// 平均値を取得
				Func<IEnumerable<Dictionary<string, object>>, string, double> func_avg = (arr, n) =>
				{
					return arr.Select(x => x[n].GetDoubleNaN()).Where(x => !double.IsNaN(x)).Average();
				};

				var create = S2Overwrite.IsChecked || !await conn.ExistsColumn("t_model", "着順");

				foreach (var raceid in rac)
				{
					MessageService.Debug($"ﾚｰｽID:開始:{raceid}");

					// ﾚｰｽ毎の纏まり
					var racarr = await CreateRaceModel(conn, raceid, ﾗﾝｸ1, ﾗﾝｸ2, 馬性, 調教場所, 一言, 追切);

					if (create)
					{
						create = false;

						// ﾃｰﾌﾞﾙ作成
						await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
						await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_model (" + racarr.First().Keys.GetString(",") + ", PRIMARY KEY (ﾚｰｽID, 馬番))");
					}

					await conn.BeginTransaction();
					foreach (var ins in racarr)
					{
						await conn.ExecuteNonQueryAsync(
							$"INSERT INTO t_model ({ins.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", ins.Keys.Count).GetString(",")})",
							ins.Keys.Select(k => SQLiteUtil.CreateParameter(System.Data.DbType.Double, ins[k])).ToArray()
						);
					}
					conn.Commit();

					AddLog($"Step5 Proccess ﾚｰｽID: {raceid}");

					Progress.Value += 1;
				}

				MessageService.Info("Step5 Completed!!");
			}
		});

		private async Task<List<Dictionary<string, double>>> CreateRaceModel(SQLiteControl conn, string raceid, List<string> ﾗﾝｸ1, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 一言, List<string> 追切)
		{
			Func<IEnumerable<Dictionary<string, object>>, string, double> func_avg = (arr, n) =>
			{
				return arr.Select(x => x[n].GetDoubleNaN()).Where(x => !double.IsNaN(x)).Average();
			};

			// ﾚｰｽ毎の纏まり
			var racarr = new List<Dictionary<string, double>>();

			// 同ﾚｰｽの平均を取りたいときに使用する
			var 同ﾚｰｽ = await conn.GetRows("SELECT * FROM t_orig WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(System.Data.DbType.String, raceid)
			);

			foreach (var src in 同ﾚｰｽ)
			{
				MessageService.Debug($"ﾚｰｽ内 foreach:開始:{raceid}");

				// 対象ﾘｽﾄから全件, 勝利, 連対, 複勝を取得
				Func<string, string, Task<Dictionary<string, double>>> func_getdata = async (keyname, headname) =>
				{
					var now = $"{src["開催日数"]}".GetDouble();
					var keyvalue = $"{src[keyname]}";
					var headvalue = $"{src[headname]}";

					var tgt = await conn.GetRows($"" +
						$" SELECT" +
						$"   COUNT(*)                                    RA0," +
						$"   COUNT(CASE WHEN t_orig.着順 = 1 THEN 1 END) RA1," +
						$"   COUNT(CASE WHEN t_orig.着順 = 2 THEN 1 END) RA2," +
						$"   COUNT(CASE WHEN t_orig.着順 = 3 THEN 1 END) RA3," +
						$"" +
						$"   COUNT(CASE WHEN (t_where.開催日数 - t_where.直近日数) < t_orig.開催日数 THEN 1 END)                      RN0," +
						$"   COUNT(CASE WHEN (t_where.開催日数 - t_where.直近日数) < t_orig.開催日数 AND t_orig.着順 = 1 THEN 1 END)  RN1," +
						$"   COUNT(CASE WHEN (t_where.開催日数 - t_where.直近日数) < t_orig.開催日数 AND t_orig.着順 = 2 THEN 1 END)  RN2," +
						$"   COUNT(CASE WHEN (t_where.開催日数 - t_where.直近日数) < t_orig.開催日数 AND t_orig.着順 = 3 THEN 1 END)  RN3" +
						$" FROM" +
						$"   (SELECT ? {keyname}, ? {headname}, ? 開催日数, 365 直近日数) t_where" +
						$"   INNER JOIN t_orig" +
						$"   ON t_orig.{keyname} = t_where.{keyname} AND t_orig.{headname} = t_where.{headname} AND t_orig.開催日数 < t_where.開催日数",
						SQLiteUtil.CreateParameter(System.Data.DbType.String, keyvalue),
						SQLiteUtil.CreateParameter(System.Data.DbType.String, headvalue),
						SQLiteUtil.CreateParameter(System.Data.DbType.Int64, now)
					);

					var ra0 = $"{tgt[0]["RA0"]}".GetDouble();
					var ra1 = $"{tgt[0]["RA1"]}".GetDouble();
					var ra2 = $"{tgt[0]["RA2"]}".GetDouble();
					var ra3 = $"{tgt[0]["RA3"]}".GetDouble();

					var rn0 = $"{tgt[0]["RN0"]}".GetDouble();
					var rn1 = $"{tgt[0]["RN1"]}".GetDouble();
					var rn2 = $"{tgt[0]["RN2"]}".GetDouble();
					var rn3 = $"{tgt[0]["RN3"]}".GetDouble();

					var dic = new Dictionary<string, double>();

					dic[$"全勝_{keyname}_{headname}"] = ra0 == 0 ? 0 : ra1 / ra0;
					dic[$"全連_{keyname}_{headname}"] = ra0 == 0 ? 0 : (ra1 + ra2) / ra0;
					dic[$"全複_{keyname}_{headname}"] = ra0 == 0 ? 0 : (ra1 + ra2 + ra3) / ra0;

					dic[$"直勝_{keyname}_{headname}"] = rn0 == 0 ? 0 : rn1 / rn0;
					dic[$"直連_{keyname}_{headname}"] = rn0 == 0 ? 0 : (rn1 + rn2) / rn0;
					dic[$"直複_{keyname}_{headname}"] = rn0 == 0 ? 0 : (rn1 + rn2 + rn3) / rn0;

					return dic;
				};

				var dic = new Dictionary<string, double>();

				// ﾍｯﾀﾞ情報
				dic["ﾚｰｽID"] = src["ﾚｰｽID"].GetDouble();
				dic["開催日数"] = src["開催日数"].GetDouble();
				dic["ﾗﾝｸ1"] = ﾗﾝｸ1.IndexOf(src["ﾗﾝｸ1"]);
				dic["ﾗﾝｸ2"] = ﾗﾝｸ2.IndexOf(src["ﾗﾝｸ2"]);

				// 予測したいﾃﾞｰﾀ
				dic["着順"] = src["着順"].GetDouble();

				// 馬毎に違う情報
				dic["枠番"] = src["枠番"].GetDouble();
				dic["馬番"] = src["馬番"].GetDouble();
				dic["馬ID"] = src["馬ID"].GetDouble();
				dic["馬性"] = 馬性.IndexOf(src["馬性"]);
				dic["馬齢"] = src["馬齢"].GetDouble();
				dic["斤量"] = src["斤量"].GetDouble();
				dic["斤量差"] = dic["斤量"].GetDouble() - func_avg(同ﾚｰｽ, "斤量");
				dic["単勝"] = src["単勝"].GetDouble();
				dic["人気"] = src["人気"].GetDouble();
				dic["体重"] = src["体重"].GetDoubleNaN();
				dic["増減"] = src["増減"].GetDouble();
				dic["体重差"] = double.IsNaN(dic["体重"]) ? double.NaN : dic["体重"] - func_avg(同ﾚｰｽ, "体重");
				dic["増減割"] = double.IsNaN(dic["体重"]) ? double.NaN : dic["増減"] / dic["体重"];
				dic["斤量割"] = double.IsNaN(dic["体重"]) ? double.NaN : dic["斤量"] / dic["体重"];
				dic["調教場所"] = 調教場所.IndexOf(src["調教場所"]);
				dic["一言"] = 一言.IndexOf(src["一言"]);
				dic["追切"] = 追切.IndexOf(src["追切"]);

				// 馬の成績を追加する→過去ﾚｰｽから算出する
				dic.AddRange(await func_getdata("馬ID", "馬ID"));
				dic.AddRange(await func_getdata("馬ID", "開催場所"));
				dic.AddRange(await func_getdata("馬ID", "ﾗﾝｸ1"));
				dic.AddRange(await func_getdata("馬ID", "ﾗﾝｸ2"));
				dic.AddRange(await func_getdata("馬ID", "回り"));
				dic.AddRange(await func_getdata("馬ID", "天候"));
				dic.AddRange(await func_getdata("馬ID", "馬場"));
				dic.AddRange(await func_getdata("馬ID", "馬場状態"));

				// 騎手の成績を追加する→過去ﾚｰｽから算出する
				dic.AddRange(await func_getdata("騎手ID", "騎手ID"));
				dic.AddRange(await func_getdata("騎手ID", "開催場所"));
				dic.AddRange(await func_getdata("騎手ID", "ﾗﾝｸ1"));
				dic.AddRange(await func_getdata("騎手ID", "ﾗﾝｸ2"));
				dic.AddRange(await func_getdata("騎手ID", "回り"));
				dic.AddRange(await func_getdata("騎手ID", "天候"));
				dic.AddRange(await func_getdata("騎手ID", "馬場"));
				dic.AddRange(await func_getdata("騎手ID", "馬場状態"));

				// 調教師の成績を追加する→過去ﾚｰｽから算出する
				dic.AddRange(await func_getdata("調教師ID", "調教師ID"));
				dic.AddRange(await func_getdata("調教師ID", "開催場所"));
				dic.AddRange(await func_getdata("調教師ID", "ﾗﾝｸ1"));
				dic.AddRange(await func_getdata("調教師ID", "ﾗﾝｸ2"));

				// 馬主の成績を追加する→過去ﾚｰｽから算出する
				dic.AddRange(await func_getdata("馬主ID", "馬主ID"));
				dic.AddRange(await func_getdata("馬主ID", "開催場所"));
				dic.AddRange(await func_getdata("馬主ID", "ﾗﾝｸ1"));
				dic.AddRange(await func_getdata("馬主ID", "ﾗﾝｸ2"));

				var 馬情報 = await conn.GetRows(
						$" SELECT t_orig.*, t_top.ﾀｲﾑ TOPﾀｲﾑ" +
						$" FROM t_orig" +
						$" LEFT OUTER JOIN t_orig t_top ON t_orig.ﾚｰｽID = t_top.ﾚｰｽID AND t_orig.開催日数 = t_top.開催日数 AND t_top.着順 = 1" +
						$" WHERE t_orig.馬ID = ? AND t_orig.開催日数 < ?",
						SQLiteUtil.CreateParameter(System.Data.DbType.String, src["馬ID"]),
						SQLiteUtil.CreateParameter(System.Data.DbType.Int64, src["開催日数"])
				);

				// 得意距離、及び今回のﾚｰｽ距離との差
				dic["距離"] = src["距離"].GetDouble();
				dic["馬_得意距離"] = 馬情報.Select(x => x["距離"].GetDouble()).Average(dic["距離"]);
				dic["馬_距離差"] = dic["馬_得意距離"] - dic["距離"];

				// 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
				Func<object, double> func_tuka = v => $"{v}".Split('-').Select(x => x.GetDouble()).Average(同ﾚｰｽ.Count);
				dic["通過"] = func_tuka(src["通過"]);
				dic["通過平均"] = 馬情報.Select(x => func_tuka(x["通過"])).Average(同ﾚｰｽ.Count / 2.0);

				// 通過順の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
				dic["上り"] = src["上り"].GetDouble();
				dic["上り平均"] = 馬情報.Select(x => x["上り"].GetDouble()).Average(double.NaN);

				// ﾀｲﾑの平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
				Func<object, double> func_time = v => $"{v}".Split(':')[0].GetDouble() * 60 + $"{v}".Split(':')[1].GetDouble();
				dic["時間"] = src["距離"].GetDouble() / func_time(src["ﾀｲﾑ"]);
				dic["時間平均"] = 馬情報.Select(x => x["距離"].GetDouble() / func_time(x["ﾀｲﾑ"])).Average(double.NaN);

				// 1着との差
				dic["TOP時間差"] = 馬情報.Select(x => func_time(x["ﾀｲﾑ"]) - func_time(x["TOPﾀｲﾑ"])).Average(double.NaN);

				// 着差の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
				Func<object, double> func_tyaku = v => CoreUtil.Nvl(
					$"{v}"
					.Replace("ハナ", "0.05")
					.Replace("アタマ", "0.10")
					.Replace("クビ", "0.15")
					.Replace("同着", "0")
					.Replace("大", "15")
					.Replace("1/4", "25")
					.Replace("1/2", "50")
					.Replace("3/4", "75"), "-0.25")
					.Split('+').Sum(x => double.Parse(x));
				dic["着差"] = func_tyaku(src["着差"]);
				dic["着差平均"] = 馬情報.Select(x => func_tyaku(x["着差"])).Average(double.NaN);

				racarr.Add(dic);

				MessageService.Debug($"ﾚｰｽ内 foreach:終了:{raceid}");
			}

			// 他の馬との比較
			racarr.ForEach(dic => dic["通過平均差"] = dic["通過平均"] - racarr.Select(x => x["通過平均"]).Where(x => !double.IsNaN(x)).Average(double.NaN));
			racarr.ForEach(dic => dic["上り平均差"] = dic["上り平均"] - racarr.Select(x => x["上り平均"]).Where(x => !double.IsNaN(x)).Average(double.NaN));
			racarr.ForEach(dic => dic["時間平均差"] = dic["時間平均"] - racarr.Select(x => x["時間平均"]).Where(x => !double.IsNaN(x)).Average(double.NaN));
			racarr.ForEach(dic => dic["着差平均差"] = dic["着差平均"] - racarr.Select(x => x["着差平均"]).Where(x => !double.IsNaN(x)).Average(double.NaN));

			return racarr;
		}
	}
}