using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using Tensorflow.Keras.Layers;

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
		private SQLiteControl _conn;
		private List<RaceData> _allRaceData = new();
		private List<HorseData> _allHorseData = new();

		public SQLiteRepository()
		{
			_conn = AppUtil.CreateSQLiteControl();
		}

		protected override void DisposeManagedResource()
		{
			base.DisposeManagedResource();

			_allRaceData.Clear();
			_allHorseData.Clear();
			_conn.Dispose();
		}

		public async Task LoadDataAsync()
		{
			// レースデータの読み込み
			_allRaceData = _conn.GetRaceDataAsync(DateTime.Now.AddYears(-7).ToTotalDays()).ToBlockingEnumerable().ToList();

			// 馬データの読み込み
			_allHorseData = _conn.GetHorseDataAsync().ToBlockingEnumerable().ToList();

			MainViewModel.AddLog($"読み込み完了: レース {_allRaceData.Count} 件, 馬 {_allHorseData.Count} 件");

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
			var raceHistory = _conn.GetJockeyRecentRaces(jockey, asOfDate, count).ToBlockingEnumerable()
				.OrderByDescending(r => r.RaceDate)
				.ToList();

			return raceHistory
				.Select(r => new RaceResult(r, _allRaceData))
				.ToList();
		}

		public List<RaceResult> GetTrainerRecentRaces(string trainer, DateTime asOfDate, int count)
		{
			var raceHistory = _conn.GetTrainerRecentRaces(trainer, asOfDate, count).ToBlockingEnumerable()
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
			var raceHistory = GetTrainerRecentRaces(trainer, asOfDate, 999)
				.OrderByDescending(r => r.RaceDate)
				.Where(r => r.HorseExperience == 0)
				.ToList();

			return raceHistory.Average(x => x.AdjustedInverseScore);
		}

		public float GetJockeyStats(string jockey, DateTime asOfDate)
		{
			var raceHistory = GetJockeyRecentRaces(jockey, asOfDate, 999)
				.OrderByDescending(r => r.RaceDate)
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

	public static partial class SQLite3Extensions
	{

		public static async IAsyncEnumerable<RaceData> GetRaceDataAsync(this SQLiteControl conn, int days)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.頭数, h.開催日, u.馬ID, d.着順, d.体重, d.ﾀｲﾑ変換, d.単勝, d.騎手ID, d.調教師ID
FROM t_orig_h h, t_orig_d d, t_uma u
WHERE h.開催日数 > ? AND h.ﾚｰｽID = d.ﾚｰｽID AND d.馬ID = u.馬ID
			";

			foreach (var x in await conn.GetRows(sql, SQLiteUtil.CreateParameter(DbType.Int32, days)))
			{
				yield return new RaceData(x);
			}
		}

		public static async IAsyncEnumerable<HorseData> GetHorseDataAsync(this SQLiteControl conn)
		{
			var sql = @"
with v_uma AS (SELECT 馬ID, 馬名, 生年月日, MAX(セリ取引価格*1, 募集情報*0.7) 購入額, 馬主ID, 父ID, 母父ID FROM t_uma),
v_uma0 AS (SELECT 父ID, 母父ID, AVG(購入額) 購入額 FROM v_uma WHERE 購入額 > 0 GROUP BY 父ID, 母父ID),
v_uma1 AS (SELECT 父ID, AVG(購入額) 購入額 FROM v_uma WHERE 購入額 > 0 GROUP BY 父ID),
v_uma2 AS (SELECT 母父ID, AVG(購入額) 購入額 FROM v_uma WHERE 購入額 > 0 GROUP BY 母父ID),
v_uma3 AS (SELECT 馬主ID, AVG(購入額) 購入額 FROM v_uma WHERE 購入額 > 0 GROUP BY 馬主ID)
SELECT 馬ID, 馬名, 生年月日, (CASE WHEN v_uma.購入額>0 THEN v_uma.購入額 ELSE COALESCE(
v_uma0.購入額*0.9,
v_uma1.購入額*0.8,
v_uma2.購入額*0.7,
v_uma3.購入額*0.6
) END) 購入額, v_uma.馬主ID, v_uma.父ID, v_uma.母父ID
FROM v_uma
LEFT JOIN v_uma0 ON v_uma.父ID = v_uma0.父ID AND v_uma.母父ID = v_uma0.母父ID
LEFT JOIN v_uma1 ON v_uma.父ID = v_uma1.父ID
LEFT JOIN v_uma2 ON v_uma.母父ID = v_uma2.母父ID
LEFT JOIN v_uma3 ON v_uma.馬主ID = v_uma3.馬主ID
			";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new HorseData(x);
			}
		}

		public static async IAsyncEnumerable<RaceData> GetJockeyRecentRaces(this SQLiteControl conn, string jockey, DateTime asOfDate, int count)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.頭数, h.開催日, u.馬ID, d.着順, d.体重, d.ﾀｲﾑ変換, d.単勝, d.騎手ID, d.調教師ID
FROM t_orig_h h, t_orig_d d, t_uma u
WHERE h.ﾚｰｽID = d.ﾚｰｽID AND d.馬ID = u.馬ID AND d.騎手ID = ? AND h.開催日数 < ?
			";

			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, jockey),
				SQLiteUtil.CreateParameter(DbType.Int32, asOfDate.ToTotalDays())
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				yield return new RaceData(x);
			}
		}

		public static async IAsyncEnumerable<RaceData> GetTrainerRecentRaces(this SQLiteControl conn, string trainer, DateTime asOfDate, int count)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.頭数, h.開催日, u.馬ID, d.着順, d.体重, d.ﾀｲﾑ変換, d.単勝, d.騎手ID, d.調教師ID
FROM t_orig_h h, t_orig_d d, t_uma u
WHERE h.ﾚｰｽID = d.ﾚｰｽID AND d.馬ID = u.馬ID AND d.調教師ID = ? AND h.開催日数 < ?
			";

			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, trainer),
				SQLiteUtil.CreateParameter(DbType.Int32, asOfDate.ToTotalDays())
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				yield return new RaceData(x);
			}
		}

	}

}