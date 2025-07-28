using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;
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
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var racebases = await NetkeibaGetter.GetRecentRaceIds(SYear, EYear, await conn.GetLastMonth()).RunAsync(races =>
				{
					return races
						.Select(x => x.Left(10))
						.Distinct()
						.ToArray();
				});

				Progress.Value = 0;
				Progress.Minimum = 0;
				Progress.Maximum = racebases.Length;

				bool create = S1Overwrite.IsChecked || !File.Exists(AppUtil.Sqlitepath);

				if (create)
				{
					DirectoryUtil.Create(Path.GetDirectoryName(AppUtil.Sqlitepath));
				}

				if (create)
				{
					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropAllTablesAsync();
				}

				// 欠落ﾃﾞｰﾀを除外
				await conn.RemoveShortageMissingDatasAsync();

				await Task.Delay(1000);

				foreach (var racebase in racebases)
				{
					if (create == false) await conn.BeginTransaction();
					await foreach (var racearr in GetSTEP1Racearrs(conn, racebase))
					{
						if (racearr.Any(x => x["回り"] != "障" && string.IsNullOrEmpty(x["ﾀｲﾑ指数"]))) continue;

						create = await conn.CreateOrigAndBeginTransaction(create);

						await conn.InsertOrigAsync(racearr);
					}
					conn.Commit();

					AddLog($"completed racebase:{racebase}");

					Progress.Value += 1;
				}

				// 血統情報の作成
				await RefreshKetto(conn);

				//// 産駒成績の更新
				//await RefreshSanku(conn);
			}

			MessageService.Info("Step1 Completed!!");
		});

		private async Task<bool> ExistsOrig(SQLiteControl conn, string raceid)
		{
			if (await conn.ExistsColumn("t_orig", "ﾚｰｽID"))
			{
				var cnt = await conn.ExecuteScalarAsync(
					$"SELECT COUNT(*) FROM t_orig WHERE ﾚｰｽID = ?",
					SQLiteUtil.CreateParameter(DbType.String, raceid)
				);
				return 0 < cnt.GetDouble();
			}
			return false;
		}

		private async Task<List<Dictionary<string, string>>> GetSTEP1Racearr(SQLiteControl conn, string raceid)
		{
			return await NetkeibaGetter.GetRaceResults(raceid).RunAsync(async arr =>
			{
				if (arr.Count != 0)
				{
					var oikiri = await NetkeibaGetter.GetOikiris(raceid);

					arr.ForEach(row => NetkeibaGetter.SetOikiris(oikiri, row));
				}
			});
		}

		private async IAsyncEnumerable<List<Dictionary<string, string>>> GetSTEP1Racearrs(SQLiteControl conn, string racebase)
		{
			var race01 = $"{racebase}01";
			if (await ExistsOrig(conn, race01)) yield break;

			var race01arr = await GetSTEP1Racearr(conn, race01);
			if (race01arr.Count == 0) yield break;

			yield return race01arr;

			var racearrs = Enumerable.Range(2, 11).Select(i =>
			{
				return GetSTEP1Racearr(conn, $"{racebase}{i.ToString(2)}");
			});

			foreach (var racearr in racearrs)
			{
				yield return await racearr;
			}
		}
	}
}