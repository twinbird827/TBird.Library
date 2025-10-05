using Codeplex.Data;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba.Models
{
	public class OptimizedHorseFeatures
	{
		public OptimizedHorseFeatures() : this(string.Empty)
		{

		}

		public OptimizedHorseFeatures(string raceId)
		{
			RaceId = raceId;
		}

		public static string[] GetNormalizationItemNames() => new[]
		{
			nameof(RestDays),
			nameof(Age),
			nameof(Gender),
			nameof(Season),
			nameof(RaceDistance),
			nameof(SameDistanceTimeIndex),
			nameof(LastRaceTimeDeviation),
			nameof(AdjustedLastThreeFurlongsAvg),
			nameof(LastRaceAdjustedLastThreeFurlongs),
			nameof(JockeyWeightDiff),
			nameof(LastRaceFinishPosition),
			nameof(Recent3AvgFinishPosition),
			nameof(FinishPositionImprovement),
			nameof(CurrentGrade),
			nameof(CurrentTrackCondition),
			nameof(TrackConditionChangeFromLast),
			nameof(SameCourseExperience),
			nameof(SameDistanceCategoryExperience),
			nameof(SameTrackTypeExperience),
			nameof(PurchasePriceRank),
		};

		public static string[] GetAdjustedPerformanceItemNames() => new[]
		{
			nameof(Recent3AdjustedAvg),
			nameof(Recent5AdjustedAvg),
			nameof(LastRaceAdjustedScore),
			nameof(AdjustedConsistency),
		};

		// 馬の基本実績（難易度調整済み）
		[LoadColumn(0)] public float Recent3AdjustedAvg { get; set; }
		[LoadColumn(1)] public float Recent5AdjustedAvg { get; set; }
		[LoadColumn(2)] public float LastRaceAdjustedScore { get; set; }
		[LoadColumn(3)] public float AdjustedConsistency { get; set; }

		public static string[] GetCondition1ItemNames() => new[]
		{
			nameof(CurrentDistanceAptitude),
			nameof(CurrentTrackTypeAptitude),
			nameof(CurrentTrackConditionAptitude),
		};

		// 条件適性（今回のレース条件に対する適性）
		[LoadColumn(4)] public float CurrentDistanceAptitude { get; set; }
		[LoadColumn(5)] public float CurrentTrackTypeAptitude { get; set; }
		[LoadColumn(6)] public float CurrentTrackConditionAptitude { get; set; }

		public static string[] GetConnectionItemNames() => new[]
		{
			nameof(JockeyRecentInverseAvg),
			nameof(JockeyCurrentConditionAvg),
			nameof(TrainerRecentInverseAvg),
			nameof(TrainerCurrentConditionAvg),
			nameof(BreederRecentInverseAvg),
			nameof(BreederCurrentConditionAvg),
			nameof(SireRecentInverseAvg),
			nameof(SireCurrentConditionAvg),
			nameof(DamSireRecentInverseAvg),
			nameof(DamSireCurrentConditionAvg),
			nameof(SireDamSireRecentInverseAvg),
			nameof(SireDamSireCurrentConditionAvg),
			nameof(JockeyTrainerRecentInverseAvg),
			nameof(JockeyTrainerCurrentConditionAvg),
		};

		// 関係者実績（条件特化）
		[LoadColumn(7)] public float JockeyRecentInverseAvg { get; set; }
		[LoadColumn(8)] public float JockeyCurrentConditionAvg { get; set; }

		[LoadColumn(9)] public float TrainerRecentInverseAvg { get; set; }
		[LoadColumn(10)] public float TrainerCurrentConditionAvg { get; set; }

		[LoadColumn(11)] public float BreederRecentInverseAvg { get; set; }
		[LoadColumn(12)] public float BreederCurrentConditionAvg { get; set; }

		[LoadColumn(13)] public float SireRecentInverseAvg { get; set; }
		[LoadColumn(14)] public float SireCurrentConditionAvg { get; set; }

		[LoadColumn(15)] public float DamSireRecentInverseAvg { get; set; }
		[LoadColumn(16)] public float DamSireCurrentConditionAvg { get; set; }

		[LoadColumn(17)] public float SireDamSireRecentInverseAvg { get; set; }
		[LoadColumn(18)] public float SireDamSireCurrentConditionAvg { get; set; }

		[LoadColumn(19)] public float JockeyTrainerRecentInverseAvg { get; set; }
		[LoadColumn(20)] public float JockeyTrainerCurrentConditionAvg { get; set; }

		public static string[] GetNewHorseItemNames() => new[]
		{
			nameof(TrainerNewHorseInverse),
			nameof(JockeyNewHorseInverse),
			nameof(SireNewHorseInverse),
			nameof(DamSireNewHorseInverse),
			nameof(BreederNewHorseInverse),
			nameof(PurchasePriceRank),
		};

		// 新馬用特徴量
		[LoadColumn(21)] public float TrainerNewHorseInverse { get; set; }
		[LoadColumn(22)] public float JockeyNewHorseInverse { get; set; }
		[LoadColumn(23)] public float SireNewHorseInverse { get; set; }
		[LoadColumn(24)] public float DamSireNewHorseInverse { get; set; }
		[LoadColumn(25)] public float BreederNewHorseInverse { get; set; }
		[LoadColumn(26)] public float PurchasePriceRank { get; set; }

		public static string[] GetStatusItemNames() => new[]
		{
			nameof(RestDays),
			nameof(IsRentoFlag),
			nameof(Age),
			nameof(Gender),
			nameof(Season),
			nameof(RaceDistance),
			nameof(PerformanceTrend),
			nameof(DistanceChangeAdaptation),
			nameof(ClassChangeAdaptation),
			nameof(JockeyWeightDiff),
			nameof(JockeyWeightDiffFromAvgInRace),
			nameof(AverageTuka),
			nameof(LastRaceTuka),
			nameof(TukaConsistency),
			nameof(AverageTukaInRace),
			nameof(LastRaceFinishPosition),
			nameof(Recent3AvgFinishPosition),
			nameof(FinishPositionImprovement),
			nameof(PaceAdvantageScore),
			nameof(CurrentGrade),
			nameof(ClassUpChallenge),
			nameof(CurrentTrackCondition),
			nameof(TrackConditionChangeFromLast),
			nameof(SameCourseExperience),
			nameof(SameDistanceCategoryExperience),
			nameof(SameTrackTypeExperience),
		};

		// 馬の状態・変化指標
		[LoadColumn(27)] public float RestDays { get; set; }
		[LoadColumn(28)] public bool IsRentoFlag { get; set; }
		[LoadColumn(29)] public float Age { get; set; }
		[LoadColumn(30)] public float Gender { get; set; }
		[LoadColumn(31)] public float Season { get; set; }
		[LoadColumn(32)] public float RaceDistance { get; set; }
		[LoadColumn(33)] public float PerformanceTrend { get; set; }
		[LoadColumn(34)] public float DistanceChangeAdaptation { get; set; }
		[LoadColumn(35)] public float ClassChangeAdaptation { get; set; }
		[LoadColumn(36)] public float JockeyWeightDiff { get; set; }
		[LoadColumn(37)] public float JockeyWeightDiffFromAvgInRace { get; set; }
		[LoadColumn(38)] public float AverageTuka { get; set; }
		[LoadColumn(39)] public float LastRaceTuka { get; set; }
		[LoadColumn(40)] public float TukaConsistency { get; set; }
		[LoadColumn(41)] public float AverageTukaInRace { get; set; }
		[LoadColumn(42)] public float LastRaceFinishPosition { get; set; }
		[LoadColumn(43)] public float Recent3AvgFinishPosition { get; set; }
		[LoadColumn(44)] public float FinishPositionImprovement { get; set; }
		[LoadColumn(45)] public float PaceAdvantageScore { get; set; }
		[LoadColumn(46)] public float CurrentGrade { get; set; }
		[LoadColumn(47)] public float ClassUpChallenge { get; set; }
		[LoadColumn(48)] public float CurrentTrackCondition { get; set; }
		[LoadColumn(49)] public float TrackConditionChangeFromLast { get; set; }
		[LoadColumn(50)] public float SameCourseExperience { get; set; }
		[LoadColumn(51)] public float SameDistanceCategoryExperience { get; set; }
		[LoadColumn(52)] public float SameTrackTypeExperience { get; set; }

		public static string[] GetTimeItemNames() => new[]
		{
			nameof(SameDistanceTimeIndex),
			nameof(LastRaceTimeDeviation),
			nameof(TimeConsistencyScore),
			nameof(AdjustedLastThreeFurlongsAvg),
			nameof(LastRaceAdjustedLastThreeFurlongs),
			nameof(AdjustedLastThreeFurlongsDiffFromAvgInRace),
		};

		// タイム関連（正規化済み）
		[LoadColumn(53)] public float SameDistanceTimeIndex { get; set; }
		[LoadColumn(54)] public float LastRaceTimeDeviation { get; set; }
		[LoadColumn(55)] public float TimeConsistencyScore { get; set; }
		[LoadColumn(56)] public float AdjustedLastThreeFurlongsAvg { get; set; }
		[LoadColumn(57)] public float LastRaceAdjustedLastThreeFurlongs { get; set; }
		[LoadColumn(58)] public float AdjustedLastThreeFurlongsDiffFromAvgInRace { get; set; }

		public static string[] GetMetadataNames() => new[]
		{
			nameof(IsNewHorse),
			nameof(AptitudeReliability),
		};

		// メタ情報
		[LoadColumn(59)] public bool IsNewHorse { get; set; }
		[LoadColumn(60)] public float AptitudeReliability { get; set; }

		// ラベル・グループ情報
		[LoadColumn(61)] public uint Label { get; set; }
		[LoadColumn(62)] public string RaceId { get; set; }

		public static string[] GetFlagItemNames() => new[]
		{
			nameof(IsNewHorse),
			nameof(IsRentoFlag),
		};

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
}