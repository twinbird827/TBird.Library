using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Wpf.Collections;
using TBird.Wpf;
using TBird.Core;
using System.IO;
using TBird.DB.SQLite;
using System.Data;
using TBird.DB;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		public BindableCollection<ComboboxItemModel> LogSource { get; } = new BindableCollection<ComboboxItemModel>();

		public BindableContextCollection<ComboboxItemModel> Logs { get; }

		public BindableCollection<CheckboxItemModel> BasyoSources { get; } = new BindableCollection<CheckboxItemModel>();

		public BindableContextCollection<CheckboxItemModel> Basyos { get; }

		public int SYear
		{
			get => _SYear;
			set => SetProperty(ref _SYear, value);
		}
		private int _SYear;

		public int EYear
		{
			get => _EYear;
			set => SetProperty(ref _EYear, value);
		}
		private int _EYear;

		public CheckboxItemModel S1Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

		public IRelayCommand S1EXEC => RelayCommand.Create(async _ =>
		{
			var years = Enumerable.Range(SYear, EYear - SYear).Select(i => i.ToString(2)).ToArray();
			var basyos = BasyoSources.Where(x => x.IsChecked).ToArray();
			var counts = Enumerable.Range(1, 6).Select(i => i.ToString(2)).ToArray();
			var days = Enumerable.Range(1, 12).Select(i => i.ToString(2)).ToArray();
			var races = Enumerable.Range(1, 12).Select(i => i.ToString(2)).ToArray();

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = years.Length * counts.Length * days.Length;

			bool create = S1Overwrite.IsChecked || !File.Exists(_sqlitepath);

			if (create)
			{
				FileUtil.BeforeCreate(_sqlitepath);
			}

			using (var conn = CreateSQLiteControl())
			{
				if (await conn.ExistsColumn("t_orig", "ﾚｰｽID"))
				{
					await conn.BeginTransaction();
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 IS NULL");
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 = ''");
					conn.Commit();
				}

				foreach (var y in years)
				{
					foreach (var c in counts)
					{
						foreach (var d in days)
						{
							Progress.Value += 1;

							foreach (var b in basyos)
							{
								foreach (var r in races)
								{
									// raceid = year + basyo + count + day + race
									var raceid = $"{y}{b.Value}{c}{d}{r}";

									if (await conn.ExistsColumn("t_orig", "ﾚｰｽID"))
									{
										var cnt = await conn.ExecuteScalarAsync(
											$"SELECT COUNT(*) FROM t_orig WHERE ﾚｰｽID = ?",
											SQLiteUtil.CreateParameter(DbType.String, raceid)
										);
										if (0 < cnt.GetDouble()) break;
									}

									try
									{
										var racearr = await GetRaces(raceid);

										if (racearr.Count == 0) break;

										if (create)
										{
											create = false;

											var integers = new[] { "開催日数", "着順" };
											var keynames = racearr.First().Keys.Select(x => integers.Contains(x) ? $"{x} INTEGER" : $"{x} TEXT");
											// ﾃｰﾌﾞﾙ作成
											await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_orig (" + keynames.GetString(",") + ", PRIMARY KEY (ﾚｰｽID, 馬番))");

											// ｲﾝﾃﾞｯｸｽ作成
											var indexes = new Dictionary<string, string[]>()
											{
												{ "馬ID", new[] { "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2", "回り", "天候", "馬場", "馬場状態" } },
												{ "騎手ID", new[] { "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2", "回り", "天候", "馬場", "馬場状態" } },
												{ "調教師ID", new[] { "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2" } },
												{ "馬主ID", new[] { "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2" } },
											};
											int index = 0;
											foreach (var k in indexes)
											{
												await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index{index++.ToString(2)} ON t_orig ({k.Key}, 開催日数)");
												foreach (var v in k.Value)
												{
													await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index{index++.ToString(2)} ON t_orig ({k.Key}, {v}, 開催日数)");
												}
											}
										}

										await conn.BeginTransaction();
										foreach (var x in racearr)
										{
											var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
											var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
											await conn.ExecuteNonQueryAsync(sql, prm);
										}
										conn.Commit();

										AddLog($"year: {y}, count: {c}, day:{d}, basyo:{b.Display}, race: {r}R, raceid: {raceid}");
									}
									catch (Exception ex)
									{
										MessageService.Exception(ex);
									}
								}
							}
						}
					}
				}
			}

			MessageService.Info("Step1 Completed!!");
		});

		private async Task<List<Dictionary<string, string>>> GetRaces(string raceid)
		{
			var ﾗﾝｸ = new[] { "G1)", "G2)", "G3)", "(G)", "(L)", "オープン", "3勝", "1600万下", "2勝", "1000万下", "1勝", "500万下", "未勝利", "新馬" };

			var arr = new List<Dictionary<string, string>>();

			var raceurl = $"https://db.netkeiba.com/race/{raceid}";

			using (var raceparser = await AppUtil.GetDocument(raceurl))
			{
				var racetable = raceparser.GetElementsByClassName("race_table_01 nk_tb_common").FirstOrDefault() as AngleSharp.Html.Dom.IHtmlTableElement;
				var ﾗﾝｸ1 = new Dictionary<string, string>()
				{
					{ "G1)", "G1" },
					{ "G2)", "G2" },
					{ "G3)", "G3" },
					{ "(G)", "オープン" },
					{ "(L)", "オープン" },
					{ "オープン", "オープン" },
					{ "3勝", "3勝" },
					{ "1600万下", "3勝" },
					{ "2勝", "2勝" },
					{ "1000万下", "2勝" },
					{ "1勝", "1勝" },
					{ "500万下", "1勝" },
					{ "未勝利", "未勝利" },
					{ "新馬", "新馬" },
					{ "", "2勝" },
				};
				var ﾗﾝｸ2 = new Dictionary<string, string>()
				{
					{ "G1", "RANK1" },
					{ "G2", "RANK1" },
					{ "G3", "RANK1" },
					{ "オープン", "RANK2" },
					{ "3勝", "RANK3" },
					{ "2勝", "RANK3" },
					{ "1勝", "RANK3" },
					{ "未勝利", "RANK4" },
					{ "新馬", "RANK5" },
				};

				if (racetable == null) return arr;

				// 追切情報を取得する
				var oikiris = await GetOikiris(raceid);

				// *****
				// 「ダ左1200m / 天候 : 晴 / ダート : 良 / 発走 : 10:01」この部分を取得して分類する
				var spans = raceparser.GetElementsByTagName("span").Select(x => x.GetInnerHtml().Split('/')).First(x => 3 < x.Length);

				// 1文字目(ダ or 芝 or 障)
				var left = spans[0].Left(1);
				// 2文字目(左 or 右) TODO 障害ﾚｰｽは1文字目が"障"になってる。
				var mawari = left == "障" ? left : spans[0].Mid(1, 1);
				// 距離
				var kyori = Regex.Match(spans[0], @"\d+").Value;
				// 天候
				var tenki = spans[1].Split(":")[1].Trim();
				// 馬場(芝 or ダート)
				var baba = spans[2].Split(":")[0].Trim();
				// 馬場状態
				var cond = spans[2].Split(":")[1].Trim();

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
					dic["ﾗﾝｸ1"] = ﾗﾝｸ1[ﾗﾝｸ.FirstOrDefault(clas.Contains) ?? ﾗﾝｸ.FirstOrDefault(dic["ﾚｰｽ名"].Contains) ?? string.Empty];
					dic["ﾗﾝｸ2"] = ﾗﾝｸ2[dic["ﾗﾝｸ1"]];
					dic["回り"] = mawari;
					dic["距離"] = kyori;
					dic["天候"] = tenki;
					dic["馬場"] = baba;
					dic["馬場状態"] = cond;

					// 着順
					dic["着順"] = row.Cells[0].GetInnerHtml();
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
					dic["上り"] = row.Cells[11].GetInnerHtml();
					// 単勝
					dic["単勝"] = row.Cells[12].GetInnerHtml();
					// 人気
					dic["人気"] = row.Cells[13].GetInnerHtml();
					// 体重
					dic["体重"] = row.Cells[14].GetInnerHtml().Split('(')[0];
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

					// 追切情報を追加
					var oikiri = oikiris.Where(x => x["枠番"] == dic["枠番"] && x["馬番"] == dic["馬番"]).FirstOrDefault();
					dic["一言"] = oikiri != null ? oikiri["一言"] : string.Empty;
					dic["追切"] = oikiri != null ? oikiri["追切"] : string.Empty;

					arr.Add(dic);
				}
			}

			return arr;
		}

		private async Task<List<Dictionary<string, string>>> GetRaces2(string raceid)
		{
			var ﾗﾝｸ = new[] { "G1)", "G2)", "G3)", "(G)", "(L)", "オープン", "３勝クラス", "1600万下", "２勝クラス", "1000万下", "１勝クラス", "500万下", "未勝利", "新馬" };

			var arr = new List<Dictionary<string, string>>();

			var raceurl = $"https://race.netkeiba.com/race/shutuba.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(raceurl))
			{
				var racetable = raceparser.GetElementsByClassName("Shutuba_Table RaceTable01 ShutubaTable").FirstOrDefault() as AngleSharp.Html.Dom.IHtmlTableElement;
				var ﾗﾝｸ1 = new Dictionary<string, string>()
				{
					{ "G1)", "G1" },
					{ "G2)", "G2" },
					{ "G3)", "G3" },
					{ "(G)", "オープン" },
					{ "(L)", "オープン" },
					{ "オープン", "オープン" },
					{ "３勝クラス", "3勝" },
					{ "1600万下", "3勝" },
					{ "２勝クラス", "2勝" },
					{ "1000万下", "2勝" },
					{ "１勝クラス", "1勝" },
					{ "500万下", "1勝" },
					{ "未勝利", "未勝利" },
					{ "新馬", "新馬" },
					{ "", "2勝" },
				};
				var ﾗﾝｸ2 = new Dictionary<string, string>()
				{
					{ "G1", "RANK1" },
					{ "G2", "RANK1" },
					{ "G3", "RANK1" },
					{ "オープン", "RANK2" },
					{ "3勝", "RANK3" },
					{ "2勝", "RANK3" },
					{ "1勝", "RANK3" },
					{ "未勝利", "RANK4" },
					{ "新馬", "RANK5" },
				};

				if (racetable == null) return arr;

				// 追切情報を取得する
				var oikiris = await GetOikiris(raceid);

				// 着順情報を取得する
				var tyakujun = await GetTyakujun(raceid);

				// *****
				// ﾚｰｽﾃﾞｰﾀを取得する
				var racedata01 = raceparser.GetElementsByClassName("RaceData01").First();

				// 「ダ1200m」を取得
				var babakyori = racedata01.GetElementsByTagName("span").First().GetInnerHtml();

				// 1文字目(ダ or 芝 or 障)
				var left = babakyori.Left(1);
				// 2文字目(左 or 右) TODO 障害ﾚｰｽは1文字目が"障"になってるので仮に右回りとする
				var mawari = Regex.Match(racedata01.InnerHtml, @"\((?<x>.+)\)").Groups["x"].Value; mawari = mawari == "障" ? "右" : mawari;
				// 距離
				var kyori = Regex.Match(babakyori, @"\d+").Value;
				// 天候
				var tenki = Regex.Match(racedata01.InnerHtml, @"天候:(?<x>[^\<]+)").Groups["x"].Value;
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
				var basyo = Regex.Match(details, @"日 (?<basyo>.+)\d+R").Groups["basyo"].Value;
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
					dic["ﾗﾝｸ1"] = ﾗﾝｸ1[ﾗﾝｸ.FirstOrDefault(dic["ﾚｰｽ名"].Contains) ?? ﾗﾝｸ.FirstOrDefault(clas.Contains) ?? string.Empty];
					dic["ﾗﾝｸ2"] = ﾗﾝｸ2[dic["ﾗﾝｸ1"]];
					dic["回り"] = mawari;
					dic["距離"] = kyori;
					dic["天候"] = tenki;
					dic["馬場"] = baba;
					dic["馬場状態"] = cond;

					// 着順
					var tyaku = tyakujun.Where(x => x["枠番"] == row.Cells[0].GetInnerHtml() && x["馬番"] == row.Cells[1].GetInnerHtml()).FirstOrDefault();
					dic["着順"] = tyaku != null ? tyaku["着順"] : "0";

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
					dic["体重"] = row.Cells[8].GetInnerHtml().Split('<')[0];
					// 増減
					dic["増減"] = row.Cells[8].GetElementsByTagName("small").Select(x => Regex.Match(x.GetInnerHtml(), @"\((?<x>.+)\)").Groups["x"].Value).FirstOrDefault() ?? "0";
					// 調教場所
					dic["調教場所"] = row.Cells[7].GetElementsByTagName("span").First().GetInnerHtml() == "栗東" ? "西" : "東";
					// 調教師名
					dic["調教師名"] = row.Cells[7].GetHrefAttribute("title");
					// 調教師ID
					dic["調教師ID"] = row.Cells[7].GetHrefAttribute("href").Split('/').Reverse().ToArray()[1];

					using (var umaparser = await AppUtil.GetDocument($"https://db.netkeiba.com/horse/{dic["馬ID"]}/"))
					{
						if (umaparser.GetElementsByClassName("db_prof_table no_OwnerUnit").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement umatable)
						{
							// 馬主名
							dic["馬主名"] = umatable.Rows[2].Cells[1].GetHrefAttribute("title");
							// 馬主ID
							dic["馬主ID"] = umatable.Rows[2].Cells[1].GetHrefAttribute("href").Split('/')[2];
						}
					}

					// 追切情報を追加
					var oikiri = oikiris.Where(x => x["枠番"] == dic["枠番"] && x["馬番"] == dic["馬番"]).FirstOrDefault();
					dic["一言"] = oikiri != null ? oikiri["一言"] : string.Empty;
					dic["追切"] = oikiri != null ? oikiri["追切"] : string.Empty;

					arr.Add(dic);
				}
			}

			return arr;
		}

		private async Task<List<Dictionary<string, string>>> GetOikiris(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var url = $"https://race.netkeiba.com/race/oikiri.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(url))
			{
				if (raceparser.GetElementById("All_Oikiri_Table") is AngleSharp.Html.Dom.IHtmlTableElement table)
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

		private async Task<List<Dictionary<string, string>>> GetTyakujun(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var url = $"https://race.netkeiba.com/race/result.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(url))
			{
				if (raceparser.GetElementsByClassName("RaceTable01 RaceCommon_Table ResultRefund Table_Show_All").FirstOrDefault() is AngleSharp.Html.Dom.IHtmlTableElement table)
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
	}
}