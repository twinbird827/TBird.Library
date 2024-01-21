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

			if (create || File.Exists(_sqlitepath))
			{
				FileUtil.BeforeCreate(_sqlitepath);
			}

			using (var conn = CreateSQLiteControl())
			{
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
										if (cnt.GetDouble() == 0) break;
									}

									try
									{
										var racearr = await GetRaces(raceid);

										if (racearr.Count == 0) break;

										if (create)
										{
											create = false;

											// ﾃｰﾌﾞﾙ作成
											await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_orig (" + racearr.First().Keys.GetString(",") + ", PRIMARY KEY (開催日数, 着順))");

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
				var ﾗﾝｸ2 = new Dictionary<string, string>()
				{
					{ "G1", "RANK1" },
					{ "G2", "RANK2" },
					{ "G3", "RANK2" },
					{ "G4", "RANK3" },
					{ "OP", "RANK3" },
					{ "3勝", "RANK4" },
					{ "1600万下", "RANK4" },
					{ "2勝", "RANK5" },
					{ "1000万下", "RANK5" },
					{ "1勝", "RANK5" },
					{ "500万下", "RANK5" },
					{ "未勝利", "RANK6" },
					{ "新馬", "RANK7" },
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
					dic["詳細"] = detail;
					dic["開催場所"] = basyo;
					dic["ｸﾗｽ"] = clas;
					dic["ﾗﾝｸ"] = ﾗﾝｸ.FirstOrDefault(dic["ｸﾗｽ"].Contains) ?? ﾗﾝｸ.FirstOrDefault(dic["ﾚｰｽ名"].Contains) ?? string.Empty;
					dic["ﾗﾝｸ1"] = dic["ﾗﾝｸ"].Replace("(G)", "G4").Replace("(L)", "OP").Replace("オープン", "OP").Replace(")", "");
					dic["ﾗﾝｸ2"] = ﾗﾝｸ2[dic["ﾗﾝｸ1"]];
					dic["その他"] = sonota;
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
					// ﾀｲﾑ指数(有料)
					dic["ﾀｲﾑ指数_有料"] = "**";
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
					// 調教ﾀｲﾑ(有料)
					dic["調教ﾀｲﾑ_有料"] = "**";
					// 厩舎ｺﾒﾝﾄ(有料)
					dic["厩舎ｺﾒﾝﾄ_有料"] = "**";
					// 備考(有料)
					dic["備考_有料"] = "**";
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
	}
}