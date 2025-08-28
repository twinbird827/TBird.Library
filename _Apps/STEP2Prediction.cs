using HorseRacingPrediction;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
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
		public static float CalculateAdjustedInverseScore(int finishPosition, Race race)
		{
			// 基本の逆数スコア
			float baseScore = 1.0f / finishPosition;

			// レース難易度による重み付け
			float difficultyMultiplier = RaceDifficultyAnalyzer.CalculateDifficultyMultiplier(race);

			return baseScore * difficultyMultiplier;
		}

		public static AdjustedPerformanceMetrics CalculateAdjustedPerformance(List<RaceResult> raceHistory)
		{
			var adjustedScores = raceHistory
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
				G1AdjustedAvg = CalculateGradeSpecificAverage(raceHistory, "G1古", "G1ク", "G1障"),
				G2G3AdjustedAvg = CalculateGradeSpecificAverage(raceHistory, "G2古", "G2ク", "G2障", "G3古", "G3ク", "G3障"),
				OpenAdjustedAvg = CalculateGradeSpecificAverage(raceHistory, "オープン古", "オープンク", "オープン障")
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

		private static float CalculateGradeSpecificAverage(List<RaceResult> races, params string[] targetGrades)
		{
			var gradeRaces = races.Where(r => targetGrades.Contains(r.Race.Grade));
			if (!gradeRaces.Any()) return 0.0f;

			return gradeRaces.Select(r => r.CalculateAdjustedInverseScore()).Average();
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
				multiplier *= CalculateQualityMultiplier(race.AverageRating);

				// 特別条件による補正
				multiplier *= GetSpecialConditionMultiplier(race);

				return multiplier;
			}

			private static float GetGradeMultiplier(string grade)
			{
				return grade switch
				{
					// 平場G1
					"G1古" => 2.5f,
					"G1ク" => 2.4f,
					// 障害G1
					"G1障" => 2.3f,
					// 平場G2
					"G2古" => 2.0f,
					"G2ク" => 1.9f,
					// 障害G2
					"G2障" => 1.8f,
					// 平場G3
					"G3古" => 1.7f,
					"G3ク" => 1.6f,
					// 障害G3
					"G3障" => 1.5f,
					// オープン
					"オープン古" => 1.4f,
					"オープンク" => 1.3f,
					"オープン障" => 1.2f,
					// 条件戦
					"3勝古" => 1.2f,
					"2勝古" => 1.1f,
					"1勝古" => 1.0f,
					"1勝ク" => 0.95f,
					// 未勝利・新馬
					"未勝利ク" => 0.8f,
					"未勝利障" => 0.75f,
					"新馬ク" => 0.6f,
					_ => 1.0f
				};
			}

			private static float CalculatePrizeMultiplier(long prizeMoney)
			{
				const long basePrize = 7000000; // 700万円を基準
				return (float)(Math.Log((double)prizeMoney / basePrize) * 0.2 + 1.0);
			}

			private static float CalculateFieldSizeMultiplier(int numberOfHorses)
			{
				const int baseField = 12; // 12頭を基準
				return (float)(Math.Log((double)numberOfHorses / baseField) * 0.15 + 1.0);
			}

			private static float CalculateQualityMultiplier(float averageRating)
			{
				const float baseRating = 80.0f;
				return (averageRating - baseRating) * 0.01f + 1.0f;
			}

			private static float GetSpecialConditionMultiplier(Race race)
			{
				float multiplier = 1.0f;

				if (race.IsInternational && (race.Grade == "G1古" || race.Grade == "G1ク"))
					multiplier *= 1.2f;

				if (race.IsAgedHorseRace)
					multiplier *= 1.1f;

				return multiplier;
			}
		}
	}

	// ===== 条件適性計算 =====

	public static class ConditionAptitudeCalculator
	{
		public static float CalculateCurrentConditionAptitude(List<RaceResult> raceHistory, Race currentRace)
		{
			// 今回と同じ条件のレースを抽出
			var similarRaces = raceHistory.Where(r =>
				r.Race.Distance == currentRace.Distance &&
				r.Race.TrackType == currentRace.TrackType &&
				r.Race.TrackCondition == currentRace.TrackCondition).ToList();

			if (!similarRaces.Any())
			{
				return CalculateRelaxedConditionAptitude(raceHistory, currentRace);
			}

			return CalculateAdjustedAverageScore(similarRaces);
		}

		public static float CalculateDistanceCategoryAptitude(List<RaceResult> races, string category)
		{
			var categoryRaces = races.Where(r =>
				GetDistanceCategory(r.Race.Distance) == category).ToList();

			return CalculateAdjustedAverageScore(categoryRaces);
		}

		public static float CalculateTrackConditionAptitude(List<RaceResult> races, string condition)
		{
			var conditionRaces = races.Where(r =>
				r.Race.TrackCondition == condition).ToList();

			return CalculateAdjustedAverageScore(conditionRaces);
		}

		private static float CalculateRelaxedConditionAptitude(List<RaceResult> raceHistory, Race currentRace)
		{
			// 1. 同距離のみ
			var sameDistance = raceHistory.Where(r => r.Race.Distance == currentRace.Distance);
			if (sameDistance.Any())
				return CalculateAdjustedAverageScore(sameDistance.ToList());

			// 2. 同距離カテゴリ
			var sameCategory = raceHistory.Where(r =>
				GetDistanceCategory(r.Race.Distance) == GetDistanceCategory(currentRace.Distance));
			if (sameCategory.Any())
				return CalculateAdjustedAverageScore(sameCategory.ToList());

			// 3. 全体平均
			return CalculateAdjustedAverageScore(raceHistory);
		}

		private static float CalculateAdjustedAverageScore(List<RaceResult> races)
		{
			if (!races.Any()) return 0.2f;

			return races.Select(r => r.CalculateAdjustedInverseScore()).Average();
		}

		private static string GetDistanceCategory(int distance)
		{
			return distance switch
			{
				<= 1400 => "Sprint",
				<= 1800 => "Mile",
				<= 2200 => "Middle",
				_ => "Long"
			};
		}
	}

	// ===== 関係者実績分析 =====

	public static class ConnectionAnalyzer
	{
		public static ConnectionMetrics AnalyzeConnections(List<RaceResult> jockeyRaces, List<RaceResult> trainerRaces, Race upcomingRace)
		{
			// 騎手分析
			var jockeyInverseScores = jockeyRaces.Select(r => r.CalculateAdjustedInverseScore()).ToArray();

			// 調教師分析
			var trainerInverseScores = trainerRaces.Select(r => r.CalculateAdjustedInverseScore()).ToArray();

			return new ConnectionMetrics
			{
				JockeyOverallInverseAvg = jockeyInverseScores.DefaultIfEmpty(0.2f).Average(),
				JockeyRecentInverseAvg = jockeyInverseScores.Take(30).DefaultIfEmpty(0.2f).Average(),
				TrainerOverallInverseAvg = trainerInverseScores.DefaultIfEmpty(0.2f).Average(),
				TrainerRecentInverseAvg = trainerInverseScores.Take(30).DefaultIfEmpty(0.2f).Average(),
				JockeyCurrentConditionAvg = CalculateConditionSpecific(jockeyRaces, upcomingRace),
				TrainerCurrentConditionAvg = CalculateConditionSpecific(trainerRaces, upcomingRace)
			};
		}

		private static float CalculateConditionSpecific(IEnumerable<RaceResult> races, Race upcomingRace)
		{
			var matchingRaces = races.Where(r =>
				GetDistanceCategory(r.Race.Distance) == GetDistanceCategory(upcomingRace.Distance) &&
				r.Race.TrackType == upcomingRace.TrackType);

			if (!matchingRaces.Any()) return 0.2f;

			return matchingRaces.Select(r => r.CalculateAdjustedInverseScore()).Average();
		}

		private static string GetDistanceCategory(int distance)
		{
			return distance switch
			{
				<= 1400 => "Sprint",
				<= 1800 => "Mile",
				<= 2200 => "Middle",
				_ => "Long"
			};
		}
	}

	public class ConnectionMetrics
	{
		public float JockeyOverallInverseAvg { get; set; }
		public float JockeyRecentInverseAvg { get; set; }
		public float TrainerOverallInverseAvg { get; set; }
		public float TrainerRecentInverseAvg { get; set; }
		public float JockeyCurrentConditionAvg { get; set; }
		public float TrainerCurrentConditionAvg { get; set; }
	}

	// ===== 体重分析 =====

	public static class WeightAnalyzer
	{
		public static WeightMetrics AnalyzeWeight(Horse horse, List<Horse> allHorsesInRace)
		{
			var weightHistory = GetWeightHistory(horse, 10);
			var currentWeight = horse.Weight;
			var previousWeight = horse.PreviousWeight;

			return new WeightMetrics
			{
				WeightChange = currentWeight - previousWeight,
				WeightChangeRate = CalculateWeightChangeRate(currentWeight, previousWeight),
				IsRapidWeightChange = IsRapidChange(currentWeight, previousWeight),
				PersonalWeightDeviation = CalculatePersonalDeviation(currentWeight, weightHistory),
				WeightRankInRace = CalculateWeightRank(horse, allHorsesInRace)
			};
		}

		private static float CalculateWeightChangeRate(float current, float previous)
		{
			if (previous == 0) return 0;
			return Math.Abs(current - previous) / previous;
		}

		private static bool IsRapidChange(float current, float previous)
		{
			var changeRate = CalculateWeightChangeRate(current, previous);
			return changeRate > 0.02f; // 2%以上の変化
		}

		private static float CalculatePersonalDeviation(float current, List<float> history)
		{
			if (!history.Any()) return 0;
			var average = history.Average();
			return current - average;
		}

		private static float CalculateWeightRank(Horse horse, List<Horse> allHorses)
		{
			var weightRank = allHorses
				.OrderByDescending(h => h.Weight)
				.ToList()
				.IndexOf(horse) + 1;

			return (float)weightRank / allHorses.Count;
		}

		// モックメソッド
		private static List<float> GetWeightHistory(Horse horse, int count) => new();
	}

	public class WeightMetrics
	{
		public float WeightChange { get; set; }
		public float WeightChangeRate { get; set; }
		public bool IsRapidWeightChange { get; set; }
		public float PersonalWeightDeviation { get; set; }
		public float WeightRankInRace { get; set; }
	}

	// ===== 新馬・未勝利戦対応 =====

	public static class MaidenRaceAnalyzer
	{
		public static NewHorseMetrics AnalyzeNewHorse(Horse horse, Race race)
		{
			var trainerStats = GetTrainerStats(horse.Trainer);
			var jockeyStats = GetJockeyStats(horse.Jockey);
			var sireStats = GetSireStats(horse.Sire);

			return new NewHorseMetrics
			{
				TrainerNewHorseInverse = trainerStats.NewHorseWinRate,
				JockeyNewHorseInverse = jockeyStats.NewHorseWinRate,
				SireNewHorseInverse = sireStats.NewHorseWinRate,
				BreederSuccessRate = GetBreederStats(horse.Breeder).SuccessRate,
				PurchasePriceRank = CalculatePriceRank(horse.PurchasePrice, race.Horses),
				BloodlineQuality = CalculateBloodlineQuality(horse.Sire, horse.DamSire)
			};
		}

		private static float CalculatePriceRank(long price, List<Horse> allHorses)
		{
			var priceRank = allHorses
				.OrderByDescending(h => h.PurchasePrice)
				.ToList()
				.FindIndex(h => h.PurchasePrice == price) + 1;

			return (float)priceRank / allHorses.Count;
		}

		private static float CalculateBloodlineQuality(string sire, string damSire)
		{
			// 簡易血統評価（実際の実装では血統データベースを使用）
			var sireQuality = GetSireQuality(sire);
			var damSireQuality = GetSireQuality(damSire);
			return (sireQuality + damSireQuality) / 2.0f;
		}

		// モックメソッド
		private static TrainerStats GetTrainerStats(string trainer) => new() { NewHorseWinRate = 0.15f };

		private static JockeyStats GetJockeyStats(string jockey) => new() { NewHorseWinRate = 0.12f };

		private static SireStats GetSireStats(string sire) => new() { NewHorseWinRate = 0.10f };

		private static BreederStats GetBreederStats(string breeder) => new() { SuccessRate = 0.08f };

		private static float GetSireQuality(string sire) => 0.5f;
	}

	public class NewHorseMetrics
	{
		public float TrainerNewHorseInverse { get; set; }
		public float JockeyNewHorseInverse { get; set; }
		public float SireNewHorseInverse { get; set; }
		public float BreederSuccessRate { get; set; }
		public float PurchasePriceRank { get; set; }
		public float BloodlineQuality { get; set; }
	}

	public class TrainerStats
	{ public float NewHorseWinRate { get; set; } }

	public class JockeyStats
	{ public float NewHorseWinRate { get; set; } }

	public class SireStats
	{ public float NewHorseWinRate { get; set; } }

	public class BreederStats
	{ public float SuccessRate { get; set; } }

	// ===== 特徴量抽出メインクラス =====

	public static class FeatureExtractor
	{
		public static OptimizedHorseFeatures ExtractFeatures(Horse horse, Race currentRace, List<Horse> allHorsesInRace, IDataRepository repo)
		{
			var raceHistory = horse.RaceHistory.OrderByDescending(r => r.RaceDate).ToList();
			var adjustedMetrics = AdjustedPerformanceCalculator.CalculateAdjustedPerformance(raceHistory);
			var connectionMetrics = ConnectionAnalyzer.AnalyzeConnections(
				repo.GetJockeyRecentRaces(horse.Jockey, currentRace.RaceDate, 100),
				repo.GetTrainerRecentRaces(horse.Trainer, currentRace.RaceDate, 100), currentRace);
			var weightMetrics = WeightAnalyzer.AnalyzeWeight(horse, allHorsesInRace);

			var features = new OptimizedHorseFeatures
			{
				// 基本実績
				Recent3AdjustedAvg = adjustedMetrics.Recent3AdjustedAvg,
				Recent5AdjustedAvg = adjustedMetrics.Recent5AdjustedAvg,
				LastRaceAdjustedScore = adjustedMetrics.LastRaceAdjustedScore,
				BestAdjustedScore = adjustedMetrics.BestAdjustedScore,
				AdjustedConsistency = adjustedMetrics.AdjustedConsistency,
				OverallAdjustedAvg = adjustedMetrics.OverallAdjustedAvg,

				// 条件適性
				CurrentDistanceAptitude = ConditionAptitudeCalculator.CalculateCurrentConditionAptitude(raceHistory, currentRace),
				CurrentTrackTypeAptitude = ConditionAptitudeCalculator.CalculateTrackConditionAptitude(raceHistory, currentRace.TrackType),
				CurrentTrackConditionAptitude = ConditionAptitudeCalculator.CalculateTrackConditionAptitude(raceHistory, currentRace.TrackCondition),
				HeavyTrackAptitude = CalculateHeavyTrackAptitude(raceHistory),
				SpecificCourseAptitude = CalculateSpecificCourseAptitude(raceHistory, currentRace.CourseName),

				// 距離適性
				SprintAptitude = ConditionAptitudeCalculator.CalculateDistanceCategoryAptitude(raceHistory, "Sprint"),
				MileAptitude = ConditionAptitudeCalculator.CalculateDistanceCategoryAptitude(raceHistory, "Mile"),
				MiddleDistanceAptitude = ConditionAptitudeCalculator.CalculateDistanceCategoryAptitude(raceHistory, "Middle"),
				LongDistanceAptitude = ConditionAptitudeCalculator.CalculateDistanceCategoryAptitude(raceHistory, "Long"),

				// 関係者実績
				JockeyCurrentConditionAvg = connectionMetrics.JockeyCurrentConditionAvg,
				TrainerCurrentConditionAvg = connectionMetrics.TrainerCurrentConditionAvg,
				JockeyOverallInverseAvg = connectionMetrics.JockeyOverallInverseAvg,
				TrainerOverallInverseAvg = connectionMetrics.TrainerOverallInverseAvg,

				// 体重関連
				WeightChange = weightMetrics.WeightChange,
				PersonalWeightDeviation = weightMetrics.PersonalWeightDeviation,
				IsRapidWeightChange = weightMetrics.IsRapidWeightChange,

				// 状態・変化
				RestDays = CalculateRestDays(horse.LastRaceDate),
				Age = horse.Age,
				Popularity = CalculatePopularity(horse, allHorsesInRace),
				PerformanceTrend = CalculatePerformanceTrend(adjustedMetrics),
				DistanceChangeAdaptation = CalculateDistanceChangeAdaptation(raceHistory, currentRace),
				ClassChangeAdaptation = CalculateClassChangeAdaptation(raceHistory, currentRace),

				// タイム関連
				SameDistanceTimeIndex = CalculateSameDistanceTimeIndex(raceHistory, currentRace.Distance),
				LastRaceTimeDeviation = CalculateLastRaceTimeDeviation(raceHistory),
				TimeConsistencyScore = CalculateTimeConsistency(raceHistory),

				// メタ情報
				IsNewHorse = horse.RaceCount == 0,
				HasRaceExperience = horse.RaceCount > 0,
				AptitudeReliability = CalculateAptitudeReliability(raceHistory, currentRace),

				// ラベル（トレーニング時に設定）
				Label = 0, // 実際の着順から計算
				RaceId = currentRace.RaceId
			};

			// 新馬の場合は特別処理
			if (horse.RaceCount == 0)
			{
				var newHorseMetrics = MaidenRaceAnalyzer.AnalyzeNewHorse(horse, currentRace);
				features.TrainerNewHorseInverse = newHorseMetrics.TrainerNewHorseInverse;
				features.JockeyNewHorseInverse = newHorseMetrics.JockeyNewHorseInverse;
				features.SireNewHorseInverse = newHorseMetrics.SireNewHorseInverse;
				features.BreederSuccessRate = newHorseMetrics.BreederSuccessRate;
			}

			return features;
		}

		// 補助計算メソッド
		private static float CalculateHeavyTrackAptitude(List<RaceResult> races)
		{
			var heavyRaces = races.Where(r => r.Race.TrackCondition == "重" || r.Race.TrackCondition == "不良");
			if (!heavyRaces.Any()) return 0.2f;
			return heavyRaces.Select(r => r.CalculateAdjustedInverseScore()).Average();
		}

		private static float CalculateSpecificCourseAptitude(List<RaceResult> races, string courseName)
		{
			var courseRaces = races.Where(r => r.Race.CourseName == courseName);
			if (!courseRaces.Any()) return 0.2f;
			return courseRaces.Select(r => r.CalculateAdjustedInverseScore()).Average();
		}

		private static int CalculateRestDays(DateTime lastRaceDate)
		{
			return (DateTime.Now - lastRaceDate).Days;
		}

		private static float CalculatePopularity(Horse horse, List<Horse> allHorses)
		{
			var popularityRank = allHorses.OrderBy(h => h.Odds).ToList().IndexOf(horse) + 1;
			return (float)popularityRank / allHorses.Count;
		}

		private static float CalculatePerformanceTrend(AdjustedPerformanceMetrics metrics)
		{
			return metrics.Recent3AdjustedAvg - metrics.OverallAdjustedAvg;
		}

		private static float CalculateDistanceChangeAdaptation(List<RaceResult> races, Race currentRace)
		{
			if (!races.Any()) return 0.5f;
			var lastDistance = races.First().Race.Distance;
			var distanceChange = Math.Abs(currentRace.Distance - lastDistance);
			return 1.0f / (1.0f + distanceChange / 400.0f); // 400m変化で半減
		}

		private static float CalculateClassChangeAdaptation(List<RaceResult> races, Race currentRace)
		{
			if (!races.Any()) return 0.5f;
			var lastGrade = races.First().Race.Grade;
			var gradeChange = GetGradeNumeric(currentRace.Grade) - GetGradeNumeric(lastGrade);
			return gradeChange <= 0 ? 1.0f : 1.0f / (1.0f + gradeChange * 0.2f);
		}

		private static int GetGradeNumeric(string grade)
		{
			return grade switch
			{
				// G1
				"G1古" => 19,
				"G1ク" => 18,
				"G1障" => 17,
				// G2
				"G2古" => 16,
				"G2ク" => 15,
				"G2障" => 14,
				// G3
				"G3古" => 13,
				"G3ク" => 12,
				"G3障" => 11,
				// オープン
				"オープン古" => 10,
				"オープンク" => 9,
				"オープン障" => 8,
				// 条件戦
				"3勝古" => 7,
				"2勝古" => 6,
				"1勝古" => 5,
				"1勝ク" => 4,
				// 未勝利・新馬
				"未勝利ク" => 3,
				"未勝利障" => 2,
				"新馬ク" => 1,
				_ => 0
			};
		}

		private static float CalculateSameDistanceTimeIndex(List<RaceResult> races, int distance)
		{
			var sameDistanceRaces = races.Where(r => r.Race.Distance == distance);
			if (!sameDistanceRaces.Any()) return 50.0f; // デフォルト偏差値

			// 簡易タイム偏差値計算
			var times = sameDistanceRaces.Select(r => r.Time);
			var avgTime = times.Average();
			var standardTime = GetStandardTime(distance);
			return (standardTime - avgTime) / 2.0f + 50.0f; // 偏差値化
		}

		private static float CalculateLastRaceTimeDeviation(List<RaceResult> races)
		{
			if (!races.Any()) return 0;
			var lastRace = races.First();
			var standardTime = GetStandardTime(lastRace.Race.Distance);
			return standardTime - lastRace.Time;
		}

		private static float CalculateTimeConsistency(List<RaceResult> races)
		{
			if (races.Count < 2) return 1.0f;
			var timeDeviations = races.Take(5).Select(r => GetStandardTime(r.Race.Distance) - r.Time);
			var stdDev = CalculateStandardDeviation(timeDeviations.ToArray());
			return 1.0f / (stdDev + 1.0f);
		}

		private static float CalculateAptitudeReliability(List<RaceResult> races, Race currentRace)
		{
			var relevantRaces = races.Count(r =>
				r.Race.Distance == currentRace.Distance ||
				r.Race.TrackType == currentRace.TrackType ||
				r.Race.TrackCondition == currentRace.TrackCondition);

			return Math.Min(relevantRaces * 0.2f, 1.0f);
		}

		private static float GetStandardTime(int distance)
		{
			return distance switch
			{
				1000 => 58.5f,
				1200 => 70.5f,
				1400 => 83.2f,
				1600 => 95.1f,
				1800 => 109.8f,
				2000 => 123.2f,
				2400 => 148.5f,
				_ => distance * 0.061f // 概算
			};
		}

		private static float CalculateStandardDeviation(float[] values)
		{
			if (values.Length < 2) return 1.0f;
			var mean = values.Average();
			var variance = values.Select(v => (v - mean) * (v - mean)).Average();
			return (float)Math.Sqrt(variance);
		}
	}

	// ===== 機械学習モデル =====

	public class HorseRacingPredictionModel
	{
		private readonly MLContext _mlContext;
		private ITransformer _model;

		public HorseRacingPredictionModel()
		{
			_mlContext = new MLContext(seed: 1);
		}

		public void TrainModel(IEnumerable<OptimizedHorseFeatures> trainingData)
		{
			var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

			var pipeline = _mlContext.Transforms
				// 重要特徴量の正規化
				.NormalizeMeanVariance("Recent3AdjustedAvg")
				.Append(_mlContext.Transforms.NormalizeMeanVariance("LastRaceAdjustedScore"))
				.Append(_mlContext.Transforms.NormalizeMeanVariance("CurrentDistanceAptitude"))
				.Append(_mlContext.Transforms.NormalizeMeanVariance("JockeyCurrentConditionAvg"))
				.Append(_mlContext.Transforms.NormalizeMeanVariance("TrainerCurrentConditionAvg"))
				.Append(_mlContext.Transforms.NormalizeMeanVariance("WeightChange"))

				// 全特徴量結合
				.Append(_mlContext.Transforms.Concatenate("Features",
					// Phase 1: 最重要特徴量
					"Recent3AdjustedAvg", "LastRaceAdjustedScore", "BestAdjustedScore",
					"CurrentDistanceAptitude", "CurrentTrackTypeAptitude", "CurrentTrackConditionAptitude",
					"JockeyCurrentConditionAvg", "TrainerCurrentConditionAvg",

					// Phase 2: 重要特徴量
					"Recent5AdjustedAvg", "OverallAdjustedAvg", "AdjustedConsistency",
					"HeavyTrackAptitude", "SpecificCourseAptitude",
					"SprintAptitude", "MileAptitude", "MiddleDistanceAptitude", "LongDistanceAptitude",
					"JockeyOverallInverseAvg", "TrainerOverallInverseAvg",

					// Phase 3: 新馬・体重・状態
					"TrainerNewHorseInverse", "JockeyNewHorseInverse", "SireNewHorseInverse", "BreederSuccessRate",
					"WeightChange", "PersonalWeightDeviation", "IsRapidWeightChange",
					"RestDays", "Age", "Popularity", "PerformanceTrend",
					"DistanceChangeAdaptation", "ClassChangeAdaptation",

					// Phase 4: タイム・メタ情報
					"SameDistanceTimeIndex", "LastRaceTimeDeviation", "TimeConsistencyScore",
					"IsNewHorse", "HasRaceExperience", "AptitudeReliability"))

				// LightGBMランキング学習
				.Append(_mlContext.Ranking.Trainers.LightGbm(
					labelColumnName: "Label",
					featureColumnName: "Features",
					rowGroupColumnName: "RaceId"));

			_model = pipeline.Fit(dataView);
		}

		public List<HorsePrediction> PredictRace(List<Horse> horses, Race race, List<RaceResult> jockeyRaces, List<RaceResult> trainerRaces)
		{
			var features = horses.Select(horse =>
				FeatureExtractor.ExtractFeatures(horse, race, horses, jockeyRaces, trainerRaces)).ToList();

			var dataView = _mlContext.Data.LoadFromEnumerable(features);
			var predictions = _model.Transform(dataView);

			var scores = predictions.GetColumn<float>("Score").ToArray();

			var results = horses.Select((horse, index) => new HorsePrediction
			{
				Horse = horse,
				Score = scores[index],
				PredictedRank = 0, // 後で設定
				Confidence = CalculateConfidence(horse, features[index])
			}).OrderByDescending(p => p.Score).ToList();

			// 順位設定
			for (int i = 0; i < results.Count; i++)
			{
				results[i].PredictedRank = i + 1;
			}

			return results;
		}

		private float CalculateConfidence(Horse horse, OptimizedHorseFeatures features)
		{
			float confidence = 0.5f; // ベースライン

			// 経験による信頼度
			if (horse.RaceCount >= 5)
				confidence += 0.2f;
			else if (horse.RaceCount >= 2)
				confidence += 0.1f;

			// 条件適性の信頼度
			confidence += features.AptitudeReliability * 0.2f;

			// 最近の実績による信頼度
			if (features.Recent3AdjustedAvg > 0.3f)
				confidence += 0.1f;

			return Math.Min(confidence, 1.0f);
		}

		public void SaveModel(string filePath)
		{
			if (_model == null)
				throw new InvalidOperationException("モデルが訓練されていません。先にTrainModelを実行してください。");

			var directory = Path.GetDirectoryName(filePath);
			if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
			{
				Directory.CreateDirectory(directory);
			}

			// ML.NET 4.0.2でのモデル保存方法
			using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Write);
			_mlContext.Model.Save(_model, null, fileStream);
			MainViewModel.AddLog($"モデルを保存しました: {filePath}");
		}

		public void LoadModel(string filePath)
		{
			if (!File.Exists(filePath))
				throw new FileNotFoundException($"モデルファイルが見つかりません: {filePath}");

			// ML.NET 4.0.2でのモデル読み込み方法
			using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
			_model = _mlContext.Model.Load(fileStream, out var modelInputSchema);
			MainViewModel.AddLog($"モデルを読み込みました: {filePath}");
		}

		public bool IsModelTrained => _model != null;

		/// <summary>
		/// モデルの訓練と自動保存
		/// </summary>
		public void TrainAndSaveModel(IEnumerable<OptimizedHorseFeatures> trainingData, string saveFilePath)
		{
			MainViewModel.AddLog("モデル訓練を開始します...");
			var startTime = DateTime.Now;

			TrainModel(trainingData);

			var trainTime = DateTime.Now - startTime;
			MainViewModel.AddLog($"訓練完了: {trainTime.TotalSeconds:F1}秒");

			SaveModel(saveFilePath);
		}

		/// <summary>
		/// モデルファイルが存在すれば読み込み、なければ訓練して保存
		/// </summary>
		public void LoadOrTrainModel(IEnumerable<OptimizedHorseFeatures> trainingData, string modelFilePath)
		{
			if (File.Exists(modelFilePath))
			{
				MainViewModel.AddLog("既存のモデルファイルを読み込みます...");
				LoadModel(modelFilePath);
			}
			else
			{
				MainViewModel.AddLog("モデルファイルが見つかりません。新しく訓練します...");
				TrainAndSaveModel(trainingData, modelFilePath);
			}
		}
	}

	public class HorsePrediction
	{
		public Horse Horse { get; set; }
		public float Score { get; set; }
		public int PredictedRank { get; set; }
		public float Confidence { get; set; }
	}

}