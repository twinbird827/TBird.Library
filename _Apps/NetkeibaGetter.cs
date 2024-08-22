using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Transactions;
using TBird.Core;
using TBird.DB.SQLite;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		private async Task<List<Dictionary<string, string>>> GetRaceResults(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var raceurl = $"https://db.netkeiba.com/race/{raceid}";

			using (var raceparser = await AppUtil.GetDocument(true, raceurl))
			{
				var racetable = raceparser.GetElementsByClassName("race_table_01 nk_tb_common").FirstOrDefault() as IHtmlTableElement;

				if (racetable == null) return arr;

				// *****
				// 「ダ左1200m / 天候 : 晴 / ダート : 良 / 発走 : 10:01」この部分を取得して分類する
				var spans = raceparser.GetElementsByTagName("span").Select(x => x.GetInnerHtml().Split('/')).First(x => 3 < x.Length);

				// 1文字目(ダ or 芝 or 障)
				var left = spans[0].Left(1);
				// 2文字目(左 or 右) TODO 障害ﾚｰｽは1文字目が"障"になってる。
				var mawari = left == "障" ? left : spans[0].Mid(1, 1);
				// 距離
				var kyori = Regex.Match(spans[0], @"\d{3,}").Value;
				// 天候
				var tenki = spans[1].Split(":")[1].Trim();
				// 馬場(芝 or ダート)
				var baba = spans[2].Split(":")[0].Trim();
				// 馬場状態
				var cond = spans[2].Split(":")[1].Trim().Replace(" ダート", "");

				// *****
				// 「2014年7月26日 3回中京7日目 2歳未勝利  (混)[指](馬齢)」この部分を取得して分類する
				var details = raceparser.GetElementsByClassName("smalltxt").First().GetInnerHtml().Split(' ');
				// 開催日
				var date = DateTime.Parse(details[0]);
				// 詳細
				var detail = details[1];
				// 場所
				var basyo = Regex.Match(detail, @"\d+回(?<basyo>.+)\d+日目").Groups["basyo"].Value;
				// ｸﾗｽ
				var clas = details[2];
				// その他
				var sonota = details.Skip(3).GetString();

				// *****
				// ﾚｰｽ名を取得
				var title = raceparser.GetElementsByClassName("mainrace_data fc").SelectMany(x => x.GetElementsByTagName("h1")).First().GetInnerHtml().Split("<")[0];

				// *****
				// 各行の処理
				foreach (var row in racetable.Rows.Skip(1))
				{
					var dic = new Dictionary<string, string>();

					// *****
					// ﾍｯﾀﾞ情報を挿入
					dic["ﾚｰｽID"] = raceid;
					dic["ﾚｰｽ名"] = title;
					dic["開催日"] = date.ToString("yyyy/MM/dd");
					dic["開催日数"] = $"{(date - DateTime.Parse("1990/01/01")).TotalDays}";
					dic["開催場所"] = basyo.Replace("1", "");
					dic["ﾗﾝｸ1"] = AppUtil.Getﾗﾝｸ1(dic["ﾚｰｽ名"], clas);
					dic["ﾗﾝｸ2"] = AppUtil.Getﾗﾝｸ2(dic["ﾗﾝｸ1"]);
					dic["回り"] = mawari;
					dic["距離"] = kyori;
					dic["天候"] = tenki;
					dic["馬場"] = baba;
					dic["馬場状態"] = cond;

					// 着順
					dic["着順"] = row.Cells[0].GetInnerHtml().Split('(')[0];
					// 着順が数値ではない場合は出走取消
					if (!int.TryParse(dic["着順"], out int def)) break;

					// 枠番
					dic["枠番"] = row.Cells[1].GetInnerHtml();
					// 馬番
					dic["馬番"] = row.Cells[2].GetInnerHtml();
					// 馬名
					dic["馬名"] = row.Cells[3].GetHrefAttribute("title");
					// 馬ID
					dic["馬ID"] = row.Cells[3].GetHrefAttribute("href").Split('/')[2];
					// 性
					dic["馬性"] = row.Cells[4].GetInnerHtml().Left(1);
					// 齢
					dic["馬齢"] = row.Cells[4].GetInnerHtml().Mid(1);
					// 斤量
					dic["斤量"] = row.Cells[5].GetInnerHtml();
					// 騎手
					dic["騎手名"] = row.Cells[6].GetHrefAttribute("title");
					// 騎手ID
					dic["騎手ID"] = row.Cells[6].GetHrefAttribute("href").Split('/')[4];
					// ﾀｲﾑ
					dic["ﾀｲﾑ"] = row.Cells[7].GetInnerHtml();
					dic["ﾀｲﾑ変換"] = dic["ﾀｲﾑ"].Split(':').Run(x => x[0].GetSingle() * 60 + x[1].GetSingle()).Str();
					// 着差
					dic["着差"] = row.Cells[8].GetInnerHtml();
					// ﾀｲﾑ指数(有料)
					dic["ﾀｲﾑ指数"] = row.Cells[9].GetInnerHtml().Replace("\n", "");
					// 通過
					dic["通過"] = row.Cells[10].GetInnerHtml();
					// 上り
					dic["上り"] = GetAgari(dic["距離"], dic["馬場"], dic["回り"] == "障", row.Cells[11].GetInnerHtml());
					// 単勝
					dic["単勝"] = row.Cells[12].GetInnerHtml();
					// 人気
					dic["人気"] = row.Cells[13].GetInnerHtml();
					// 体重
					dic["体重"] = row.Cells[14].GetInnerHtml().Split('(')[0].GetSingle(450).ToString();
					// 増減 TODO 軽量不能時はｾﾞﾛ
					dic["増減"] = row.Cells[14].GetTryCatch(s => s.Split('(')[1].Split(')')[0]);
					//// 調教ﾀｲﾑ(有料)
					//dic["調教ﾀｲﾑ"] = row.Cells[15].GetInnerHtml();
					//// 厩舎ｺﾒﾝﾄ(有料)
					//dic["厩舎ｺﾒﾝﾄ"] = row.Cells[16].GetInnerHtml();
					// 備考(有料)
					dic["備考"] = row.Cells[17].GetInnerHtml().Replace("\n", "");
					// 調教場所
					dic["調教場所"] = row.Cells[18].GetInnerHtml().Split('[')[1].Split(']')[0];
					// 調教師名
					dic["調教師名"] = row.Cells[18].GetHrefAttribute("title");
					// 調教師ID
					dic["調教師ID"] = row.Cells[18].GetHrefAttribute("href").Split('/')[4];
					// 馬主名
					dic["馬主名"] = row.Cells[19].GetHrefAttribute("title");
					// 馬主ID
					dic["馬主ID"] = row.Cells[19].GetHrefAttribute("href").Split('/')[4];
					// 賞金
					dic["賞金"] = $"{row.Cells[20].GetInnerHtml().Replace(",", "").GetSingle()}";

					// 追切情報の枠だけ用意する
					SetOikirisEmpty(dic);

					arr.Add(dic);
				}
			}

			return arr;
		}

		private string GetAgari(string 距離, string 馬場, bool 障害, string 上り)
		{
			return 障害
				? 上り.GetDouble().Divide(0.36 + 距離.GetDouble().Multiply(1.5).Divide(100000)).ToString()
				: 馬場 == "芝"
				? 上り.GetDouble().Divide(0.94 + 距離.GetDouble().Divide(20000)).ToString()
				: 上り.GetDouble().Divide(1.01 + 距離.GetDouble().Divide(20000)).ToString();
		}

		private async Task<List<Dictionary<string, string>>> GetOikiris(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			//			var url = $"https://race.netkeiba.com/race/oikiri.html?race_id={raceid}";
			var url = $"https://race.netkeiba.com/race/oikiri.html?race_id={raceid}&type=2&rf=shutuba_submenu";

			using (var raceparser = await AppUtil.GetDocument(true, url))
			{
				if (raceparser.GetElementById("All_Oikiri_Table") is IHtmlTableElement table)
				{
					foreach (var row in table.Rows.Skip(1))
					{
						var dic = new Dictionary<string, string>();

						// 枠番
						dic["枠番"] = row.Cells[0].GetInnerHtml();
						// 馬番
						dic["馬番"] = row.Cells[1].GetInnerHtml();
						// 追切場所
						dic["追切場所"] = row.Cells[5].GetInnerHtml();
						// 追切馬場
						dic["追切馬場"] = row.Cells[6].GetInnerHtml();
						// 追切騎手
						dic["追切騎手"] = row.Cells[7].GetInnerHtml();
						var li = row.Cells[8].GetElementsByTagName("li").Select(x => x.InnerHtml.Split('<')[0]).ToArray();
						// 追切時間
						dic["追切時間1"] = li[0];
						dic["追切時間2"] = li[1];
						dic["追切時間3"] = li[2];
						dic["追切時間4"] = li[3];
						dic["追切時間5"] = li[4];
						var cl = row.Cells[8].GetElementsByTagName("li").Select(x => x.GetAttribute("class") ?? "").ToArray();
						dic["追切基準1"] = cl[0];
						dic["追切基準2"] = cl[1];
						dic["追切基準3"] = cl[2];
						dic["追切基準4"] = cl[3];
						dic["追切基準5"] = cl[4];
						// 追切強さ
						dic["追切強さ"] = row.Cells[10].GetInnerHtml();
						// 追切一言
						dic["追切一言"] = row.Cells[11].GetInnerHtml();
						// 追切評価
						dic["追切評価"] = row.Cells[12].GetInnerHtml();

						arr.Add(dic);
					}
				}
			}

			return arr;
		}

		private void SetOikirisEmpty(Dictionary<string, string> dic)
		{
			dic["追切場所"] = string.Empty;
			dic["追切馬場"] = string.Empty;
			dic["追切騎手"] = string.Empty;
			dic["追切時間1"] = string.Empty;
			dic["追切時間2"] = string.Empty;
			dic["追切時間3"] = string.Empty;
			dic["追切時間4"] = string.Empty;
			dic["追切時間5"] = string.Empty;
			dic["追切基準1"] = string.Empty;
			dic["追切基準2"] = string.Empty;
			dic["追切基準3"] = string.Empty;
			dic["追切基準4"] = string.Empty;
			dic["追切基準5"] = string.Empty;
			dic["追切強さ"] = string.Empty;
			dic["追切一言"] = string.Empty;
			dic["追切評価"] = string.Empty;
		}

		private void SetOikiris(List<Dictionary<string, string>> oikiri, Dictionary<string, string> row)
		{
			var oik = oikiri.FirstOrDefault(x => x["枠番"] == row["枠番"] && x["馬番"] == row["馬番"]);
			row["追切場所"] = oik != null ? oik["追切場所"] : string.Empty;
			row["追切馬場"] = oik != null ? oik["追切馬場"] : string.Empty;
			row["追切騎手"] = oik != null ? oik["追切騎手"] : string.Empty;
			row["追切時間1"] = oik != null ? oik["追切時間1"] : string.Empty;
			row["追切時間2"] = oik != null ? oik["追切時間2"] : string.Empty;
			row["追切時間3"] = oik != null ? oik["追切時間3"] : string.Empty;
			row["追切時間4"] = oik != null ? oik["追切時間4"] : string.Empty;
			row["追切時間5"] = oik != null ? oik["追切時間5"] : string.Empty;
			row["追切基準1"] = oik != null ? oik["追切基準1"] : string.Empty;
			row["追切基準2"] = oik != null ? oik["追切基準2"] : string.Empty;
			row["追切基準3"] = oik != null ? oik["追切基準3"] : string.Empty;
			row["追切基準4"] = oik != null ? oik["追切基準4"] : string.Empty;
			row["追切基準5"] = oik != null ? oik["追切基準5"] : string.Empty;
			row["追切強さ"] = oik != null ? oik["追切強さ"] : string.Empty;
			row["追切一言"] = oik != null ? oik["追切一言"] : string.Empty;
			row["追切評価"] = oik != null ? oik["追切評価"] : string.Empty;
		}

		private async Task<List<Dictionary<string, string>>> GetRaceShutubas(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var raceurl = $"https://race.netkeiba.com/race/shutuba.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(false, raceurl))
			{
				var racetable = raceparser.GetElementsByClassName("Shutuba_Table RaceTable01 ShutubaTable").FirstOrDefault() as IHtmlTableElement;

				if (racetable == null) return arr;

				// *****
				// ﾚｰｽﾃﾞｰﾀを取得する
				var racedata01 = raceparser.GetElementsByClassName("RaceData01").First();

				// 「ダ1200m」を取得
				var babakyori = racedata01.GetElementsByTagName("span").First().GetInnerHtml().Trim();

				// 1文字目(ダ or 芝 or 障)
				var left = babakyori.Left(1);
				// 2文字目(左 or 右) TODO 障害ﾚｰｽは1文字目が"障"になってるので仮に右回りとする
				var mawari = left == "障" ? "障" : Regex.Match(racedata01.InnerHtml, @"\((?<x>.+)\)").Groups["x"].Value.Split("&nbsp;")[0].Trim();
				// 距離
				var kyori = Regex.Match(babakyori, @"\d+").Value;
				// 天候
				var tenki = Regex.Match(racedata01.InnerHtml, @"天候:(?<x>[^\<]+)").Groups["x"].Value.Split("&nbsp;")[0].Trim();
				// 馬場(芝 or ダート)
				var baba = left == "ダ" ? "ダート" : "芝";
				// 馬場状態
				var cond = Regex.Match(racedata01.InnerHtml, @"馬場:(?<x>[^\<]+)").Groups["x"].Value;

				// *****
				// 「根岸ステークス(G3) 出馬表 | 2024年1月28日 東京11R レース情報(JRA) - netkeiba.com」この部分を取得して分類する
				var details = raceparser.GetElementsByTagName("title").First().GetInnerHtml();
				// 開催日
				var date = DateTime.Parse(Regex.Match(details, @"\d+年\d+月\d+日").Value);
				// 場所
				var basyo = Regex.Match(details, @"日 (?<basyo>[^\d]+)\d+R").Groups["basyo"].Value;
				// ｸﾗｽ
				var clas = raceparser.GetElementsByClassName("RaceData02").SelectMany(x => x.GetElementsByTagName("span")).Skip(4).First().GetInnerHtml();

				// *****
				// ﾚｰｽ名を取得
				var title = details.Split(" 出馬表")[0];

				// *****
				// 各行の処理
				foreach (var row in racetable.Rows.Skip(2))
				{
					var dic = new Dictionary<string, string>();

					// *****
					// ﾍｯﾀﾞ情報を挿入
					dic["ﾚｰｽID"] = raceid;
					dic["ﾚｰｽ名"] = title;
					dic["開催日"] = date.ToString("yyyy/MM/dd");
					dic["開催日数"] = $"{(date - DateTime.Parse("1990/01/01")).TotalDays}";
					dic["開催場所"] = basyo.Replace("1", "");
					dic["ﾗﾝｸ1"] = AppUtil.Getﾗﾝｸ1(dic["ﾚｰｽ名"], clas);
					dic["ﾗﾝｸ2"] = AppUtil.Getﾗﾝｸ2(dic["ﾗﾝｸ1"]);
					dic["回り"] = mawari;
					dic["距離"] = kyori;
					dic["天候"] = tenki;
					dic["馬場"] = baba;
					dic["馬場状態"] = cond switch
					{
						"不" => "不良",
						"稍" => "稍重",
						_ => cond
					};

					dic["着順"] = "0";

					// 枠番
					dic["枠番"] = row.Cells[0].GetInnerHtml();
					// 馬番
					dic["馬番"] = row.Cells[1].GetInnerHtml();

					// 馬名
					dic["馬名"] = row.Cells[3].GetHrefAttribute("title");
					// 馬ID
					dic["馬ID"] = row.Cells[3].GetHrefAttribute("href").Split("horse/")[1];
					// 性
					dic["馬性"] = row.Cells[4].GetInnerHtml().Left(1);
					// 齢
					dic["馬齢"] = row.Cells[4].GetInnerHtml().Mid(1);
					// 斤量
					dic["斤量"] = row.Cells[5].GetInnerHtml();
					// 騎手
					dic["騎手名"] = row.Cells[6].GetHrefAttribute("title");
					// 騎手ID
					dic["騎手ID"] = row.Cells[6].GetHrefAttribute("href").Split('/').Reverse().ToArray()[1];
					// ﾀｲﾑ(なし)
					dic["ﾀｲﾑ"] = "0:00.0";
					dic["ﾀｲﾑ変換"] = dic["ﾀｲﾑ"].Split(':').Run(x => x[0].GetSingle() * 60 + x[1].GetSingle()).Str();

					// 着差(なし)
					dic["着差"] = "0";
					// ﾀｲﾑ指数(有料)
					dic["ﾀｲﾑ指数"] = "0";
					// 通過(なし)
					dic["通過"] = "0";
					// 上り(なし)
					dic["上り"] = "0";
					// 単勝(スクレイピングじゃ取れないらしい)
					dic["単勝"] = "0";
					// 人気(スクレイピングじゃ取れないらしい)
					dic["人気"] = "0";
					// 体重
					dic["体重"] = row.Cells[8].GetInnerHtml().Split('<')[0].Replace("\r\n", "").Trim();
					// 増減
					dic["増減"] = row.Cells[8].GetElementsByTagName("small").Select(x => Regex.Match(x.GetInnerHtml(), @"\((?<x>.+)\)").Groups["x"].Value).FirstOrDefault() ?? "0";
					// 備考(有料)
					dic["備考"] = "";
					// 調教場所
					dic["調教場所"] = row.Cells[7].GetElementsByTagName("span").First().GetInnerHtml() == "栗東" ? "西" : "東";
					// 調教師名
					dic["調教師名"] = row.Cells[7].GetHrefAttribute("title");
					// 調教師ID
					dic["調教師ID"] = row.Cells[7].GetHrefAttribute("href").Split('/').Reverse().ToArray()[1];

					// 馬主情報の枠だけ用意する
					dic["馬主名"] = string.Empty;
					dic["馬主ID"] = string.Empty;
					// 賞金
					dic["賞金"] = "0";

					// 追切情報の枠だけ用意する
					SetOikirisEmpty(dic);

					arr.Add(dic);
				}
			}

			return arr;
		}

		private async Task<Dictionary<string, string>> GetBanushi(string umaid)
		{
			var dic = new Dictionary<string, string>();

			using (var umaparser = await AppUtil.GetDocument(false, $"https://db.netkeiba.com/horse/{umaid}/"))
			{
				if (umaparser.GetElementsByClassName("db_prof_table no_OwnerUnit").FirstOrDefault() is IHtmlTableElement umatable1)
				{
					// 馬主名
					dic["馬主名"] = umatable1.Rows[2].Cells[1].GetHrefAttribute("title");
					// 馬主ID
					dic["馬主ID"] = umatable1.Rows[2].Cells[1].GetHrefAttribute("href").Split('/')[2];
				}
				else if (umaparser.GetElementsByClassName("db_prof_table ").FirstOrDefault() is IHtmlTableElement umatable2)
				{
					// 馬主名
					dic["馬主名"] = umatable2.Rows[2].Cells[1].GetHrefAttribute("title");
					// 馬主ID
					dic["馬主ID"] = umatable2.Rows[2].Cells[1].GetHrefAttribute("href").Split('/')[2];
				}
			}

			return dic;
		}

		private async Task<List<Dictionary<string, string>>> GetTyakujun(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var url = $"https://race.netkeiba.com/race/result.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(false, url))
			{
				if (raceparser.GetElementsByClassName("RaceTable01 RaceCommon_Table ResultRefund Table_Show_All").FirstOrDefault() is IHtmlTableElement table)
				{
					foreach (var row in table.Rows.Skip(1))
					{
						var dic = new Dictionary<string, string>();

						// 枠番
						dic["枠番"] = row.Cells[1].GetInnerHtml();
						// 馬番
						dic["馬番"] = row.Cells[2].GetInnerHtml();
						// 着順
						dic["着順"] = row.Cells[0].GetInnerHtml();

						arr.Add(dic);
					}
				}
			}

			return arr;
		}

		private async Task<Dictionary<string, string>> GetPayout(string raceid)
		{
			var dic = new Dictionary<string, string>();

			var url = $"https://race.netkeiba.com/race/result.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(false, url))
			{
				if (raceparser.GetElementsByClassName("Payout_Detail_Table").FirstOrDefault(x => x.GetAttribute("summary") == "ワイド") is IHtmlTableElement table1)
				{
					// ﾚｰｽID
					dic["ﾚｰｽID"] = raceid;
					// 三連複
					dic["三連複"] = GetPayout(table1, "Fuku3");
					// 三連単
					dic["三連単"] = GetPayout(table1, "Tan3");
					// ワイド
					dic["ワイド"] = GetPayout(table1, "Wide");
					// 馬単
					dic["馬単"] = GetPayout(table1, "Umatan");
				}

				if (raceparser.GetElementsByClassName("Payout_Detail_Table").FirstOrDefault(x => x.GetAttribute("summary") == "払戻し") is IHtmlTableElement table2)
				{
					// 馬連
					dic["馬連"] = GetPayout(table2, "Umaren");
					// 単勝
					dic["単勝"] = GetPayout(table2, "Tansho", "div", "span");
				}
			}

			return dic;
		}

		private string GetPayout(IHtmlTableElement table, string tag, string ul = "ul", string li = "li")
		{
			var result = table.GetElementsByClassName(tag)
				.OfType<IHtmlTableRowElement>()
				.SelectMany(x => x.GetElementsByClassName("Result"))
				.SelectMany(x => x.GetElementsByTagName(ul))
				.Select(x => x.GetElementsByTagName(li).Select(y => y.GetInnerHtml()).Where(x => !string.IsNullOrEmpty(x)).GetString("-"))
				.ToArray();

			var payout = table.GetElementsByClassName(tag)
				.OfType<IHtmlTableRowElement>()
				.SelectMany(x => x.GetElementsByClassName("Payout"))
				.Select(x => x.GetInnerHtml())
				.SelectMany(x => x.Split("<br />"))
				.SelectMany(x => x.Split("<br>"))
				.Select(x => x.Replace("円", "").Replace(",", ""))
				.ToArray();

			return Enumerable.Range(0, Arr(result.Length, payout.Length).Min())
				.Select(i => $"{result[i]},{payout[i]}")
				.GetString(";");
		}

		private async IAsyncEnumerable<Dictionary<string, string>> GetKetto(string uma)
		{
			var url = $"https://db.netkeiba.com/horse/ped/{uma}/";

			yield return new Dictionary<string, string>()
			{
				{ "馬ID", uma },
				{ "父ID", string.Empty },
				{ "母ID", string.Empty }
			};

			using (var ped = await AppUtil.GetDocument(false, url))
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

					yield return new Dictionary<string, string>()
					{
						{ "馬ID", uma },
						{ "父ID", f },
						{ "母ID", m }
					};

					var rowspan08 = table.GetElementsByTagName("td").Where(x => x.GetAttribute("rowspan").GetInt32() == 8).ToArray();
					var ff = func(rowspan08, 0);
					var fm = func(rowspan08, 1);

					yield return new Dictionary<string, string>()
					{
						{ "馬ID", f },
						{ "父ID", ff },
						{ "母ID", fm }
					};

					var mf = func(rowspan08, 2);
					var mm = func(rowspan08, 3);

					yield return new Dictionary<string, string>()
					{
						{ "馬ID", m },
						{ "父ID", mf },
						{ "母ID", mm }
					};
				}
			}
		}

		private async IAsyncEnumerable<Dictionary<string, object>> GetSanku(string uma)
		{
			var url = $"https://db.netkeiba.com/?pid=horse_sire&id={uma}&course=1&mode=1&type=0";

			using (var ped = await AppUtil.GetDocument(false, url))
			{
				if (ped.GetElementsByClassName("nk_tb_common race_table_01").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
				{
					foreach (var row in table.Rows.Skip(3))
					{
						var dic = new Dictionary<string, object>();

						dic["馬ID"] = uma;
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

						yield return dic;
					}
				}
			}

		}

		private async Task<IEnumerable<string>> GetCurrentRaceUrls()
		{
			return await GetCurrentRaceIds(DateTime.Now).RunAsync(arr =>
			{
				return arr
					.Select(x => x.Left(10))
					.Distinct()
					.Select(x => $"https://race.netkeiba.com/race/shutuba.html?race_id={x}01");
			});
		}

		private async Task<IEnumerable<string>> GetCurrentRaceIds(DateTime date)
		{
			return await GetRaceIds(date).RunAsync(async arr =>
			{
				return arr.Any() ? arr : await GetCurrentRaceIds(date.AddDays(1));
			});
		}

		private async Task<IEnumerable<string>> GetRecentRaceIds(int start, int end)
		{
			var now = DateTime.Now.ToString("yyyyMMdd").GetInt32();

			Enumerable.Range(start, end - start + 1);

			var dates = await Enumerable.Range(start, end - start + 1).Select(y =>
			{
				return Enumerable.Range(1, y < end ? 12 : DateTime.Now.Month)
					.Select(m => GetKaisaiDate(y, m))
					.WhenAllExpand();
			}).WhenAllExpand();

			return await dates
				.Where(d => d.GetInt32() < now)
				.Select(date => GetRaceIds(DateTime.ParseExact(date, "yyyyMMdd", null)))
				.WhenAllExpand();
		}

		private async Task<IEnumerable<string>> GetRaceIds(DateTime date)
		{
			var url = $"https://race.netkeiba.com/top/race_list_sub.html?kaisai_date={date.ToString("yyyyMMdd")}";

			using (var ped = await AppUtil.GetDocument(false, url))
			{
				return ped
					.GetElementsByTagName("a")
					.Select(x => x.GetAttribute("href"))
					.Select(x => Regex.Match(x ?? string.Empty, @"race_id=(?<x>[\d]+)"))
					.Where(x => x.Success && x.Groups["x"].Success)
					.Select(x => x.Groups["x"].Value)
					.Distinct();
			}
		}

		private async Task<IEnumerable<string>> GetKaisaiDate(int year, int month)
		{
			var url = $"https://race.netkeiba.com/top/calendar.html?year={year}&month={month}";

			using (var ped = await AppUtil.GetDocument(false, url))
			{
				var arr = ped
					.GetElementsByTagName("a")
					.Select(x => x.GetAttribute("href"))
					.Select(x => Regex.Match(x ?? string.Empty, @"kaisai_date=(?<x>[\d]+)"))
					.Where(x => x.Success && x.Groups["x"].Success)
					.Select(x => x.Groups["x"].Value)
					.Distinct();

				return arr;
			}
		}
	}
}