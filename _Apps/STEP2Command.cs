using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public class STEP2Command : STEPBase
	{
		private Dictionary<string, List<RaceDetail>> _Horses = new();
		private Dictionary<string, List<RaceDetail>> _Jockeys = new();
		private Dictionary<string, List<RaceDetail>> _Trainers = new();
		private Dictionary<string, List<RaceDetail>> _Sires = new();
		private Dictionary<string, List<RaceDetail>> _DamSires = new();
		private Dictionary<string, List<RaceDetail>> _SireDamSires = new();
		private Dictionary<string, List<RaceDetail>> _Breeders = new();

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

					// これまで作成した教育ﾃﾞｰﾀの削除
					AppSetting.Instance.RemoveAllRankingTrain();
				}

				// ﾃｰﾌﾞﾙ作成
				await conn.CreateModel();

				// バッチ処理で訓練データを生成・保存
				await GenerateAndSaveTrainingDataAsync(conn);

				_Horses.Clear();
				_Jockeys.Clear();
				_Trainers.Clear();
				_Sires.Clear();
				_DamSires.Clear();
				_SireDamSires.Clear();
				_Breeders.Clear();
			}
		}

		/// <summary>
		/// 指定期間のレースデータから訓練データを段階的に生成・保存
		/// </summary>
		public async Task GenerateAndSaveTrainingDataAsync(SQLiteControl conn)
		{
			MainViewModel.AddLog($"訓練データ生成開始");

			var already = conn.GetAlreadyCreatedRacesAsync().ToBlockingEnumerable().ToArray();

			await foreach (var race in conn.GetRaceAsync())
			{
				try
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

					// 過去ﾚｰｽの結果をｾｯﾄする
					details.ForEach(x => x.Initialize(_Horses.Get(x.Horse, new List<RaceDetail>())));

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					if (!already.Contains(race.RaceId))
					{
						// 特徴量を生成
						var results = details.Select(x =>
						{
							var features = x.ExtractFeatures(
								_Horses.Get(x.Horse, new List<RaceDetail>()),
								details,
								_Jockeys.Get(x.Jockey, new List<RaceDetail>()),
								_Trainers.Get(x.Trainer, new List<RaceDetail>()),
								_Breeders.Get(x.Breeder, new List<RaceDetail>()),
								_Sires.Get(x.Sire, new List<RaceDetail>()),
								_DamSires.Get(x.DamSire, new List<RaceDetail>()),
								_SireDamSires.Get(x.SireDamSire, new List<RaceDetail>())
							);

							// ラベル生成（難易度調整済み着順スコア）
							features.Label = (x.FinishPosition - 1).Run(x => x < 12 ? x : 11);

							return features;
						});

						// ﾃﾞｰﾀﾍﾞｰｽに格納
						await conn.InsertModelAsync(results);
					}

					// 今ﾚｰｽの情報をﾒﾓﾘに格納
					details.ForEach(x =>
					{
						void AddHistory(Dictionary<string, List<RaceDetail>> dic, RaceDetail tgt, string key)
						{
							if (!dic.ContainsKey(key))
							{
								dic.Add(key, new List<RaceDetail>());
							}
							dic[key].Insert(0, tgt);
						}

						AddHistory(_Horses, x, x.Horse);
						AddHistory(_Jockeys, x, x.Jockey);
						AddHistory(_Trainers, x, x.Trainer);
						AddHistory(_Sires, x, x.Sire);
						AddHistory(_DamSires, x, x.DamSire);
						AddHistory(_SireDamSires, x, x.SireDamSire);
						AddHistory(_Breeders, x, x.Breeder);
					});

					MainViewModel.AddLog($"訓練データ生成完了：{race.RaceId} {race.RaceDate}");
				}
				catch (Exception ex)
				{
					MessageService.Debug(ex.ToString());
				}
			}

			MainViewModel.AddLog($"訓練データ生成完了");
		}
	}

	// ===== 難易度調整済みパフォーマンス計算 =====

	public class AdjustedPerformanceMetrics
	{
		public float Recent3AdjustedAvg { get; set; }
		public float Recent5AdjustedAvg { get; set; }
		public float OverallAdjustedAvg { get; set; }
		public float BestAdjustedScore { get; set; }
		public float LastRaceAdjustedScore { get; set; }
		public float AdjustedConsistency { get; set; }
		public float G1AdjustedAvg { get; set; }
		public float G2G3AdjustedAvg { get; set; }
		public float OpenAdjustedAvg { get; set; }
	}

	public static class AdjustedPerformanceCalculator
	{
		public static float CalculateAdjustedInverseScore(uint finishPosition, Race race)
		{
			// 基本の逆数スコア
			float baseScore = 1.0f / finishPosition;

			// レース難易度による重み付け
			float difficultyMultiplier = RaceDifficultyAnalyzer.CalculateDifficultyMultiplier(race);

			return baseScore * difficultyMultiplier;
		}

		public static AdjustedPerformanceMetrics CalculateAdjustedPerformance(Race race, RaceDetail detail, List<RaceDetail> results)
		{
			var adjustedScores = results
				.Select(result => result.CalculateAdjustedInverseScore())
				.ToArray();

			return new AdjustedPerformanceMetrics
			{
				Recent3AdjustedAvg = adjustedScores.Take(3).DefaultIfEmpty(0.1f).Average(),
				Recent5AdjustedAvg = adjustedScores.Take(5).DefaultIfEmpty(0.1f).Average(),
				OverallAdjustedAvg = adjustedScores.DefaultIfEmpty(0.1f).Average(),
				BestAdjustedScore = adjustedScores.DefaultIfEmpty(0.1f).Max(),
				LastRaceAdjustedScore = adjustedScores.FirstOrDefault(0.1f),
				AdjustedConsistency = CalculateConsistency(adjustedScores),
				G1AdjustedAvg = CalculateGradeSpecificAverage(results, x => x.IsG1()),
				G2G3AdjustedAvg = CalculateGradeSpecificAverage(results, x => x.IsG2() || x.IsG3()),
				OpenAdjustedAvg = CalculateGradeSpecificAverage(results, x => x.IsOPEN())
			};
		}

		private static float CalculateConsistency(float[] scores)
		{
			if (scores.Length < 2) return 1.0f;

			var mean = scores.Average();
			var variance = scores.Select(s => (s - mean) * (s - mean)).Average();
			var stdDev = (float)Math.Sqrt(variance);

			return mean / (stdDev + 0.1f); // 安定性指標
		}

		private static float CalculateGradeSpecificAverage(List<RaceDetail> races, Func<GradeType, bool> is_target)
		{
			var targetGrades = EnumUtil.GetValues<GradeType>().Where(is_target);

			return races
				.Where(r => targetGrades.Contains(r.Race.Grade))
				.AdjustedInverseScoreAverage(0.0F);
		}

		// ===== レース難易度分析 =====

		public static class RaceDifficultyAnalyzer
		{
			public static float CalculateDifficultyMultiplier(Race race)
			{
				float multiplier = 1.0f;

				// グレード別基本倍率
				multiplier *= GetGradeMultiplier(race.Grade);

				// 賞金による補正
				multiplier *= CalculatePrizeMultiplier(race.FirstPrizeMoney);

				// 頭数による競争激化度
				multiplier *= CalculateFieldSizeMultiplier(race.NumberOfHorses);

				// 出走馬の平均レーティング
				if (race.AverageRating > 0) multiplier *= CalculateQualityMultiplier(race.AverageRating);

				// 特別条件による補正
				multiplier *= GetSpecialConditionMultiplier(race);

				return multiplier;
			}

			private static float GetGradeMultiplier(GradeType grade)
			{
				return (grade.Single() + 5F) / 10F;
			}

			private static float CalculatePrizeMultiplier(long prizeMoney)
			{
				const long basePrize = 700; // 700万円を基準
				return (float)(Math.Log((double)prizeMoney / basePrize) * 0.2 + 1.0);
			}

			private static float CalculateFieldSizeMultiplier(int numberOfHorses)
			{
				const int baseField = 12; // 12頭を基準
				return (float)(Math.Log((double)numberOfHorses / baseField) * 0.15 + 1.0);
			}

			private static float CalculateQualityMultiplier(float averageRating)
			{
				return (float)Math.Log(averageRating) * 0.01f + 1.0f;
			}

			private static float GetSpecialConditionMultiplier(Race race)
			{
				float multiplier = 1.0f;

				if (race.IsInternational)
					multiplier *= 1.2f;

				if (race.IsAgedHorseRace)
					multiplier *= 1.1f;

				return multiplier;
			}
		}
	}

	// ===== 条件適性計算 =====
	public class ConditionMetrics
	{
		public float CurrentDistanceAptitude { get; set; }
		public float CurrentTrackTypeAptitude { get; set; }
		public float CurrentTrackConditionAptitude { get; set; }
		public float HeavyTrackAptitude { get; set; }
		public float SpecificCourseAptitude { get; set; }

		public float SprintAptitude { get; set; }
		public float MileAptitude { get; set; }
		public float MiddleDistanceAptitude { get; set; }
		public float LongDistanceAptitude { get; set; }
	}

	public static class ConditionAptitudeCalculator
	{
		public static ConditionMetrics CalculateConditionMetrics(List<RaceDetail> horses, Race race)
		{
			return new ConditionMetrics()
			{
				CurrentDistanceAptitude = ConditionAptitudeCalculator.CalculateCurrentConditionAptitude(horses, race),
				CurrentTrackTypeAptitude = ConditionAptitudeCalculator.CalculateTrackTypeAptitude(horses, race.TrackType),
				CurrentTrackConditionAptitude = ConditionAptitudeCalculator.CalculateTrackConditionAptitude(horses, race.TrackConditionType),
				HeavyTrackAptitude = CalculateHeavyTrackAptitude(horses),
				SpecificCourseAptitude = CalculateSpecificCourseAptitude(horses, race.CourseName),
				SprintAptitude = CalculateDistanceCategoryAptitude(horses, DistanceCategory.Sprint),
				MileAptitude = CalculateDistanceCategoryAptitude(horses, DistanceCategory.Mile),
				MiddleDistanceAptitude = CalculateDistanceCategoryAptitude(horses, DistanceCategory.Middle),
				LongDistanceAptitude = CalculateDistanceCategoryAptitude(horses, DistanceCategory.Long),
			};
		}

		private static float CalculateCurrentConditionAptitude(List<RaceDetail> horses, Race race)
		{
			// 今回と同じ条件のレースを抽出
			var similarRaces = horses.Where(r =>
				r.Race.Distance == race.Distance &&
				r.Race.TrackType == race.TrackType &&
				r.Race.TrackConditionType == race.TrackConditionType).ToList();

			if (!similarRaces.Any())
			{
				return CalculateRelaxedConditionAptitude(horses, race);
			}

			return CalculateAdjustedAverageScore(similarRaces);
		}

		private static float CalculateDistanceCategoryAptitude(List<RaceDetail> races, DistanceCategory category)
		{
			var categoryRaces = races
				.Where(r => r.Race.DistanceCategory == category)
				.ToList();

			return CalculateAdjustedAverageScore(categoryRaces);
		}

		private static float CalculateTrackTypeAptitude(List<RaceDetail> races, TrackType type)
		{
			var conditionRaces = races.Where(r =>
				r.Race.TrackType == type).ToList();

			return CalculateAdjustedAverageScore(conditionRaces);
		}

		private static float CalculateTrackConditionAptitude(List<RaceDetail> races, TrackConditionType condition)
		{
			var conditionRaces = races.Where(r =>
				r.Race.TrackConditionType == condition).ToList();

			return CalculateAdjustedAverageScore(conditionRaces);
		}

		private static float CalculateRelaxedConditionAptitude(List<RaceDetail> raceHistory, Race currentRace)
		{
			// 1. 同距離のみ
			var sameDistance = raceHistory.Where(r => r.Race.Distance == currentRace.Distance);
			if (sameDistance.Any())
				return CalculateAdjustedAverageScore(sameDistance.ToList());

			// 2. 同距離カテゴリ
			var sameCategory = raceHistory.Where(r => r.Race.DistanceCategory == currentRace.DistanceCategory);
			if (sameCategory.Any())
				return CalculateAdjustedAverageScore(sameCategory.ToList());

			// 3. 全体平均
			return CalculateAdjustedAverageScore(raceHistory);
		}

		private static float CalculateAdjustedAverageScore(List<RaceDetail> races)
		{
			if (!races.Any()) return 0.2f;

			return races.AdjustedInverseScoreAverage();
		}

		// 補助計算メソッド
		private static float CalculateHeavyTrackAptitude(List<RaceDetail> races)
		{
			var heavyRaces = races.Where(r => new[] { TrackConditionType.Heavy, TrackConditionType.Poor }.Contains(r.Race.TrackConditionType));
			if (!heavyRaces.Any()) return 0.2f;
			return heavyRaces.AdjustedInverseScoreAverage();
		}

		private static float CalculateSpecificCourseAptitude(List<RaceDetail> races, string courseName)
		{
			var courseRaces = races.Where(r => r.Race.CourseName == courseName);
			if (!courseRaces.Any()) return 0.2f;
			return courseRaces.AdjustedInverseScoreAverage();
		}

	}

	// ===== 関係者実績分析 =====

	public static class ConnectionAnalyzer
	{
		public static ConnectionMetrics AnalyzeConnections(Race upcomingRace, List<RaceDetail> jockeys, List<RaceDetail> trainers, List<RaceDetail> breeders, List<RaceDetail> sires, List<RaceDetail> damsires, List<RaceDetail> siredamsires)
		{
			return new ConnectionMetrics
			{
				JockeyOverallInverseAvg = jockeys.AdjustedInverseScoreAverage(0.2F),
				JockeyRecentInverseAvg = jockeys.Take(30).AdjustedInverseScoreAverage(0.2F),
				JockeyCurrentConditionAvg = CalculateConditionSpecific(jockeys, upcomingRace),

				TrainerOverallInverseAvg = trainers.AdjustedInverseScoreAverage(0.2F),
				TrainerRecentInverseAvg = trainers.Take(30).AdjustedInverseScoreAverage(0.2F),
				TrainerCurrentConditionAvg = CalculateConditionSpecific(trainers, upcomingRace),

				BreederOverallInverseAvg = trainers.AdjustedInverseScoreAverage(0.2F),
				BreederRecentInverseAvg = trainers.Take(30).AdjustedInverseScoreAverage(0.2F),
				BreederCurrentConditionAvg = CalculateConditionSpecific(trainers, upcomingRace),

				SireOverallInverseAvg = trainers.AdjustedInverseScoreAverage(0.2F),
				SireRecentInverseAvg = trainers.Take(30).AdjustedInverseScoreAverage(0.2F),
				SireCurrentConditionAvg = CalculateConditionSpecific(trainers, upcomingRace),

				DamSireOverallInverseAvg = trainers.AdjustedInverseScoreAverage(0.2F),
				DamSireRecentInverseAvg = trainers.Take(30).AdjustedInverseScoreAverage(0.2F),
				DamSireCurrentConditionAvg = CalculateConditionSpecific(trainers, upcomingRace),

				SireDamSireOverallInverseAvg = trainers.AdjustedInverseScoreAverage(0.2F),
				SireDamSireRecentInverseAvg = trainers.Take(30).AdjustedInverseScoreAverage(0.2F),
				SireDamSireCurrentConditionAvg = CalculateConditionSpecific(trainers, upcomingRace),
			};
		}

		private static float CalculateConditionSpecific(IEnumerable<RaceDetail> races, Race upcomingRace)
		{
			var matchingRaces = races
				.Where(r => r.Race.DistanceCategory == upcomingRace.DistanceCategory && r.Race.TrackType == upcomingRace.TrackType);

			if (!matchingRaces.Any()) return 0.2f;

			return matchingRaces.AdjustedInverseScoreAverage();
		}
	}

	public class ConnectionMetrics
	{
		public float JockeyOverallInverseAvg { get; set; }
		public float JockeyRecentInverseAvg { get; set; }
		public float JockeyCurrentConditionAvg { get; set; }

		public float TrainerOverallInverseAvg { get; set; }
		public float TrainerRecentInverseAvg { get; set; }
		public float TrainerCurrentConditionAvg { get; set; }

		public float BreederOverallInverseAvg { get; set; }
		public float BreederRecentInverseAvg { get; set; }
		public float BreederCurrentConditionAvg { get; set; }

		public float SireOverallInverseAvg { get; set; }
		public float SireRecentInverseAvg { get; set; }
		public float SireCurrentConditionAvg { get; set; }

		public float DamSireOverallInverseAvg { get; set; }
		public float DamSireRecentInverseAvg { get; set; }
		public float DamSireCurrentConditionAvg { get; set; }

		public float SireDamSireOverallInverseAvg { get; set; }
		public float SireDamSireRecentInverseAvg { get; set; }
		public float SireDamSireCurrentConditionAvg { get; set; }
	}

	// ===== 新馬・未勝利戦対応 =====

	public static class MaidenRaceAnalyzer
	{
		public static NewHorseMetrics AnalyzeNewHorse(RaceDetail detail, RaceDetail[] inraces, List<RaceDetail> horses, List<RaceDetail> jockeys, List<RaceDetail> trainers, List<RaceDetail> breeders, List<RaceDetail> sires, List<RaceDetail> damsires)
		{
			float CalculateNewHorseInverse(List<RaceDetail> arr) => arr
				.Where(r => r.RaceCount == 0)
				.AdjustedInverseScoreAverage();

			return new NewHorseMetrics
			{
				TrainerNewHorseInverse = CalculateNewHorseInverse(trainers),
				JockeyNewHorseInverse = CalculateNewHorseInverse(jockeys),
				SireNewHorseInverse = CalculateNewHorseInverse(sires),
				DamSireNewHorseInverse = CalculateNewHorseInverse(damsires),
				BreederNewHorseInverse = CalculateNewHorseInverse(breeders),
				PurchasePriceRank = detail.PurchasePrice / inraces.Average(x => x.PurchasePrice),
			};
		}
	}

	public class NewHorseMetrics
	{
		public float TrainerNewHorseInverse { get; set; }
		public float JockeyNewHorseInverse { get; set; }
		public float SireNewHorseInverse { get; set; }
		public float DamSireNewHorseInverse { get; set; }
		public float BreederNewHorseInverse { get; set; }
		public float PurchasePriceRank { get; set; }
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
		private static readonly string[] col_model = Arr("ﾚｰｽID", "馬ID", "Features");

		public static async Task CreateModel(this SQLiteControl conn)
		{
			await conn.Create(
				"t_model",
				col_model,
				Arr("ﾚｰｽID", "馬ID")
			);

			// TODO indexの作成
		}

		public static async Task<bool> ExistsModelTableAsync(this SQLiteControl conn)
		{
			return await conn.ExistsColumn("t_model", "ﾚｰｽID");
		}

		public static async Task InsertModelAsync(this SQLiteControl conn, IEnumerable<OptimizedHorseFeaturesModel> data)
		{
			if (!data.Any()) return;

			foreach (var chunk in data.GroupBy(x => x.RaceId))
			{
				await conn.BeginTransaction();
				foreach (var x in chunk)
				{
					var parameters = new[]
					{
						SQLiteUtil.CreateParameter(DbType.String, x.RaceId),
						SQLiteUtil.CreateParameter(DbType.String, x.HorseName),
						SQLiteUtil.CreateParameter(DbType.Object, x.Serialize())
					};
					await conn.ExecuteNonQueryAsync("REPLACE INTO t_model (ﾚｰｽID, 馬ID, Features) VALUES (?, ?, ?)", parameters);
				}
				conn.Commit();
			}
		}

		public static async Task<bool> ExistsModelAsync(this SQLiteControl conn, string raceid)
		{
			var cnt = await conn.ExecuteScalarAsync(
				"SELECT COUNT(*) FROM t_orig_h WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			).RunAsync(x => x.GetInt32());
			return 0 < cnt;
		}

		public static async IAsyncEnumerable<Race> GetRaceAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new Race(x);
			}
		}

		public static async IAsyncEnumerable<RaceDetail> GetRaceDetailsAsync(this SQLiteControl conn, Race race)
		{
			var sql = @"
SELECT d.ﾚｰｽID, d.馬番, d.馬ID, d.騎手ID, d.調教師ID, u.父ID, u.母父ID, u.生産者ID, d.着順, d.ﾀｲﾑ変換, d.賞金, u.評価額, u.生年月日, d.斤量, d.通過, d.上り
FROM v_orig_d d, t_uma u
WHERE d.ﾚｰｽID = ? AND d.馬ID = u.馬ID
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, race.RaceId),
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				yield return new RaceDetail(x, race);
			}
		}

		public static async IAsyncEnumerable<string> GetAlreadyCreatedRacesAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT DISTINCT ﾚｰｽID
FROM   t_model h
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["ﾚｰｽID"].Str();
			}
		}

	}

}