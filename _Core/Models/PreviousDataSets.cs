using AngleSharp.Common;
using Codeplex.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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

		private static bool DistanceWithinRance(RaceDetail x, RaceDetail y) => Math.Abs(x.Race.Distance - y.Race.Distance) <= 200;

		public static List<RaceDetail> GetHorses(RaceDetail x) => GetHorses(x.Horse, x);

		public static List<RaceDetail> GetHorses(string x, RaceDetail detail) => _PDS.GetMaster(detail, _PDS.GetHorse(x));

		public static List<RaceDetail> GetJockeys(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockey(x));

		public static List<RaceDetail> GetJockeyDistances(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockey(x), y => DistanceWithinRance(x, y));

		public static List<RaceDetail> GetJockeyPlaces(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockeyPlace(x));

		public static List<RaceDetail> GetJockeyTracks(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockeyTrack(x));

		public static List<RaceDetail> GetJockeyTrackConditions(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockeyTrackCondition(x));

		public static List<RaceDetail> GetTrainers(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetTrainer(x));

		public static List<RaceDetail> GetTrainerPlaces(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetTrainerPlace(x));

		public static List<RaceDetail> GetJockeyTrainers(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetJockeyTrainer(x));

		public static List<RaceDetail> GetBreeders(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetBreeder(x));

		public static List<RaceDetail> GetTrainerBreeders(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetTrainerBreeder(x));

		public static List<RaceDetail> GetSires(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSire(x));

		public static List<RaceDetail> GetSireDistances(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSire(x), y => DistanceWithinRance(x, y));

		public static List<RaceDetail> GetSireTracks(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSireTrack(x));

		public static List<RaceDetail> GetSireTrackConditions(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSireTrackCondition(x));

		public static List<RaceDetail> GetSireDamSires(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSireDamSire(x));

		public static List<RaceDetail> GetSireDamSireDistances(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSireDamSire(x), y => DistanceWithinRance(x, y));

		public static List<RaceDetail> GetSireDamSireTracks(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSireDamSireTrack(x));

		public static List<RaceDetail> GetSireDamSireTrackConditions(RaceDetail x) => _PDS.GetMaster(x, _PDS.GetSireDamSireTrackCondition(x));

		public static TrackConditionDistance GetTrackConditionDistances(Race x) => _PDS._TrackConditionDistances.Get(_PDS.GetTrackConditionDistance(x), TrackConditionDistance.Default);

		// Elo取得 — 通常(K=24)
		public static float GetHorseElo(RaceDetail x) => _PDS._HorseElo.Get($"H{x.Horse}", EloInitial);

		public static float GetJockeyElo(RaceDetail x) => _PDS._JockeyElo.Get($"J{x.Jockey}", EloInitial);

		public static float GetTrainerElo(RaceDetail x) => _PDS._TrainerElo.Get($"T{x.Trainer}", EloInitial);

		// Elo取得 — 早期収束(K=48)
		public static float GetFastHorseElo(RaceDetail x) => _PDS._FastHorseElo.Get($"H{x.Horse}", EloInitial);

		public static float GetFastJockeyElo(RaceDetail x) => _PDS._FastJockeyElo.Get($"J{x.Jockey}", EloInitial);

		public static float GetFastTrainerElo(RaceDetail x) => _PDS._FastTrainerElo.Get($"T{x.Trainer}", EloInitial);

		// Elo更新（ﾏﾙﾁﾌﾟﾚｲﾔｰ方式）
		public static void UpdateElo(RaceDetail[] details)
		{
			var n = details.Length;
			if (n < 2) return;

			// 各馬の現在Eloを取得（通常 + 早期収束）
			var horseElos = details.Select(d => GetHorseElo(d)).ToArray();
			var jockeyElos = details.Select(d => GetJockeyElo(d)).ToArray();
			var trainerElos = details.Select(d => GetTrainerElo(d)).ToArray();
			var fastHorseElos = details.Select(d => GetFastHorseElo(d)).ToArray();
			var fastJockeyElos = details.Select(d => GetFastJockeyElo(d)).ToArray();
			var fastTrainerElos = details.Select(d => GetFastTrainerElo(d)).ToArray();

			for (int i = 0; i < n; i++)
			{
				float horseExp = 0, jockeyExp = 0, trainerExp = 0;
				float fastHorseExp = 0, fastJockeyExp = 0, fastTrainerExp = 0;
				float actual = 0;

				for (int j = 0; j < n; j++)
				{
					if (i == j) continue;
					// 通常Elo期待勝率
					horseExp += 1f / (1f + MathF.Pow(10f, (horseElos[j] - horseElos[i]) / 400f));
					jockeyExp += 1f / (1f + MathF.Pow(10f, (jockeyElos[j] - jockeyElos[i]) / 400f));
					trainerExp += 1f / (1f + MathF.Pow(10f, (trainerElos[j] - trainerElos[i]) / 400f));
					// 早期収束Elo期待勝率
					fastHorseExp += 1f / (1f + MathF.Pow(10f, (fastHorseElos[j] - fastHorseElos[i]) / 400f));
					fastJockeyExp += 1f / (1f + MathF.Pow(10f, (fastJockeyElos[j] - fastJockeyElos[i]) / 400f));
					fastTrainerExp += 1f / (1f + MathF.Pow(10f, (fastTrainerElos[j] - fastTrainerElos[i]) / 400f));
					// 実績（着順が上なら勝ち）
					actual += details[i].FinishPosition < details[j].FinishPosition ? 1f : 0f;
				}

				var norm = n - 1f;
				var id = details[i];

				// 通常Elo更新 (K=24)
				_PDS._HorseElo[$"H{id.Horse}"] = horseElos[i] + EloK * (actual - horseExp) / norm;
				_PDS._JockeyElo[$"J{id.Jockey}"] = jockeyElos[i] + EloK * (actual - jockeyExp) / norm;
				_PDS._TrainerElo[$"T{id.Trainer}"] = trainerElos[i] + EloK * (actual - trainerExp) / norm;

				// 早期収束Elo更新 (K=48)
				_PDS._FastHorseElo[$"H{id.Horse}"] = fastHorseElos[i] + EloFastK * (actual - fastHorseExp) / norm;
				_PDS._FastJockeyElo[$"J{id.Jockey}"] = fastJockeyElos[i] + EloFastK * (actual - fastJockeyExp) / norm;
				_PDS._FastTrainerElo[$"T{id.Trainer}"] = fastTrainerElos[i] + EloFastK * (actual - fastTrainerExp) / norm;
			}
		}

		public static void AddHistory(RaceDetail x)
		{
			foreach (var key in _PDS.GetKeyArray(x))
			{
				var dic = _PDS._master[key.Key];
				if (!dic.TryGetValue(key.Value, out var list))
				{
					list = new List<RaceDetail>();
					dic.Add(key.Value, list);
				}
				list.Add(x);
				var cutoff = x.Race.RaceDate.AddYears(-3);
				while (list.Count > 0 && list[0].Race.RaceDate < cutoff)
				{
					list.RemoveAt(0);
				}
			}
		}

	}

	public partial class PreviousDataSets
	{
		private Dictionary<string, List<RaceDetail>>[] _master = Enumerable
			.Range(0, 20)
			.Select(i => new Dictionary<string, List<RaceDetail>>())
			.ToArray();

		private KeyValuePair<int, string> GetHorse(string horse) => new KeyValuePair<int, string>(0, $"H{horse}");

		private KeyValuePair<int, string> GetJockey(RaceDetail x) => new KeyValuePair<int, string>(1, $"J{x.Jockey}");

		private KeyValuePair<int, string> GetJockeyPlace(RaceDetail x) => new KeyValuePair<int, string>(2, $"J{x.Jockey}-JP{x.Race.Place}");

		private KeyValuePair<int, string> GetJockeyTrack(RaceDetail x) => new KeyValuePair<int, string>(3, $"J{x.Jockey}-JT{x.Race.Track}");

		private KeyValuePair<int, string> GetJockeyTrackCondition(RaceDetail x) => new KeyValuePair<int, string>(4, $"J{x.Jockey}-JTC{x.Race.TrackCondition}");

		private KeyValuePair<int, string> GetTrainer(RaceDetail x) => new KeyValuePair<int, string>(5, $"T{x.Trainer}");

		private KeyValuePair<int, string> GetTrainerPlace(RaceDetail x) => new KeyValuePair<int, string>(6, $"T{x.Trainer}-TP{x.Race.Place}");

		private KeyValuePair<int, string> GetJockeyTrainer(RaceDetail x) => new KeyValuePair<int, string>(7, $"J{x.Jockey}-T{x.Trainer}");

		private KeyValuePair<int, string> GetTrainerBreeder(RaceDetail x) => new KeyValuePair<int, string>(8, $"T{x.Trainer}-B{x.Breeder}");

		private KeyValuePair<int, string> GetBreeder(RaceDetail x) => new KeyValuePair<int, string>(9, $"B{x.Breeder}");

		private KeyValuePair<int, string> GetSire(RaceDetail x) => new KeyValuePair<int, string>(10, $"S{x.Sire}");

		private KeyValuePair<int, string> GetSireTrack(RaceDetail x) => new KeyValuePair<int, string>(11, $"S{x.Sire}-ST{x.Race.Track}");

		private KeyValuePair<int, string> GetSireTrackCondition(RaceDetail x) => new KeyValuePair<int, string>(12, $"S{x.Sire}-STC{x.Race.TrackCondition}");

		private KeyValuePair<int, string> GetSireDamSire(RaceDetail x) => new KeyValuePair<int, string>(13, $"S{x.Sire}-D{x.DamSire}");

		private KeyValuePair<int, string> GetSireDamSireTrack(RaceDetail x) => new KeyValuePair<int, string>(14, $"S{x.Sire}-D{x.DamSire}-ST{x.Race.Track}");

		private KeyValuePair<int, string> GetSireDamSireTrackCondition(RaceDetail x) => new KeyValuePair<int, string>(15, $"S{x.Sire}-D{x.DamSire}-STC{x.Race.TrackCondition}");

		private KeyValuePair<int, string>[] GetKeyArray(RaceDetail x) => new[]
		{
			GetHorse(x.Horse),
			GetJockey(x),
			GetJockeyPlace(x),
			GetJockeyTrack(x),
			GetJockeyTrackCondition(x),
			GetTrainer(x),
			GetTrainerPlace(x),
			GetJockeyTrainer(x),
			GetBreeder(x),
			GetTrainerBreeder(x),
			GetSire(x),
			GetSireTrack(x),
			GetSireTrackCondition(x),
			GetSireDamSire(x),
			GetSireDamSireTrack(x),
			GetSireDamSireTrackCondition(x),
		};

		private Dictionary<string, TrackConditionDistance> _TrackConditionDistances = new();

		// Eloﾚｰﾃｨﾝｸﾞ — 通常(K=24)
		private Dictionary<string, float> _HorseElo = new();

		private Dictionary<string, float> _JockeyElo = new();
		private Dictionary<string, float> _TrainerElo = new();

		// Eloﾚｰﾃｨﾝｸﾞ — 早期収束(K=48)
		private Dictionary<string, float> _FastHorseElo = new();

		private Dictionary<string, float> _FastJockeyElo = new();
		private Dictionary<string, float> _FastTrainerElo = new();

		private const float EloInitial = 1500f;
		private const float EloK = 16f;
		private const float EloFastK = 64f;

		private List<RaceDetail> GetMaster(RaceDetail x, KeyValuePair<int, string> kvp) => GetMaster(x, kvp, x => true);

		private List<RaceDetail> GetMaster(RaceDetail x, KeyValuePair<int, string> kvp, Func<RaceDetail, bool> func) => ((IEnumerable<RaceDetail>)_master[kvp.Key]
			.Get(kvp.Value, new List<RaceDetail>()))
			.Reverse()
			.Where(y => y.Race.RaceDate < x.Race.RaceDate.AddDays(-3) && func(y)).Take(100).ToList();

		private string GetTrackConditionDistance(Race x) => $"T{x.Track}-C{x.TrackCondition}-D{x.Distance}";

		private string GetTrackConditionDistance(TrackConditionDistance x) => $"T{x.Track}-C{x.TrackCondition}-D{x.Distance}";

		private async Task InitializeHistory(SQLiteControl conn, DateTime date)
		{
			var prevdate = date.AddYears(-10);
			await foreach (var (race, details) in conn.GetRaceDetailsGroupedAsync(date))
			{
				if (race.RaceDate > prevdate)
				{
					var tcd = PreviousDataSets.GetTrackConditionDistances(race);

					// 過去ﾃﾞｰﾀ設定
					details.ForEach(x => x.SetHistoricalData(GetHorses(x), details, tcd));

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					// 今ﾚｰｽの情報をﾒﾓﾘに格納
					details.ForEach(AddHistory);
				}
				UpdateElo(details);
			}
		}

		private void Clear()
		{
			_master.ForEach(x => x.Clear());
			_TrackConditionDistances.Clear();
			_HorseElo.Clear();
			_JockeyElo.Clear();
			_TrainerElo.Clear();
			_FastHorseElo.Clear();
			_FastJockeyElo.Clear();
			_FastTrainerElo.Clear();
		}
	}

	public class CircularDateBuffer
	{
		private readonly RaceDetail[] _buffer;
		private int _head;    // 先頭インデックス
		private int _count;
		private readonly int _retentionYears;

		public int Count => _count;

		public CircularDateBuffer(
			int capacity = 2048,
			int retentionYears = 3)
		{
			_buffer = new RaceDetail[capacity];
			_retentionYears = retentionYears;
		}

		// 追加 O(1)
		public void Add(RaceDetail item)
		{
			if (_count == _buffer.Length)
				throw new InvalidOperationException("Buffer full");

			int tail = (_head + _count) % _buffer.Length;
			_buffer[tail] = item;
			_count++;

			var threshold = item.Race.RaceDate.AddYears(-_retentionYears);
			while (_count > 0 && _buffer[_head].Race.RaceDate < threshold)
			{
				_buffer[_head] = default!;       // GC参照を切る
				_head = (_head + 1) % _buffer.Length; // O(1) ⚡ シフトなし
				_count--;
			}
		}

		// 逆順 Where ToArray  ← ここも最適化
		public RaceDetail[] GetReversedWhere(Func<RaceDetail, bool> predicate)
		{
			var result = new List<RaceDetail>(_count);
			for (int i = _count - 1; i >= 0; i--)
			{
				var item = _buffer[(_head + i) % _buffer.Length];
				if (predicate(item)) result.Add(item);
			}
			return result.ToArray();
		}
	}
}