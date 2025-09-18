using Codeplex.Data;
using MathNet.Numerics;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using TBird.Core;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ListView;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;
using static Tensorboard.CodeDef.Types;

namespace Netkeiba
{
	// ===== データモデル =====

	public class Race
	{
		public Race(Dictionary<string, object> x)
		{
			try
			{
				RaceId = x.Get("ﾚｰｽID").Str();
				CourseName = x.Get("ﾚｰｽ名").Str();
				Place = x.Get("開催場所").Str();
				Distance = x.Get("距離").Int32();
				DistanceCategory = Distance.ToDistanceCategory();
				Track = x.Get("馬場").Str();
				TrackType = Track.ToTrackType();
				TrackCondition = x.Get("馬場状態").Str();
				TrackConditionType = TrackCondition.ToTrackConditionType();
				Grade = x.Get("ﾗﾝｸ1").Str().ToGrade();
				FirstPrizeMoney = x.Get("優勝賞金").Int64();
				NumberOfHorses = x.Get("頭数").Int32();
				RaceDate = x.Get("開催日").Date();
				IsInternational = Grade.IsG1() && FirstPrizeMoney > 200000000;
				IsAgedHorseRace = Grade.IsCLASSIC() == false;
			}
			catch (Exception ex)
			{
				MessageService.Debug(ex.ToString());
				throw;
			}
		}

		public string RaceId { get; }
		public string CourseName { get; }
		public string Place { get; private set; }
		public int Distance { get; private set; }
		public DistanceCategory DistanceCategory { get; private set; }
		public string Track { get; }
		public TrackType TrackType { get; private set; }
		public string TrackCondition { get; }
		public TrackConditionType TrackConditionType { get; private set; }
		public GradeType Grade { get; private set; }
		public long FirstPrizeMoney { get; private set; }
		public int NumberOfHorses { get; private set; }
		public DateTime RaceDate { get; private set; }
		public float AverageRating { get; set; }
		public bool IsInternational { get; }
		public bool IsAgedHorseRace { get; }
	}

	public class RaceDetail
	{
		public RaceDetail(Dictionary<string, object> x, Race race)
		{
			try
			{
				Race = race;
				Horse = x.Get("馬ID").Str();
				Jockey = x.Get("騎手ID").Str();
				Trainer = x.Get("調教師ID").Str();
				Sire = x.Get("父ID").Str();
				DamSire = x.Get("母父ID").Str();
				Breeder = x.Get("生産者ID").Str();
				FinishPosition = x.Get("着順").Int32();
				Time = x.Get("ﾀｲﾑ変換").Single();
				PrizeMoney = x.Get("賞金").Single();
				PurchasePrice = x.Get("購入額").Single();
				BirthDate = x.Get("生年月日").Date();

				Age = (Race.RaceDate - BirthDate).TotalDays.Single() / 365F;
			}
			catch (Exception ex)
			{
				MessageService.Debug(ex.ToString());
				throw;
			}
		}

		public Race Race { get; }

		public string RaceId => Race.RaceId;
		public string Horse { get; }
		public string Jockey { get; }
		public string Trainer { get; }
		public string Sire { get; }
		public string DamSire { get; }
		public string SireDamSire => $"{Sire}-{DamSire}";
		public string Breeder { get; }
		public int FinishPosition { get; }
		public float Time { get; }
		public float PrizeMoney { get; }
		public float PurchasePrice { get; }
		public DateTime BirthDate { get; }
		public float Age { get; }

		public int RaceCount { get; private set; }
		public float AverageRating { get; private set; }
		public DateTime LastRaceDate { get; private set; }

		public float CalculateAdjustedInverseScore() => AdjustedPerformanceCalculator.CalculateAdjustedInverseScore(FinishPosition, Race);

		public void Initialize(List<RaceDetail> horses)
		{
			RaceCount = horses.Count;
			AverageRating = horses.Select(x => x.PrizeMoney)
				.DefaultIfEmpty(PurchasePrice / 10000F / 10F)
				.Average();

			if (horses.Count == 0)
			{
				LastRaceDate = Race.RaceDate.AddMonths(-2);
			}
			else
			{
				LastRaceDate = horses.First().Race.RaceDate;
			}
		}

