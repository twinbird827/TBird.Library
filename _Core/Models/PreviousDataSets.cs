using Codeplex.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;

namespace Netkeiba.Models
{
	public partial class PreviousDataSets
	{
		private static PreviousDataSets _PDS = new();

		private PreviousDataSets()
		{

		}

		public static async Task Initialize(SQLiteControl conn)
		{
			void SetTrackConditionDistances(TrackConditionDistance[] tracks) => tracks.ForEach(x => _PDS._TrackConditionDistances.Add(_PDS.GetTrackConditionDistance(x), x));

			_PDS.Clear();

			SetTrackConditionDistances(await conn.GetTrackDistanceAsync().ToArrayAsync());
		}

		public static async Task Initialize(SQLiteControl conn, DateTime date)
		{
			await Initialize(conn);

			await _PDS.InitializeHistory(conn, date);
		}

		public static List<RaceDetail> GetHorses(RaceDetail x) => GetHorses(x.Horse, x);

		public static List<RaceDetail> GetHorses(string x, RaceDetail detail) => _PDS.GetMaster(detail, _PDS.GetHorse(x));

		public static List<RaceDetail> GetJockeys(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockey(x));

		public static List<RaceDetail> GetJockeyPlaces(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockeyPlace(x));

		public static List<RaceDetail> GetJockeyTracks(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockeyTrack(x));

		public static List<RaceDetail> GetTrainers(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetTrainer(x));

		public static List<RaceDetail> GetJockeyTrainers(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockeyTrainer(x));

		public static List<RaceDetail> GetBreeders(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetBreeder(x));

		public static List<RaceDetail> GetTrainerBreeders(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetTrainerBreeder(x));

		public static List<RaceDetail> GetSires(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSire(x));

		public static List<RaceDetail> GetDamSires(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetDamSire(x));

		public static List<RaceDetail> GetSireDamSires(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSireDamSire(x));

		public static TrackConditionDistance GetTrackConditionDistances(RaceDetail x) => _PDS._TrackConditionDistances.Get(_PDS.GetTrackConditionDistance(x.Race), TrackConditionDistance.Default);

		public static void AddHistory(RaceDetail x)
		{
			void AddHistory(Dictionary<string, List<RaceDetail>> dic, RaceDetail tgt, string key)
			{
				if (!dic.ContainsKey(key))
				{
					dic.Add(key, new List<RaceDetail>());
				}
				dic[key].Insert(0, tgt);
				if (dic[key].Count > 500)
				{
					dic[key].RemoveAt(500 - 1);
				}
			}

			_PDS.GetKeyArray(x).ForEach(key =>
			{
				AddHistory(_PDS._master[key.Key], x, key.Value);
			});
		}

	}

	public partial class PreviousDataSets
	{
		private Dictionary<string, List<RaceDetail>>[] _master = Enumerable
			.Range(0, 15)
			.Select(i => new Dictionary<string, List<RaceDetail>>())
			.ToArray();

		private KeyValuePair<int, string> GetHorse(string horse) => new KeyValuePair<int, string>(0, $"H{horse}");

		private KeyValuePair<int, string> GetJockey(RaceDetail x) => new KeyValuePair<int, string>(1, $"J{x.Jockey}");

		private KeyValuePair<int, string> GetJockeyPlace(RaceDetail x) => new KeyValuePair<int, string>(2, $"J{x.Jockey}-JP{x.Race.Place}");

		private KeyValuePair<int, string> GetJockeyTrack(RaceDetail x) => new KeyValuePair<int, string>(3, $"J{x.Jockey}-JT{x.Race.Track}");

		private KeyValuePair<int, string> GetTrainer(RaceDetail x) => new KeyValuePair<int, string>(4, $"T{x.Trainer}");

		private KeyValuePair<int, string> GetJockeyTrainer(RaceDetail x) => new KeyValuePair<int, string>(5, $"J{x.Jockey}-T{x.Trainer}");

		private KeyValuePair<int, string> GetTrainerBreeder(RaceDetail x) => new KeyValuePair<int, string>(6, $"T{x.Trainer}-B{x.Breeder}");

		private KeyValuePair<int, string> GetBreeder(RaceDetail x) => new KeyValuePair<int, string>(7, $"B{x.Breeder}");

		private KeyValuePair<int, string> GetSire(RaceDetail x) => new KeyValuePair<int, string>(8, $"S{x.Sire}");

		private KeyValuePair<int, string> GetDamSire(RaceDetail x) => new KeyValuePair<int, string>(9, $"D{x.DamSire}");

		private KeyValuePair<int, string> GetSireDamSire(RaceDetail x) => new KeyValuePair<int, string>(10, $"S{x.Sire}-D{x.DamSire}");

		private KeyValuePair<int, string>[] GetKeyArray(RaceDetail x) => new[]
		{
			GetHorse(x.Horse),
			GetJockey(x),
			GetJockeyPlace(x),
			GetJockeyTrack(x),
			GetTrainer(x),
			GetJockeyTrainer(x),
			GetBreeder(x),
			GetTrainerBreeder(x),
			GetSire(x),
			GetDamSire(x),
			GetSireDamSire(x),
		};

		private Dictionary<string, TrackConditionDistance> _TrackConditionDistances = new();

		private List<RaceDetail> GetMaster(RaceDetail x, KeyValuePair<int, string> kvp) => _master[kvp.Key]
			.Get(kvp.Value, new List<RaceDetail>())
			.Where(y => x.Race.RaceDate.AddYears(-3) < y.Race.RaceDate && y.Race.RaceDate < x.Race.RaceDate.AddDays(-3)).Take(100).ToList();

		private string GetTrackConditionDistance(Race x) => $"T{x.Track}-C{x.TrackCondition}-D{x.Distance}";

		private string GetTrackConditionDistance(TrackConditionDistance x) => $"T{x.Track}-C{x.TrackCondition}-D{x.Distance}";

		private async Task InitializeHistory(SQLiteControl conn, DateTime date)
		{
			foreach (var race in await conn.GetRaceAsync(date).ToArrayAsync())
			{
				// 今ﾚｰｽの情報を取得する
				var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

				// 過去ﾃﾞｰﾀ設定
				details.ForEach(x => x.SetHistoricalData(GetHorses(x), details, GetTrackConditionDistances(x)));

				// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
				race.AverageRating = details.Average(x => x.AverageRating);

				// 今ﾚｰｽの情報をﾒﾓﾘに格納
				details.ForEach(AddHistory);
			}
		}

		private void Clear()
		{
			_master.ForEach(x => x.Clear());
			_TrackConditionDistances.Clear();
		}
	}
}