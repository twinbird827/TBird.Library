using Microsoft.ML.TorchSharp.Roberta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;
using Tensorflow.Keras.Layers;

namespace Netkeiba.Models
{
	public class PreviousDataSets
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

		private List<RaceDetail> GetMaster(KeyValuePair<int, string> kvp) => _master[kvp.Key].Get(kvp.Value, new List<RaceDetail>());

		public List<RaceDetail> GetHorses(RaceDetail x) => GetHorses(x.Horse);

		public List<RaceDetail> GetHorses(string x) => GetMaster(GetHorse(x));

		public List<RaceDetail> GetJockeys(RaceDetail x) => GetMaster(GetJockey(x));

		public List<RaceDetail> GetJockeyPlaces(RaceDetail x) => GetMaster(GetJockeyPlace(x));

		public List<RaceDetail> GetJockeyTracks(RaceDetail x) => GetMaster(GetJockeyTrack(x));

		public List<RaceDetail> GetTrainers(RaceDetail x) => GetMaster(GetTrainer(x));

		public List<RaceDetail> GetJockeyTrainers(RaceDetail x) => GetMaster(GetJockeyTrainer(x));

		public List<RaceDetail> GetBreeders(RaceDetail x) => GetMaster(GetBreeder(x));

		public List<RaceDetail> GetTrainerBreeders(RaceDetail x) => GetMaster(GetTrainerBreeder(x));

		public List<RaceDetail> GetSires(RaceDetail x) => GetMaster(GetSire(x));

		public List<RaceDetail> GetDamSires(RaceDetail x) => GetMaster(GetDamSire(x));

		public List<RaceDetail> GetSireDamSires(RaceDetail x) => GetMaster(GetSireDamSire(x));

		private string GetTrackConditionDistance(Race x) => $"T{x.Track}-C{x.TrackCondition}-D{x.Distance}";

		private string GetTrackConditionDistance(TrackConditionDistance x) => $"T{x.Track}-C{x.TrackCondition}-D{x.Distance}";

		public TrackConditionDistance GetTrackConditionDistances(RaceDetail x) => _TrackConditionDistances.Get(GetTrackConditionDistance(x.Race), TrackConditionDistance.Default);

		public void SetTrackConditionDistances(TrackConditionDistance[] tracks) => tracks.ForEach(x => _TrackConditionDistances.Add(GetTrackConditionDistance(x), x));

		public void AddHistory(RaceDetail x)
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

			GetKeyArray(x).ForEach(key =>
			{
				AddHistory(_master[key.Key], x, key.Value);
			});
		}

		public Task InitializeHistory(SQLiteControl conn) => InitializeHistory(conn, DateTime.Now.AddDays(-4));

		public async Task InitializeHistory(SQLiteControl conn, DateTime date)
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

		public void Clear()
		{
			_master.ForEach(x => x.Clear());
			_TrackConditionDistances.Clear();
		}
	}
}