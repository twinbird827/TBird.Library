using HorseRacingPrediction;
using Microsoft.ML.TorchSharp.Roberta;
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
		List<Race> GetRacesAsync(DateTime startDate, DateTime endDate);

		List<RaceData> GetRaceResultsAsync(string raceId);

		List<RaceResult> GetHorseHistoryBeforeAsync(string horseName, DateTime beforeDate);

		HorseDetails GetHorseDetailsAsync(string horseName, DateTime asOfDate);

		List<RaceResult> GetJockeyRecentRaces(string jockey, DateTime asOfDate, int count);

		List<RaceResult> GetTrainerRecentRaces(string trainer, DateTime asOfDate, int count);

		List<HorseData> GetHorseDatasInRace(string raceId);

		float GetTrainerStats(string trainer, DateTime asOfDate);

		float GetJockeyStats(string jockey, DateTime asOfDate);

		float GetSireStats(string sire);

		float GetBreederStats(string breeder, DateTime asOfDate);

		float GetSireQuality(string sire);

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

				MainViewModel.AddLog($"読み込み完了: レース {_allRaceData.Count} 件, 馬 {_allHorseData.Count} 件");

			}
		}

		public List<Race> GetRacesAsync(DateTime startDate, DateTime endDate)
		{
			return _allRaceData
				.Where(r => r.RaceDate >= startDate && r.RaceDate <= endDate)
				.GroupBy(r => r.RaceId)
				.Select(g => g.First())
				.Select(r => new Race(r, _allRaceData))
				.ToList();
		}

		public List<RaceData> GetRaceResultsAsync(string raceId)
		{
			return _allRaceData
				.Where(r => r.RaceId == raceId)
				.OrderBy(r => r.FinishPosition)
				.ToList();
		}

		public List<RaceResult> GetHorseHistoryBeforeAsync(string horseName, DateTime beforeDate)
		{
			return _allRaceData
				.Where(r => r.HorseName == horseName && r.RaceDate < beforeDate)
				.OrderByDescending(r => r.RaceDate)
				.Select(r => new RaceResult(r, _allRaceData))
				.ToList();
		}

		public HorseDetails GetHorseDetailsAsync(string horseName, DateTime asOfDate)
		{
			var horseData = _allHorseData.First(h => h.Name == horseName);
			var raceHistory = _allRaceData
				.Where(r => r.HorseName == horseName && r.RaceDate < asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.ToList();

			return new HorseDetails(horseData, raceHistory, asOfDate);
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

		public List<HorseData> GetHorseDatasInRace(string raceId)
		{
			var horses = _allRaceData
				.Where(r => r.RaceId == raceId)
				.Select(r => _allHorseData.First(horse => horse.Name == r.HorseName))
				.ToList();

			return horses;
		}

		public float GetTrainerStats(string trainer, DateTime asOfDate)
		{
			var raceHistory = _allRaceData
				.Where(r => r.TrainerName == trainer && r.RaceDate < asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.Select(r => new RaceResult(r, _allRaceData))
				.Where(r => r.HorseExperience == 0)
				.ToList();

			return raceHistory.Average(x => x.AdjustedInverseScore);
		}

		public float GetJockeyStats(string jockey, DateTime asOfDate)
		{
			var raceHistory = _allRaceData
				.Where(r => r.JockeyName == jockey && r.RaceDate < asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.Select(r => new RaceResult(r, _allRaceData))
				.Where(r => r.HorseExperience == 0)
				.ToList();

			return raceHistory.Average(x => x.AdjustedInverseScore);
		}

		public float GetSireStats(string sire)
		{
			var raceHistory = _allRaceData
				.Where(r => r.HorseName == sire)
				.OrderByDescending(r => r.RaceDate)
				.Select(r => new RaceResult(r, _allRaceData))
				.ToList();

			return raceHistory.Average(x => x.AdjustedInverseScore);
		}

		public float GetBreederStats(string breeder, DateTime asOfDate)
		{
			var breedHorses = _allHorseData
				.Where(x => x.BreederName == breeder && x.BirthDate.AddYears(2) < asOfDate)
				.Select(x => x.Name)
				.ToArray();
			var raceHistory = _allRaceData
				.Where(r => breedHorses.Contains(r.HorseName) && r.RaceDate < asOfDate)
				.OrderByDescending(r => r.RaceDate)
				.Select(r => new RaceResult(r, _allRaceData))
				.Where(r => r.HorseExperience == 0)
				.ToList();

			return raceHistory.Average(x => x.AdjustedInverseScore);
		}

		public float GetSireQuality(string sire) => GetSireStats(sire);

	}

}