		public OptimizedHorseFeaturesModel ExtractFeatures(List<RaceDetail> horses, RaceDetail[] inraces, List<RaceDetail> jockeys, List<RaceDetail> trainers, List<RaceDetail> sires, List<RaceDetail> damsires, List<RaceDetail> siredamsires, List<RaceDetail> breeders)
		{
			float GetStandardTime(int distance)
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

			float CalculateStandardDeviation(float[] values)
			{
				if (values.Length < 2) return 1.0f;
				var mean = values.Average();
				var variance = values.Select(v => (v - mean) * (v - mean)).Average();
				return (float)Math.Sqrt(variance);
			}

			float CalculateDistanceChangeAdaptation()
			{
				if (!horses.Any()) return 0.5f;
				var lastDistance = horses.First().Race.Distance;
				var distanceChange = Math.Abs(Race.Distance - lastDistance);
				return 1.0f / (1.0f + distanceChange / 400.0f); // 400m変化で半減
			}

			float CalculateClassChangeAdaptation()
			{
				if (!horses.Any()) return 0.5f;
				var lastGrade = horses.First().Race.Grade;
				var gradeChange = Race.Grade.Int32() - lastGrade.Int32();
				return gradeChange <= 0 ? 1.0f : 1.0f / (1.0f + gradeChange * 0.2f);
			}

			float CalculateSameDistanceTimeIndex(int distance)
			{
				var sameDistanceRaces = horses.Where(r => r.Race.Distance == distance);
				if (!sameDistanceRaces.Any()) return 50.0f; // デフォルト偏差値

				// 簡易タイム偏差値計算
				var times = sameDistanceRaces.Select(r => r.Time);
				var avgTime = times.Average();
				var standardTime = GetStandardTime(distance);
				return (standardTime - avgTime) / 2.0f + 50.0f; // 偏差値化
			}

			float CalculateLastRaceTimeDeviation()
			{
				if (!horses.Any()) return 0;
				var lastRace = horses.First();
				var standardTime = GetStandardTime(lastRace.Race.Distance);
				return standardTime - lastRace.Time;
			}

			float CalculateTimeConsistency()
			{
				if (horses.Count < 2) return 1.0f;
				var timeDeviations = horses.Take(5).Select(r => GetStandardTime(r.Race.Distance) - r.Time);
				var stdDev = CalculateStandardDeviation(timeDeviations.ToArray());
				return 1.0f / (stdDev + 1.0f);
			}

			float CalculateAptitudeReliability()
			{
				var relevantRaces = horses.Count(r =>
					r.Race.Distance == Race.Distance ||
					r.Race.TrackType == Race.TrackType ||
					r.Race.TrackConditionType == Race.TrackConditionType);

				return Math.Min(relevantRaces * 0.2f, 1.0f);
			}

			var adjustedMetrics = AdjustedPerformanceCalculator.CalculateAdjustedPerformance(Race, this, horses);
			var connectionMetrics = ConnectionAnalyzer.AnalyzeConnections(Race, jockeys, trainers, breeders, sires, damsires, siredamsires);
			var conditionMetrics = ConditionAptitudeCalculator.CalculateConditionMetrics(horses, Race);

			var features = new OptimizedHorseFeaturesModel(this)
			{
				// 基本実績
				Recent3AdjustedAvg = adjustedMetrics.Recent3AdjustedAvg,
				Recent5AdjustedAvg = adjustedMetrics.Recent5AdjustedAvg,
				LastRaceAdjustedScore = adjustedMetrics.LastRaceAdjustedScore,
				BestAdjustedScore = adjustedMetrics.BestAdjustedScore,
				AdjustedConsistency = adjustedMetrics.AdjustedConsistency,
				OverallAdjustedAvg = adjustedMetrics.OverallAdjustedAvg,

				// 条件適性
				CurrentDistanceAptitude = conditionMetrics.CurrentDistanceAptitude,
				CurrentTrackTypeAptitude = conditionMetrics.CurrentTrackTypeAptitude,
				CurrentTrackConditionAptitude = conditionMetrics.CurrentTrackConditionAptitude,
				HeavyTrackAptitude = conditionMetrics.HeavyTrackAptitude,
				SpecificCourseAptitude = conditionMetrics.SpecificCourseAptitude,

				// 距離適性
				SprintAptitude = conditionMetrics.SprintAptitude,
				MileAptitude = conditionMetrics.MileAptitude,
				MiddleDistanceAptitude = conditionMetrics.MiddleDistanceAptitude,
				LongDistanceAptitude = conditionMetrics.LongDistanceAptitude,

				// 関係者実績
				JockeyOverallInverseAvg = connectionMetrics.JockeyOverallInverseAvg,
				JockeyRecentInverseAvg = connectionMetrics.JockeyRecentInverseAvg,
				JockeyCurrentConditionAvg = connectionMetrics.JockeyCurrentConditionAvg,
				TrainerOverallInverseAvg = connectionMetrics.TrainerOverallInverseAvg,
				TrainerRecentInverseAvg = connectionMetrics.TrainerRecentInverseAvg,
				TrainerCurrentConditionAvg = connectionMetrics.TrainerCurrentConditionAvg,
				BreederOverallInverseAvg = connectionMetrics.BreederOverallInverseAvg,
				BreederRecentInverseAvg = connectionMetrics.BreederRecentInverseAvg,
				BreederCurrentConditionAvg = connectionMetrics.BreederCurrentConditionAvg,
				SireOverallInverseAvg = connectionMetrics.SireOverallInverseAvg,
				SireRecentInverseAvg = connectionMetrics.SireRecentInverseAvg,
				SireCurrentConditionAvg = connectionMetrics.SireCurrentConditionAvg,
				DamSireOverallInverseAvg = connectionMetrics.DamSireOverallInverseAvg,
				DamSireRecentInverseAvg = connectionMetrics.DamSireRecentInverseAvg,
				DamSireCurrentConditionAvg = connectionMetrics.DamSireCurrentConditionAvg,
				SireDamSireOverallInverseAvg = connectionMetrics.SireDamSireOverallInverseAvg,
				SireDamSireRecentInverseAvg = connectionMetrics.SireDamSireRecentInverseAvg,
				SireDamSireCurrentConditionAvg = connectionMetrics.SireDamSireCurrentConditionAvg,

				// 状態・変化
				RestDays = (Race.RaceDate - LastRaceDate).Days,
				Age = Age,
				Popularity = 1F,
				PerformanceTrend = adjustedMetrics.Recent3AdjustedAvg - adjustedMetrics.OverallAdjustedAvg,
				DistanceChangeAdaptation = CalculateDistanceChangeAdaptation(),
				ClassChangeAdaptation = CalculateClassChangeAdaptation(),

				// タイム関連
				SameDistanceTimeIndex = CalculateSameDistanceTimeIndex(Race.Distance),
				LastRaceTimeDeviation = CalculateLastRaceTimeDeviation(),
				TimeConsistencyScore = CalculateTimeConsistency(),

				// メタ情報
				IsNewHorse = RaceCount == 0,
				HasRaceExperience = RaceCount > 0,
				AptitudeReliability = CalculateAptitudeReliability(),

				// ラベル（トレーニング時に設定）
				Label = 0, // 実際の着順から計算
			};

			// 新馬の場合は特別処理
			if (RaceCount == 0)
			{
				var newHorseMetrics = MaidenRaceAnalyzer.AnalyzeNewHorse(this, inraces, horses, jockeys, trainers, breeders, sires, damsires);
				features.TrainerNewHorseInverse = newHorseMetrics.TrainerNewHorseInverse;
				features.JockeyNewHorseInverse = newHorseMetrics.JockeyNewHorseInverse;
				features.SireNewHorseInverse = newHorseMetrics.SireNewHorseInverse;
				features.DamSireNewHorseInverse = newHorseMetrics.DamSireNewHorseInverse;
				features.BreederNewHorseInverse = newHorseMetrics.BreederNewHorseInverse;
				features.PurchasePriceRank = newHorseMetrics.PurchasePriceRank;
			}

			return features;

		}
	}

