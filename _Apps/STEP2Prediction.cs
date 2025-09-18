using ControlzEx.Standard;
using HorseRacingPrediction;
using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

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
				multiplier *= CalculateQualityMultiplier(race.AverageRating);

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

	// ===== 特徴量抽出メインクラス =====

	// ===== 機械学習モデル =====

	//public class HorseRacingPredictionModel
	//{
	//	private readonly MLContext _mlContext;
	//	private ITransformer _model;

	//	public HorseRacingPredictionModel()
	//	{
	//		_mlContext = new MLContext(seed: 1);
	//	}

	//	public void TrainModel(IEnumerable<OptimizedHorseFeatures> trainingData)
	//	{
	//		var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);

	//		var pipeline = _mlContext.Transforms
	//			// 重要特徴量の正規化
	//			.NormalizeMeanVariance("Recent3AdjustedAvg")
	//			.Append(_mlContext.Transforms.NormalizeMeanVariance("LastRaceAdjustedScore"))
	//			.Append(_mlContext.Transforms.NormalizeMeanVariance("CurrentDistanceAptitude"))
	//			.Append(_mlContext.Transforms.NormalizeMeanVariance("JockeyCurrentConditionAvg"))
	//			.Append(_mlContext.Transforms.NormalizeMeanVariance("TrainerCurrentConditionAvg"))
	//			.Append(_mlContext.Transforms.NormalizeMeanVariance("WeightChange"))

	//			// 全特徴量結合
	//			.Append(_mlContext.Transforms.Concatenate("Features",
	//				// Phase 1: 最重要特徴量
	//				"Recent3AdjustedAvg", "LastRaceAdjustedScore", "BestAdjustedScore",
	//				"CurrentDistanceAptitude", "CurrentTrackTypeAptitude", "CurrentTrackConditionAptitude",
	//				"JockeyCurrentConditionAvg", "TrainerCurrentConditionAvg",

	//				// Phase 2: 重要特徴量
	//				"Recent5AdjustedAvg", "OverallAdjustedAvg", "AdjustedConsistency",
	//				"HeavyTrackAptitude", "SpecificCourseAptitude",
	//				"SprintAptitude", "MileAptitude", "MiddleDistanceAptitude", "LongDistanceAptitude",
	//				"JockeyOverallInverseAvg", "TrainerOverallInverseAvg",

	//				// Phase 3: 新馬・体重・状態
	//				"TrainerNewHorseInverse", "JockeyNewHorseInverse", "SireNewHorseInverse", "BreederSuccessRate",
	//				"WeightChange", "PersonalWeightDeviation", "IsRapidWeightChange",
	//				"RestDays", "Age", "Popularity", "PerformanceTrend",
	//				"DistanceChangeAdaptation", "ClassChangeAdaptation",

	//				// Phase 4: タイム・メタ情報
	//				"SameDistanceTimeIndex", "LastRaceTimeDeviation", "TimeConsistencyScore",
	//				"IsNewHorse", "HasRaceExperience", "AptitudeReliability"))

	//			// LightGBMランキング学習
	//			.Append(_mlContext.Ranking.Trainers.LightGbm(
	//				labelColumnName: "Label",
	//				featureColumnName: "Features",
	//				rowGroupColumnName: "RaceId"));

	//		_model = pipeline.Fit(dataView);
	//	}

	//	public List<HorsePrediction> PredictRace(List<Horse> horses, Race race, IDataRepository repo)
	//	{
	//		var features = horses
	//			.Select(horse => FeatureExtractor.ExtractFeatures(horse, race, new List<RaceResult>(), new List<RaceResult>(), repo) as OptimizedHorseFeatures)
	//			.ToList();

	//		var dataView = _mlContext.Data.LoadFromEnumerable(features);
	//		var predictions = _model.Transform(dataView);

	//		var scores = predictions.GetColumn<float>("Score").ToArray();

	//		var results = horses.Select((horse, index) => new HorsePrediction
	//		{
	//			Horse = horse,
	//			Score = scores[index],
	//			PredictedRank = 0, // 後で設定
	//			Confidence = CalculateConfidence(horse, features[index])
	//		}).OrderByDescending(p => p.Score).ToList();

	//		// 順位設定
	//		for (int i = 0; i < results.Count; i++)
	//		{
	//			results[i].PredictedRank = i + 1;
	//		}

	//		return results;
	//	}

	//	private float CalculateConfidence(Horse horse, OptimizedHorseFeatures features)
	//	{
	//		float confidence = 0.5f; // ベースライン

	//		// 経験による信頼度
	//		if (horse.RaceCount >= 5)
	//			confidence += 0.2f;
	//		else if (horse.RaceCount >= 2)
	//			confidence += 0.1f;

	//		// 条件適性の信頼度
	//		confidence += features.AptitudeReliability * 0.2f;

	//		// 最近の実績による信頼度
	//		if (features.Recent3AdjustedAvg > 0.3f)
	//			confidence += 0.1f;

	//		return Math.Min(confidence, 1.0f);
	//	}

	//	public void SaveModel(string filePath)
	//	{
	//		if (_model == null)
	//			throw new InvalidOperationException("モデルが訓練されていません。先にTrainModelを実行してください。");

	//		var directory = Path.GetDirectoryName(filePath);
	//		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
	//		{
	//			Directory.CreateDirectory(directory);
	//		}

	//		// ML.NET 4.0.2でのモデル保存方法
	//		using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.Write);
	//		_mlContext.Model.Save(_model, null, fileStream);
	//		MainViewModel.AddLog($"モデルを保存しました: {filePath}");
	//	}

	//	public void LoadModel(string filePath)
	//	{
	//		if (!File.Exists(filePath))
	//			throw new FileNotFoundException($"モデルファイルが見つかりません: {filePath}");

	//		// ML.NET 4.0.2でのモデル読み込み方法
	//		using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
	//		_model = _mlContext.Model.Load(fileStream, out var modelInputSchema);
	//		MainViewModel.AddLog($"モデルを読み込みました: {filePath}");
	//	}

	//	public bool IsModelTrained => _model != null;

	//	/// <summary>
	//	/// モデルの訓練と自動保存
	//	/// </summary>
	//	public void TrainAndSaveModel(IEnumerable<OptimizedHorseFeatures> trainingData, string saveFilePath)
	//	{
	//		MainViewModel.AddLog("モデル訓練を開始します...");
	//		var startTime = DateTime.Now;

	//		TrainModel(trainingData);

	//		var trainTime = DateTime.Now - startTime;
	//		MainViewModel.AddLog($"訓練完了: {trainTime.TotalSeconds:F1}秒");

	//		SaveModel(saveFilePath);
	//	}

	//	/// <summary>
	//	/// モデルファイルが存在すれば読み込み、なければ訓練して保存
	//	/// </summary>
	//	public void LoadOrTrainModel(IEnumerable<OptimizedHorseFeatures> trainingData, string modelFilePath)
	//	{
	//		if (File.Exists(modelFilePath))
	//		{
	//			MainViewModel.AddLog("既存のモデルファイルを読み込みます...");
	//			LoadModel(modelFilePath);
	//		}
	//		else
	//		{
	//			MainViewModel.AddLog("モデルファイルが見つかりません。新しく訓練します...");
	//			TrainAndSaveModel(trainingData, modelFilePath);
	//		}
	//	}
	//}

	//public class HorsePrediction
	//{
	//	public Horse Horse { get; set; }
	//	public float Score { get; set; }
	//	public int PredictedRank { get; set; }
	//	public float Confidence { get; set; }
	//}

}