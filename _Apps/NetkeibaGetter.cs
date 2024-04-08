using AngleSharp.Html.Dom;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		private async Task<List<Dictionary<string, string>>> GetRaceResults(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var raceurl = $"https://db.netkeiba.com/race/{raceid}";

			using (var raceparser = await AppUtil.GetDocument(raceurl))
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
					dic["開催場所"] = basyo;
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
					// 着差
					dic["着差"] = row.Cells[8].GetInnerHtml();
					//// ﾀｲﾑ指数(有料)
					//dic["ﾀｲﾑ指数_有料"] = "**";
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
					//dic["調教ﾀｲﾑ_有料"] = "**";
					//// 厩舎ｺﾒﾝﾄ(有料)
					//dic["厩舎ｺﾒﾝﾄ_有料"] = "**";
					//// 備考(有料)
					//dic["備考_有料"] = "**";
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
					dic["一言"] = string.Empty;
					dic["追切"] = string.Empty;

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

			var url = $"https://race.netkeiba.com/race/oikiri.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(url))
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
						// 一言
						dic["一言"] = row.Cells[4].GetInnerHtml();
						// 評価
						dic["追切"] = row.Cells[5].GetInnerHtml();

						arr.Add(dic);
					}
				}
			}

			return arr;
		}

		private async Task<List<Dictionary<string, string>>> GetRaceShutubas(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var raceurl = $"https://race.netkeiba.com/race/shutuba.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(raceurl))
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
					dic["開催場所"] = basyo;
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
					// 着差(なし)
					dic["着差"] = "0";
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
					dic["一言"] = string.Empty;
					dic["追切"] = string.Empty;

					arr.Add(dic);
				}
			}

			return arr;
		}

		private async Task<Dictionary<string, string>> GetBanushi(string umaid)
		{
			var dic = new Dictionary<string, string>();

			using (var umaparser = await AppUtil.GetDocument($"https://db.netkeiba.com/horse/{umaid}/"))
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

			using (var raceparser = await AppUtil.GetDocument(url))
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

			using (var raceparser = await AppUtil.GetDocument(url))
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

	}
}