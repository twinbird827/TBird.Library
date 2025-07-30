using HorseRacingPrediction;
using System;
using System.Collections.Generic;
using System.Data;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;
using TBird.DB;
using MathNet.Numerics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using System.Windows.Media.Media3D;

namespace Netkeiba
{
	public class STEP2Command : STEPBase
	{
		public STEP2Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var create = VM.S2Overwrite.IsChecked || !await conn.ExistsModelTableAsync();

				if (create)
				{
					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropSTEP2();
				}

				// ﾃｰﾌﾞﾙ作成
				await conn.CreateModel();
			}
		}
	}

	public class SQLiteRepository : IDataRepository
	{
		private List<RaceData> _allRaceData = new();
		private List<HorseData> _allHorseData = new();
		private List<ConnectionData> _allConnectionData = new();

		public SQLiteRepository()
		{

		}

		public async Task LoadDataAsync()
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var days = (DateTime.Now.AddYears(-7) - DateTime.Parse("1990/01/01")).TotalDays.Int32();

				// レースデータの読み込み
				_allRaceData = conn.GetRaceDataAsync(days).ToBlockingEnumerable().ToList();

				// 馬データの読み込み
				_allHorseData = conn.GetHorseDataAsync().ToBlockingEnumerable().ToList();

				// 関係者データの読み込み
				var connectionsPath = Path.Combine(_dataDirectory, "connections.csv");

				MainViewModel.AddLog($"読み込み完了: レース {_allRaceData.Count} 件, 馬 {_allHorseData.Count} 件, 関係者 {_allConnectionData.Count} 件");

			}
		}

		public async Task<List<Race>> GetRacesAsync(DateTime startDate, DateTime endDate)
		{
			return _allRaceData
				.Where(r => r.RaceDate >= startDate && r.RaceDate <= endDate)
				.GroupBy(r => r.RaceId)
				.Select(g => g.First())
				.Select(r => new Race
				{
					RaceId = r.RaceId,
					CourseName = r.CourseName,
					Distance = r.Distance,
					TrackType = r.TrackType,
					TrackCondition = r.TrackCondition,
					Grade = r.Grade,
					FirstPrizeMoney = r.FirstPrizeMoney,
					NumberOfHorses = r.NumberOfHorses,
					RaceDate = r.RaceDate,
					AverageRating = CalculateAverageRating(r.RaceId),
					IsInternational = r.Grade == "G1" && r.FirstPrizeMoney > 200000000,
					IsAgedHorseRace = r.Grade != "新馬" && r.Grade != "未勝利"
				})
				.ToList();
		}

		public async Task<List<RaceResultData>> GetRaceResultsAsync(string raceId)
		{
			return _allRaceData
				.Where(r => r.RaceId == raceId)
				.Select(r => new RaceResultData
				{
					RaceId = r.RaceId,
					HorseName = r.HorseName,
					FinishPosition = r.FinishPosition,
					Weight = r.Weight,
					Time = r.Time,
					Odds = r.Odds,
					JockeyName = r.JockeyName,
					TrainerName = r.TrainerName,
					RaceDate = r.RaceDate
				})
				.OrderBy(r => r.FinishPosition)
				.ToList();
		}

		public async Task<List<RaceResult>> GetHorseHistoryBeforeAsync(string horseName, DateTime beforeDate)
		{
			return _allRaceData
				.Where(r => r.HorseName == horseName && r.RaceDate < beforeDate)
				.OrderByDescending(r => r.RaceDate)
				.Select(r => new RaceResult
				{
					FinishPosition = r.FinishPosition,
					Time = r.Time,
					TotalHorses = r.NumberOfHorses,
					RaceDate = r.RaceDate,
					HorseExperience = CalculateHorseExperience(r.HorseName, r.RaceDate),
					Race = new Race
					{
						RaceId = r.RaceId,
						Distance = r.Distance,
						TrackType = r.TrackType,
						TrackCondition = r.TrackCondition,
						Grade = r.Grade,
						CourseName = r.CourseName,
						FirstPrizeMoney = r.FirstPrizeMoney,
						NumberOfHorses = r.NumberOfHorses,
						RaceDate = r.RaceDate
					}
				})
				.ToList();
		}

		public async Task<HorseDetails> GetHorseDetailsAsync(string horseName, DateTime asOfDate)
		{
			var horseData = _allHorseData.FirstOrDefault(h => h.Name == horseName);
			var raceHistory = _allRaceData
				.Where(r => r.HorseName == horseName && r.RaceDate < asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.ToList();

			return new HorseDetails
			{
				Name = horseName,
				Age = CalculateAge(horseData?.BirthDate ?? DateTime.Now.AddYears(-4), asOfDate),
				PreviousWeight = raceHistory.Skip(1).FirstOrDefault()?.Weight ?? 456,
				SireName = horseData?.SireName ?? "Unknown",
				DamSireName = horseData?.DamSireName ?? "Unknown",
				BreederName = horseData?.BreederName ?? "Unknown",
				LastRaceDate = raceHistory.FirstOrDefault()?.RaceDate ?? DateTime.MinValue,
				PurchasePrice = horseData?.PurchasePrice ?? 10000000,
				RaceCount = raceHistory.Count
			};
		}

		public async Task<ConnectionDetails> GetConnectionsAsync(string horseName, DateTime asOfDate)
		{
			// 最新の関係者情報を取得
			var activeConnection = _allConnectionData
				.Where(c => c.HorseName == horseName && c.IsActive)
				.OrderByDescending(c => c.FromDate)
				.FirstOrDefault();

			if (activeConnection != null)
			{
				return new ConnectionDetails
				{
					JockeyName = activeConnection.JockeyName,
					TrainerName = activeConnection.TrainerName,
					AsOfDate = asOfDate
				};
			}

			// 関係者データがない場合、最新のレース結果から取得
			var latestRace = _allRaceData
				.Where(r => r.HorseName == horseName && r.RaceDate <= asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.FirstOrDefault();

			return new ConnectionDetails
			{
				JockeyName = latestRace?.JockeyName ?? "Unknown",
				TrainerName = latestRace?.TrainerName ?? "Unknown",
				AsOfDate = asOfDate
			};
		}

		private float CalculateAverageRating(string raceId)
		{
			// 簡易レーティング計算（実際の実装では詳細な計算を行う）
			var raceHorses = _allRaceData.Where(r => r.RaceId == raceId).ToList();
			if (!raceHorses.Any()) return 80.0f;

			// オッズから逆算した強さ指標
			var avgOdds = raceHorses.Average(h => h.Odds);
			return Math.Max(70.0f, Math.Min(95.0f, 100.0f - (float)Math.Log(avgOdds) * 5.0f));
		}

		private int CalculateHorseExperience(string horseName, DateTime raceDate)
		{
			return _allRaceData
				.Count(r => r.HorseName == horseName && r.RaceDate < raceDate);
		}

		private int CalculateAge(DateTime birthDate, DateTime asOfDate)
		{
			var age = asOfDate.Year - birthDate.Year;
			if (asOfDate.DayOfYear < birthDate.DayOfYear) age--;
			return Math.Max(2, Math.Min(age, 10)); // 2-10歳の範囲に制限
		}
	}

	public static partial class SQLite3Extensions
	{
		/// <summary>
		/// 教育ﾃﾞｰﾀ作成用ﾃｰﾌﾞﾙを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task DropSTEP2(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
		}

		/// <summary>馬ﾍｯﾀﾞ</summary>
		private static readonly string[] col_model = Arr("ﾚｰｽID", "ﾚｰｽ名", "開催日", "開催日数", "開催場所", "ﾗﾝｸ1", "ﾗﾝｸ2", "回り", "距離", "天候", "馬場", "馬場状態");

		public static async Task CreateModel(this SQLiteControl conn)
		{
			await conn.Create(
				"t_model",
				col_model,
				Arr("ﾚｰｽID")
			);

			// TODO indexの作成
		}

		public static async Task<bool> ExistsModelTableAsync(this SQLiteControl conn)
		{
			return await conn.ExistsColumn("t_model", "ﾚｰｽID");
		}

		public static async Task<bool> ExistsModelAsync(this SQLiteControl conn, string raceid)
		{
			var cnt = await conn.ExecuteScalarAsync(
				"SELECT COUNT(*) FROM t_orig_h WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			).RunAsync(x => x.GetInt32());
			return 0 < cnt;
		}

		public static async IAsyncEnumerable<RaceData> GetRaceDataAsync(this SQLiteControl conn, int days)
		{
			var sql = "SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.頭数, h.開催日, d.馬名, d.着順, d.体重, d.ﾀｲﾑ変換, d.単勝, d.騎手ID, d.調教師ID FROM t_orig_h h, t_orig_d d WHERE h.開催日数 > ? AND h.ﾚｰｽID = d.ﾚｰｽID";

			foreach (var x in await conn.GetRows(sql, SQLiteUtil.CreateParameter(DbType.Int32, days)))
			{
				yield return new RaceData()
				{
					RaceId = x["ﾚｰｽID"].Str(),
					CourseName = x["ﾚｰｽ名"].Str(),
					Distance = x["距離"].Int32(),
					TrackType = x["馬場"].Str(),
					TrackCondition = x["馬場状態"].Str(),
					Grade = x["ﾗﾝｸ1"].Str(),
					FirstPrizeMoney = x["優勝賞金"].Int64(),
					NumberOfHorses = x["頭数"].Int32(),
					RaceDate = x["開催日"].Date(),
					HorseName = x["馬名"].Str(),
					FinishPosition = x["着順"].Int32(),
					Weight = x["体重"].Single(),
					Time = x["ﾀｲﾑ変換"].Single(),
					Odds = x["単勝"].Single(),
					JockeyName = x["騎手ID"].Str(),
					TrainerName = x["調教師ID"].Str()
				};
			}
		}

		public static async IAsyncEnumerable<HorseData> GetHorseDataAsync(this SQLiteControl conn)
		{
			var sql = "SELECT 馬ID, 馬名, 誕生日, 購入額, 馬主ID, 父ID, 母父ID FROM t_uma";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new HorseData()
				{
					Name = x["馬ID"].Str(),
					BirthDate = x["誕生日"].Date(),
					SireName = x["父ID"].Str(),
					DamSireName = x["母父ID"].Str(),
					BreederName = x["馬主ID"].Str(),
					PurchasePrice = x["購入額"].Int64()
				};
			}
		}

		public static async IAsyncEnumerable<ConnectionData> GetConnectionDataAsync(this SQLiteControl conn)
		{
			var sql = "SELECT 馬ID, 馬名, 誕生日, 購入額, 馬主ID, 父ID, 母父ID FROM t_uma";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new ConnectionData()
				{
					Name = x["馬ID"].Str(),
					BirthDate = x["誕生日"].Date(),
					SireName = x["父ID"].Str(),
					DamSireName = x["母父ID"].Str(),
					BreederName = x["馬主ID"].Str(),
					PurchasePrice = x["購入額"].Int64()
				};
			}
		}

	}

}