	// ===== CSVファイル用のデータクラス =====

	// ===== 特徴量クラス =====

	public class OptimizedHorseFeatures
	{
		public OptimizedHorseFeatures() : this(string.Empty)
		{

		}

		public OptimizedHorseFeatures(string raceId)
		{
			RaceId = raceId;
		}

		// 馬の基本実績（難易度調整済み）
		[LoadColumn(0)] public float Recent3AdjustedAvg { get; set; }
		[LoadColumn(1)] public float Recent5AdjustedAvg { get; set; }
		[LoadColumn(2)] public float LastRaceAdjustedScore { get; set; }
		[LoadColumn(3)] public float BestAdjustedScore { get; set; }
		[LoadColumn(4)] public float AdjustedConsistency { get; set; }
		[LoadColumn(5)] public float OverallAdjustedAvg { get; set; }

		// 条件適性（今回のレース条件に対する適性）
		[LoadColumn(6)] public float CurrentDistanceAptitude { get; set; }
		[LoadColumn(7)] public float CurrentTrackTypeAptitude { get; set; }
		[LoadColumn(8)] public float CurrentTrackConditionAptitude { get; set; }
		[LoadColumn(9)] public float HeavyTrackAptitude { get; set; }
		[LoadColumn(10)] public float SpecificCourseAptitude { get; set; }

