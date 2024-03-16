﻿using System;
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
using AngleSharp.Html.Dom;

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
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 = 0");
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
										var racearr = await GetRaceResults(raceid).RunAsync(async arr =>
										{
											if (arr.Count != 0)
											{
												var oikiri = await GetOikiris(raceid);

												arr.ForEach(row =>
												{
													var oik = oikiri.FirstOrDefault(x => x["枠番"] == row["枠番"] && x["馬番"] == row["馬番"]);
													row["一言"] = oik != null ? oik["一言"] : string.Empty;
													row["追切"] = oik != null ? oik["追切"] : string.Empty;
												});
											}
										});

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
												{ "馬ID", new[] { "開催場所", "回り", "天候", "馬場", "馬場状態" } },
												{ "騎手ID", new[] { "開催場所", "回り", "天候", "馬場", "馬場状態" } },
												{ "調教師ID", new[] { "開催場所" } },
												{ "馬主ID", new[] { "開催場所"} },
											};
											int index = 0;
											foreach (var k in indexes)
											{
												await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index{index++.ToString(2)} ON t_orig ({k.Key}, 開催日数, ﾗﾝｸ2, 着順)");
												foreach (var v in k.Value)
												{
													await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index{index++.ToString(2)} ON t_orig ({k.Key}, 開催日数, ﾗﾝｸ2, 着順, {v})");
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

		private async Task<List<Dictionary<string, string>>> GetRaces2(string raceid)
		{
			var arr = new List<Dictionary<string, string>>();

			var raceurl = $"https://race.netkeiba.com/race/shutuba.html?race_id={raceid}";

			using (var raceparser = await AppUtil.GetDocument(raceurl))
			{
				var racetable = raceparser.GetElementsByClassName("Shutuba_Table RaceTable01 ShutubaTable").FirstOrDefault() as AngleSharp.Html.Dom.IHtmlTableElement;

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
	}
}