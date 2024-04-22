﻿using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Wpf.Collections;

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
			var years = Enumerable.Range(SYear, EYear - SYear + 1).Select(i => i.ToString(2)).ToArray();
			var basyos = BasyoSources.Where(x => x.IsChecked).ToArray();
			var counts = Enumerable.Range(1, 6).Select(i => i.ToString(2)).ToArray();
			var days = Enumerable.Range(1, 12).Select(i => i.ToString(2)).ToArray();
			var races = Enumerable.Range(1, 12).Select(i => i.ToString(2)).ToArray();

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = years.Length * counts.Length * days.Length;

			bool create = S1Overwrite.IsChecked || !File.Exists(AppUtil.Sqlitepath);

			if (create)
			{
				FileUtil.BeforeCreate(AppUtil.Sqlitepath);
			}

			using (var conn = AppUtil.CreateSQLiteControl())
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
													row["調教場所"] = oik != null ? oik["調教場所"] : string.Empty;
													row["調教馬場"] = oik != null ? oik["調教馬場"] : string.Empty;
													row["調教騎手"] = oik != null ? oik["調教騎手"] : string.Empty;
													row["調教1"] = oik != null ? oik["調教1"] : string.Empty;
													row["調教2"] = oik != null ? oik["調教2"] : string.Empty;
													row["調教3"] = oik != null ? oik["調教3"] : string.Empty;
													row["調教4"] = oik != null ? oik["調教4"] : string.Empty;
													row["調教5"] = oik != null ? oik["調教5"] : string.Empty;
													row["調教強さ"] = oik != null ? oik["調教強さ"] : string.Empty;
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
											await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_shutuba (" + keynames.GetString(",") + ", PRIMARY KEY (ﾚｰｽID, 馬番))");

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

				// 血統情報の作成
				await RefreshKetto(conn);

				// 産駒成績の更新
				await RefreshSanku(conn, true, await conn.GetRows(r => r.Get<string>(0), "SELECT DISTINCT 馬ID FROM t_orig WHERE 馬ID NOT IN (SELECT 馬ID FROM t_sanku)"));
			}

			MessageService.Info("Step1 Completed!!");
		});
	}
}