		// 距離カテゴリ別適性
		[LoadColumn(11)] public float SprintAptitude { get; set; }
		[LoadColumn(12)] public float MileAptitude { get; set; }
		[LoadColumn(13)] public float MiddleDistanceAptitude { get; set; }
		[LoadColumn(14)] public float LongDistanceAptitude { get; set; }

		// 関係者実績（条件特化）
		[LoadColumn(15)] public float JockeyOverallInverseAvg { get; set; }
		[LoadColumn(16)] public float JockeyRecentInverseAvg { get; set; }
		[LoadColumn(17)] public float JockeyCurrentConditionAvg { get; set; }

		[LoadColumn(18)] public float TrainerOverallInverseAvg { get; set; }
		[LoadColumn(19)] public float TrainerRecentInverseAvg { get; set; }
		[LoadColumn(20)] public float TrainerCurrentConditionAvg { get; set; }

		[LoadColumn(21)] public float BreederOverallInverseAvg { get; set; }
		[LoadColumn(22)] public float BreederRecentInverseAvg { get; set; }
		[LoadColumn(23)] public float BreederCurrentConditionAvg { get; set; }

		[LoadColumn(24)] public float SireOverallInverseAvg { get; set; }
		[LoadColumn(25)] public float SireRecentInverseAvg { get; set; }
		[LoadColumn(26)] public float SireCurrentConditionAvg { get; set; }

		[LoadColumn(27)] public float DamSireOverallInverseAvg { get; set; }
		[LoadColumn(28)] public float DamSireRecentInverseAvg { get; set; }
		[LoadColumn(29)] public float DamSireCurrentConditionAvg { get; set; }

		[LoadColumn(30)] public float SireDamSireOverallInverseAvg { get; set; }
		[LoadColumn(31)] public float SireDamSireRecentInverseAvg { get; set; }
		[LoadColumn(32)] public float SireDamSireCurrentConditionAvg { get; set; }

		// 新馬用特徴量
		[LoadColumn(33)] public float TrainerNewHorseInverse { get; set; }
		[LoadColumn(34)] public float JockeyNewHorseInverse { get; set; }
		[LoadColumn(35)] public float SireNewHorseInverse { get; set; }
		[LoadColumn(36)] public float DamSireNewHorseInverse { get; set; }
		[LoadColumn(37)] public float BreederNewHorseInverse { get; set; }
		[LoadColumn(38)] public float PurchasePriceRank { get; set; }

		// 馬の状態・変化指標
		[LoadColumn(39)] public int RestDays { get; set; }
		[LoadColumn(40)] public float Age { get; set; }
		[LoadColumn(41)] public float Popularity { get; set; }
		[LoadColumn(42)] public float PerformanceTrend { get; set; }
		[LoadColumn(43)] public float DistanceChangeAdaptation { get; set; }
		[LoadColumn(44)] public float ClassChangeAdaptation { get; set; }

		// タイム関連（正規化済み）
		[LoadColumn(45)] public float SameDistanceTimeIndex { get; set; }
		[LoadColumn(46)] public float LastRaceTimeDeviation { get; set; }
		[LoadColumn(47)] public float TimeConsistencyScore { get; set; }

		// メタ情報
		[LoadColumn(48)] public bool IsNewHorse { get; set; }
		[LoadColumn(49)] public bool HasRaceExperience { get; set; }
		[LoadColumn(50)] public float AptitudeReliability { get; set; }

