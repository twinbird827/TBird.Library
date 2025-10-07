using Codeplex.Data;
using Microsoft.ML.Data;
using System.Collections.Generic;
using System.Linq;
using TBird.Core;

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
			//nameof(Gender),
			nameof(Umaban),
			// Season, RaceDistance: カテゴリ値のため正規化から除外
			nameof(SameDistanceTimeIndex),
			nameof(LastRaceTimeDeviation),
			nameof(AdjustedLastThreeFurlongsAvg),
			nameof(LastRaceAdjustedLastThreeFurlongs),
			nameof(JockeyWeightDiff),
			nameof(LastRaceFinishPosition),
			nameof(Recent3AvgFinishPosition),
			nameof(FinishPositionImprovement),
			// CurrentGrade, CurrentTrackCondition: カテゴリ値のため正規化から除外
			nameof(TrackConditionChangeFromLast),
			nameof(SameCourseExperience),
			nameof(SameDistanceCategoryExperience),
			nameof(SameTrackTypeExperience),
			nameof(PurchasePriceRank),
			nameof(BreederRecentInverseAvg),
			nameof(BreederCurrentConditionAvg),
			nameof(SireRecentInverseAvg),
			nameof(SireCurrentConditionAvg),
			nameof(DamSireRecentInverseAvg),
			nameof(DamSireCurrentConditionAvg),
			nameof(SireDamSireRecentInverseAvg),
			nameof(SireDamSireCurrentConditionAvg),
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
			nameof(CurrentGrade),
			nameof(CurrentTrackCondition),
			nameof(PaceAdvantageScore),
			nameof(ClassUpChallenge),
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
			nameof(AverageTimeIndex),
			nameof(LastRaceTimeIndex),
			nameof(AverageTimeIndexRankInRace),
		};

		// タイム関連（正規化済み）
		[LoadColumn(53)] public float SameDistanceTimeIndex { get; set; }
		[LoadColumn(54)] public float LastRaceTimeDeviation { get; set; }
		[LoadColumn(55)] public float TimeConsistencyScore { get; set; }
		[LoadColumn(56)] public float AdjustedLastThreeFurlongsAvg { get; set; }
		[LoadColumn(57)] public float LastRaceAdjustedLastThreeFurlongs { get; set; }
		[LoadColumn(58)] public float AdjustedLastThreeFurlongsDiffFromAvgInRace { get; set; }
		[LoadColumn(59)] public float AverageTimeIndex { get; set; }
		[LoadColumn(60)] public float LastRaceTimeIndex { get; set; }
		[LoadColumn(61)] public float AverageTimeIndexRankInRace { get; set; }

		public static string[] GetRacePositionItemNames() => new[]
		{
			nameof(Umaban),
		};

		[LoadColumn(62)] public int Umaban { get; set; }

		public static string[] GetMetadataNames() => new[]
		{
			nameof(IsNewHorse),
			nameof(AptitudeReliability),
		};

		// メタ情報
		[LoadColumn(63)] public bool IsNewHorse { get; set; }
		[LoadColumn(64)] public float AptitudeReliability { get; set; }

		// ラベル・グループ情報
		[LoadColumn(65)] public uint Label { get; set; }
		[LoadColumn(66)] public string RaceId { get; set; }

		public static string[] GetFlagItemNames() => new[]
		{
			nameof(IsNewHorse),
			nameof(IsRentoFlag),
			nameof(Umaban),
		};

	}

	public class OptimizedHorseFeaturesModel : OptimizedHorseFeatures
	{
		public OptimizedHorseFeaturesModel() : base()
		{
			Horse = string.Empty;
		}

		public OptimizedHorseFeaturesModel(RaceDetail detail) : base(detail.RaceId)
		{
			Horse = detail.Horse;
		}

		public string Horse { get; set; }

		public string Serialize() => DynamicJson.Serialize(this);

		public static OptimizedHorseFeaturesModel? Deserialize(Dictionary<string, object> x)
		{
			var instance = new OptimizedHorseFeaturesModel();

			instance.Recent3AdjustedAvg = x["Recent3AdjustedAvg"].Single();
			instance.Recent5AdjustedAvg = x["Recent5AdjustedAvg"].Single();
			instance.LastRaceAdjustedScore = x["LastRaceAdjustedScore"].Single();
			instance.AdjustedConsistency = x["AdjustedConsistency"].Single();
			instance.CurrentDistanceAptitude = x["CurrentDistanceAptitude"].Single();
			instance.CurrentTrackTypeAptitude = x["CurrentTrackTypeAptitude"].Single();
			instance.CurrentTrackConditionAptitude = x["CurrentTrackConditionAptitude"].Single();
			instance.JockeyRecentInverseAvg = x["JockeyRecentInverseAvg"].Single();
			instance.JockeyCurrentConditionAvg = x["JockeyCurrentConditionAvg"].Single();
			instance.TrainerRecentInverseAvg = x["TrainerRecentInverseAvg"].Single();
			instance.TrainerCurrentConditionAvg = x["TrainerCurrentConditionAvg"].Single();
			instance.BreederRecentInverseAvg = x["BreederRecentInverseAvg"].Single();
			instance.BreederCurrentConditionAvg = x["BreederCurrentConditionAvg"].Single();
			instance.SireRecentInverseAvg = x["SireRecentInverseAvg"].Single();
			instance.SireCurrentConditionAvg = x["SireCurrentConditionAvg"].Single();
			instance.DamSireRecentInverseAvg = x["DamSireRecentInverseAvg"].Single();
			instance.DamSireCurrentConditionAvg = x["DamSireCurrentConditionAvg"].Single();
			instance.SireDamSireRecentInverseAvg = x["SireDamSireRecentInverseAvg"].Single();
			instance.SireDamSireCurrentConditionAvg = x["SireDamSireCurrentConditionAvg"].Single();
			instance.JockeyTrainerRecentInverseAvg = x["JockeyTrainerRecentInverseAvg"].Single();
			instance.JockeyTrainerCurrentConditionAvg = x["JockeyTrainerCurrentConditionAvg"].Single();
			instance.TrainerNewHorseInverse = x["TrainerNewHorseInverse"].Single();
			instance.JockeyNewHorseInverse = x["JockeyNewHorseInverse"].Single();
			instance.SireNewHorseInverse = x["SireNewHorseInverse"].Single();
			instance.DamSireNewHorseInverse = x["DamSireNewHorseInverse"].Single();
			instance.BreederNewHorseInverse = x["BreederNewHorseInverse"].Single();
			instance.PurchasePriceRank = x["PurchasePriceRank"].Single();
			instance.RestDays = x["RestDays"].Single();
			instance.IsRentoFlag = x["IsRentoFlag"].Int32() > 0;
			instance.Age = x["Age"].Single();
			instance.Gender = x["Gender"].Single();
			instance.Season = x["Season"].Single();
			instance.RaceDistance = x["RaceDistance"].Single();
			instance.PerformanceTrend = x["PerformanceTrend"].Single();
			instance.DistanceChangeAdaptation = x["DistanceChangeAdaptation"].Single();
			instance.ClassChangeAdaptation = x["ClassChangeAdaptation"].Single();
			instance.JockeyWeightDiff = x["JockeyWeightDiff"].Single();
			instance.JockeyWeightDiffFromAvgInRace = x["JockeyWeightDiffFromAvgInRace"].Single();
			instance.AverageTuka = x["AverageTuka"].Single();
			instance.LastRaceTuka = x["LastRaceTuka"].Single();
			instance.TukaConsistency = x["TukaConsistency"].Single();
			instance.AverageTukaInRace = x["AverageTukaInRace"].Single();
			instance.LastRaceFinishPosition = x["LastRaceFinishPosition"].Single();
			instance.Recent3AvgFinishPosition = x["Recent3AvgFinishPosition"].Single();
			instance.FinishPositionImprovement = x["FinishPositionImprovement"].Single();
			instance.PaceAdvantageScore = x["PaceAdvantageScore"].Single();
			instance.CurrentGrade = x["CurrentGrade"].Single();
			instance.ClassUpChallenge = x["ClassUpChallenge"].Single();
			instance.CurrentTrackCondition = x["CurrentTrackCondition"].Single();
			instance.TrackConditionChangeFromLast = x["TrackConditionChangeFromLast"].Single();
			instance.SameCourseExperience = x["SameCourseExperience"].Single();
			instance.SameDistanceCategoryExperience = x["SameDistanceCategoryExperience"].Single();
			instance.SameTrackTypeExperience = x["SameTrackTypeExperience"].Single();
			instance.SameDistanceTimeIndex = x["SameDistanceTimeIndex"].Single();
			instance.LastRaceTimeDeviation = x["LastRaceTimeDeviation"].Single();
			instance.TimeConsistencyScore = x["TimeConsistencyScore"].Single();
			instance.AdjustedLastThreeFurlongsAvg = x["AdjustedLastThreeFurlongsAvg"].Single();
			instance.LastRaceAdjustedLastThreeFurlongs = x["LastRaceAdjustedLastThreeFurlongs"].Single();
			instance.AdjustedLastThreeFurlongsDiffFromAvgInRace = x["AdjustedLastThreeFurlongsDiffFromAvgInRace"].Single();
			instance.AverageTimeIndex = x["AverageTimeIndex"].Single();
			instance.LastRaceTimeIndex = x["LastRaceTimeIndex"].Single();
			instance.AverageTimeIndexRankInRace = x["AverageTimeIndexRankInRace"].Single();
			instance.Umaban = x["Umaban"].Int32();
			instance.IsNewHorse = x["IsNewHorse"].Int32() > 0;
			instance.AptitudeReliability = x["AptitudeReliability"].Single();
			instance.Label = (uint)x["Label"].Int32();
			instance.RaceId = x["RaceId"].Str();
			instance.Horse = x["Horse"].Str();

			return instance;
		}
	}
}