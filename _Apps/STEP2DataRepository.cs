using HorseRacingPrediction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public interface IDataRepository
	{
		Task<List<Race>> GetRacesAsync(DateTime startDate, DateTime endDate);

		Task<List<RaceResultData>> GetRaceResultsAsync(string raceId);

		Task<List<RaceResult>> GetHorseHistoryBeforeAsync(string horseName, DateTime beforeDate);

		Task<HorseDetails> GetHorseDetailsAsync(string horseName, DateTime asOfDate);

		List<RaceResult> GetJockeyRecentRaces(string jockey, DateTime asOfDate, int count);

		List<RaceResult> GetTrainerRecentRaces(string trainer, DateTime asOfDate, int count);

	}

	public class SQLiteRepository : TBirdObject, IDataRepository
	{
		private List<RaceData> _allRaceData = new();
		private List<HorseData> _allHorseData = new();

		public SQLiteRepository()
		{

		}

		protected override void DisposeManagedResource()
		{
			base.DisposeManagedResource();

			_allRaceData.Clear();
			_allHorseData.Clear();
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
				.Select(r => new RaceResult(r, _allRaceData))
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

		public List<RaceResult> GetJockeyRecentRaces(string jockey, DateTime asOfDate, int count)
		{
			var raceHistory = _allRaceData
				.Where(r => r.JockeyName == jockey && r.RaceDate < asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.ToList();

			return raceHistory
				.Select(r => new RaceResult(r, _allRaceData))
				.ToList();
		}

		public List<RaceResult> GetTrainerRecentRaces(string trainer, DateTime asOfDate, int count)
		{
			var raceHistory = _allRaceData
				.Where(r => r.TrainerName == trainer && r.RaceDate < asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.ToList();

			return raceHistory
				.Select(r => new RaceResult(r, _allRaceData))
				.ToList();
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

}