		// ラベル・グループ情報
		[LoadColumn(51)] public float Label { get; set; }
		[LoadColumn(52)] public string RaceId { get; set; }

	}

	public class OptimizedHorseFeaturesModel : OptimizedHorseFeatures
	{
		public OptimizedHorseFeaturesModel() : base()
		{
			HorseName = string.Empty;
		}

		public OptimizedHorseFeaturesModel(RaceDetail detail) : base(detail.RaceId)
		{
			HorseName = detail.Horse;
		}

		public string HorseName { get; set; }

		public string Serialize() => DynamicJson.Serialize(this);

		public static OptimizedHorseFeaturesModel? Deserialize(string json)
		{
			var obj = DynamicJson.Parse(json);
			return obj?.Deserialize<OptimizedHorseFeaturesModel>() ?? default;
		}
	}

	public enum GradeType
	{
		// G1
		G1古 = 20,

		G1ク = 19,

		G1障 = 18,

		// G2
		G2古 = 17,

		G2ク = 16,

		G2障 = 15,

		// G3
		G3古 = 14,

		G3ク = 13,

		G3障 = 12,

		// オープン
		オープン古 = 11,

		オープンク = 10,

		オープン障 = 9,

		// 条件戦
		勝3古 = 8,

		勝2古 = 7,

		勝2ク = 6,

		勝1古 = 5,

		勝1ク = 4,

		// 未勝利・新馬
		未勝利ク = 3,

		未勝利障 = 2,

		新馬ク = 1,

	}

	public enum DistanceCategory
	{
		Sprint,
		Mile,
		Middle,
		Long
	}

	public enum TrackType
	{
		// 芝
		Grass,

		// ダート
		Dirt,

		Unknown
	}

	public enum TrackConditionType
	{
		// 良
		Good,

		// 稍重
		SlightlyHeavy,

		// 重
		Heavy,

		// 不良
		Poor,

		Unknown,
	}

	public static class ModelExtensions
	{
		public static GradeType ToGrade(this string grade) => EnumUtil.ToEnum<GradeType>(grade);

		public static bool IsG1(this GradeType grade) => grade switch
		{
			GradeType.G1ク => true,
			GradeType.G1古 => true,
			GradeType.G1障 => true,
			_ => false
		};

		public static bool IsG2(this GradeType grade) => grade switch
		{
			GradeType.G2ク => true,
			GradeType.G2古 => true,
			GradeType.G2障 => true,
			_ => false
		};

		public static bool IsG3(this GradeType grade) => grade switch
		{
			GradeType.G3ク => true,
			GradeType.G3古 => true,
			GradeType.G3障 => true,
			_ => false
		};

		public static bool IsOPEN(this GradeType grade) => grade switch
		{
			GradeType.オープンク => true,
			GradeType.オープン古 => true,
			GradeType.オープン障 => true,
			_ => false
		};

		public static bool IsCLASSIC(this GradeType grade) => grade switch
		{
			GradeType.G1ク => true,
			GradeType.G2ク => true,
			GradeType.G3ク => true,
			GradeType.オープンク => true,
			GradeType.勝2ク => true,
			GradeType.勝1ク => true,
			GradeType.未勝利ク => true,
			GradeType.新馬ク => true,
			_ => false,
		};

		public static DistanceCategory ToDistanceCategory(this int distance) => distance switch
		{
			<= 1400 => DistanceCategory.Sprint,
			<= 1800 => DistanceCategory.Mile,
			<= 2200 => DistanceCategory.Middle,
			_ => DistanceCategory.Long
		};

		public static TrackType ToTrackType(this string track) => track switch
		{
			"芝" => TrackType.Grass,
			"ダート" => TrackType.Dirt,
			_ => TrackType.Unknown
		};

		public static TrackConditionType ToTrackConditionType(this string condition) => condition switch
		{
			"良" => TrackConditionType.Good,
			"稍重" => TrackConditionType.SlightlyHeavy,
			"重" => TrackConditionType.Heavy,
			"不良" => TrackConditionType.Poor,
			_ => TrackConditionType.Unknown
		};

		public static float AdjustedInverseScoreAverage(this IEnumerable<RaceDetail> arr, float def = 0.1F) => arr.Aggregate(tmp => tmp.Average(x => x.CalculateAdjustedInverseScore()), def);
	}
}