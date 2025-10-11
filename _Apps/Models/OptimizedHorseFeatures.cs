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
			nameof(UmabanAdvantage),
			// Season, RaceDistance: カテゴリ値のため正規化から除外
			nameof(JockeyDistanceAptitude),
			nameof(JockeyTrackConditionAptitude),
			nameof(JockeyPlaceAptitude),
			nameof(TrainerDistanceAptitude),
			nameof(TrainerTrackConditionAptitude),
			nameof(TrainerPlaceAptitude),
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
			nameof(SireDistanceAptitude),
			nameof(SireTrackConditionAptitude),
			nameof(SirePlaceAptitude),
			nameof(DamSireRecentInverseAvg),
			nameof(DamSireCurrentConditionAvg),
			nameof(DamSireDistanceAptitude),
			nameof(DamSireTrackConditionAptitude),
			nameof(DamSirePlaceAptitude),
			nameof(SireDamSireRecentInverseAvg),
			nameof(SireDamSireCurrentConditionAvg),
			// nameof(SireDamSireDistanceAptitude),  // 重要度0.2787 削除（案10）
			nameof(SireDamSireTrackConditionAptitude),
			nameof(SireDamSirePlaceAptitude),
			nameof(JockeyTrainerDistanceAptitude),
			nameof(JockeyTrainerTrackConditionAptitude),
			nameof(JockeyTrainerPlaceAptitude),
			nameof(GradeChange),
			nameof(TukaAdvantage),
			nameof(PaceStyleCompatibility),
			// 新規追加特徴量
			nameof(LastRaceScore_X_TimeRank),
			nameof(JockeyPlace_X_TrainerPlace),
			nameof(JockeyPlace_X_DistanceApt),
			nameof(LastRaceScore_X_JockeyPlace),
			nameof(Recent3Avg_X_JockeyRecent),
			nameof(JockeyRecentRankInRace),
			nameof(LastRaceScoreRankInRace),
			nameof(AgeRankInRace),
			nameof(RestDaysRankInRace),
			nameof(Recent3AvgRankInRace),
			nameof(RecentUpwardTrend),
			nameof(Recent1to2Improvement),
			nameof(Recent2to3Improvement),
			nameof(Recent1to2ImprovementAmount),
			nameof(Recent2to3ImprovementAmount),
			nameof(Interval1to2Days),
			nameof(Interval2to3Days),
			nameof(JockeyTrainerDistanceAptitude_Robust),
			nameof(JockeyTrainerTrackConditionAptitude_Robust),
			nameof(JockeyTrainerPlaceAptitude_Robust),
			// nameof(SeasonTargetEncoded), // 実装保留
			// nameof(CurrentGradeTargetEncoded), // 実装保留
			// nameof(CurrentTrackConditionTargetEncoded), // 実装保留
			nameof(OverallHorseQuality),
			nameof(OverallConnectionQuality),
		};

		public static string[] GetAdjustedPerformanceItemNames() => new[]
		{
			// Recent3AdjustedAvg, Recent5AdjustedAvg, LastRaceAdjustedScore は交互作用項/ランク特徴量で代替
			// nameof(Recent3AdjustedAvg),  // → Recent3AvgRankInRace で代替
			// nameof(Recent5AdjustedAvg),  // → 冗長性が高いため削除
			// nameof(LastRaceAdjustedScore), // → LastRaceScore_X_TimeRank, LastRaceScore_X_JockeyPlace で代替
			nameof(AdjustedConsistency),
		};

		// 馬の基本実績（難易度調整済み）
		[LoadColumn(0)] public float Recent3AdjustedAvg { get; set; }
		[LoadColumn(1)] public float Recent5AdjustedAvg { get; set; }
		[LoadColumn(2)] public float LastRaceAdjustedScore { get; set; }
		[LoadColumn(3)] public float AdjustedConsistency { get; set; }

		public static string[] GetCondition1ItemNames() => new[]
		{
			// nameof(CurrentDistanceAptitude),  // 重要度0.2478 削除（案9）
			nameof(CurrentTrackTypeAptitude),
			// nameof(CurrentTrackConditionAptitude),  // 重要度0.2414 削除（案8）
		};

		// 条件適性（今回のレース条件に対する適性）
		[LoadColumn(4)] public float CurrentDistanceAptitude { get; set; }
		[LoadColumn(5)] public float CurrentTrackTypeAptitude { get; set; }
		[LoadColumn(6)] public float CurrentTrackConditionAptitude { get; set; }

		public static string[] GetConnectionItemNames() => new[]
		{
			// nameof(JockeyRecentInverseAvg),  // → JockeyRecentRankInRace で代替
			nameof(JockeyCurrentConditionAvg),
			// nameof(JockeyDistanceAptitude),  // 重要度0.2792 削除（案10）
			nameof(JockeyTrackConditionAptitude),
			// nameof(JockeyPlaceAptitude),  // → JockeyPlace_X_TrainerPlace, JockeyPlace_X_DistanceApt で代替
			nameof(TrainerRecentInverseAvg),
			nameof(TrainerCurrentConditionAvg),
			// nameof(TrainerDistanceAptitude),  // 重要度0.2847 削除（案10）
			// nameof(TrainerTrackConditionAptitude),  // 重要度0.2932 削除（案12）
			// nameof(TrainerPlaceAptitude),  // 重要度0.2737 削除（案12）
			// nameof(BreederRecentInverseAvg),  // 重要度0.2510 削除（案9）
			nameof(BreederCurrentConditionAvg),
			nameof(SireRecentInverseAvg),
			// nameof(SireCurrentConditionAvg),  // 重要度0.2943 削除（案12）
			// nameof(SireDistanceAptitude),  // 重要度0.2606 削除（案9）
			// nameof(SireTrackConditionAptitude),  // 重要度0.2833 削除（案12）
			nameof(SirePlaceAptitude),
			// nameof(DamSireRecentInverseAvg),  // 重要度0.2892 削除（案12）
			nameof(DamSireCurrentConditionAvg),
			// nameof(DamSireDistanceAptitude),  // 重要度0.2591 削除（案9）
			// nameof(DamSireTrackConditionAptitude),  // 重要度0.2896 削除（案12）
			nameof(DamSirePlaceAptitude),
			nameof(SireDamSireRecentInverseAvg),
			nameof(SireDamSireCurrentConditionAvg),
			// nameof(SireDamSireDistanceAptitude),  // 重要度0.2787 削除（案10）
			nameof(SireDamSireTrackConditionAptitude),
			nameof(SireDamSirePlaceAptitude),
			// nameof(JockeyTrainerRecentInverseAvg),  // 重要度0.2988 削除（案12）
			nameof(JockeyTrainerCurrentConditionAvg),
			// nameof(JockeyTrainerDistanceAptitude),  // 重要度0.2421 削除（案8）
			// nameof(JockeyTrainerTrackConditionAptitude),  // 重要度0.2406 削除（案8）
			// nameof(JockeyTrainerPlaceAptitude),  // 重要度0.2455 削除（案9）
		};

		// 関係者実績（条件特化）
		[LoadColumn(7)] public float JockeyRecentInverseAvg { get; set; }
		[LoadColumn(8)] public float JockeyCurrentConditionAvg { get; set; }
		[LoadColumn(80)] public float JockeyDistanceAptitude { get; set; }
		[LoadColumn(81)] public float JockeyTrackConditionAptitude { get; set; }
		[LoadColumn(82)] public float JockeyPlaceAptitude { get; set; }

		[LoadColumn(9)] public float TrainerRecentInverseAvg { get; set; }
		[LoadColumn(10)] public float TrainerCurrentConditionAvg { get; set; }
		[LoadColumn(83)] public float TrainerDistanceAptitude { get; set; }
		[LoadColumn(84)] public float TrainerTrackConditionAptitude { get; set; }
		[LoadColumn(85)] public float TrainerPlaceAptitude { get; set; }

		[LoadColumn(11)] public float BreederRecentInverseAvg { get; set; }
		[LoadColumn(12)] public float BreederCurrentConditionAvg { get; set; }

		[LoadColumn(13)] public float SireRecentInverseAvg { get; set; }
		[LoadColumn(14)] public float SireCurrentConditionAvg { get; set; }
		[LoadColumn(70)] public float SireDistanceAptitude { get; set; }
		[LoadColumn(74)] public float SireTrackConditionAptitude { get; set; }
		[LoadColumn(77)] public float SirePlaceAptitude { get; set; }

		[LoadColumn(15)] public float DamSireRecentInverseAvg { get; set; }
		[LoadColumn(16)] public float DamSireCurrentConditionAvg { get; set; }
		[LoadColumn(71)] public float DamSireDistanceAptitude { get; set; }
		[LoadColumn(75)] public float DamSireTrackConditionAptitude { get; set; }
		[LoadColumn(78)] public float DamSirePlaceAptitude { get; set; }

		[LoadColumn(17)] public float SireDamSireRecentInverseAvg { get; set; }
		[LoadColumn(18)] public float SireDamSireCurrentConditionAvg { get; set; }
		[LoadColumn(73)] public float SireDamSireDistanceAptitude { get; set; }
		[LoadColumn(76)] public float SireDamSireTrackConditionAptitude { get; set; }
		[LoadColumn(79)] public float SireDamSirePlaceAptitude { get; set; }

		[LoadColumn(19)] public float JockeyTrainerRecentInverseAvg { get; set; }
		[LoadColumn(20)] public float JockeyTrainerCurrentConditionAvg { get; set; }
		[LoadColumn(86)] public float JockeyTrainerDistanceAptitude { get; set; }
		[LoadColumn(87)] public float JockeyTrainerTrackConditionAptitude { get; set; }
		[LoadColumn(88)] public float JockeyTrainerPlaceAptitude { get; set; }

		public static string[] GetNewHorseItemNames() => new[]
		{
			nameof(TrainerNewHorseInverse),
			// nameof(JockeyNewHorseInverse),  // 重要度0.2925 削除（案12）
			nameof(SireNewHorseInverse),
			nameof(DamSireNewHorseInverse),
			// nameof(BreederNewHorseInverse),  // 重要度0.2916 削除（案12）
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
			// nameof(IsRentoFlag),  // 重要度0.0149 削除
			nameof(Age),
			// nameof(Gender),  // 重要度0.0984 削除（案7）
			nameof(Season),
			nameof(RaceDistance),
			// nameof(PerformanceTrend),  // 重要度0.2391 削除（案8）
			// nameof(DistanceChangeAdaptation),  // 重要度0.1981 削除（案4）
			// nameof(ClassChangeAdaptation),  // 重要度0.2782 削除（案12）
			// nameof(JockeyWeightDiff),  // 重要度0.1582 削除（案4）
			nameof(JockeyWeightDiffFromAvgInRace),
			nameof(AverageTuka),
			// nameof(LastRaceTuka),  // 重要度0.2939 削除（案12）
			// nameof(TukaConsistency),  // 重要度0.2870 削除（案10）
			nameof(AverageTukaInRace),
			// nameof(TukaAdvantage),  // 重要度0.2949 削除（案12）
			// nameof(LastRaceFinishPosition),  // 重要度0.1931 削除（案4）
			nameof(Recent3AvgFinishPosition),
			// nameof(FinishPositionImprovement),  // 重要度0.1470 削除（案4）
			nameof(CurrentGrade),
			nameof(CurrentTrackCondition),
			// nameof(PaceAdvantageScore),  // 重要度0.1768 削除（案4）
			// nameof(PaceStyleCompatibility),  // 重要度0.1900 削除（案4）
			// nameof(ClassUpChallenge),  // 重要度0.0260 削除
			nameof(GradeChange),
			// nameof(TrackConditionChangeFromLast),  // 重要度0.1266 削除（案4）
			// nameof(SameCourseExperience),  // 重要度0.2431 削除（案8）
			// nameof(SameDistanceCategoryExperience),  // 重要度0.1855 削除（案4）
			// nameof(SameTrackTypeExperience),  // 重要度0.2960 削除（案12）
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
		[LoadColumn(69)] public float TukaAdvantage { get; set; }
		[LoadColumn(42)] public float LastRaceFinishPosition { get; set; }
		[LoadColumn(43)] public float Recent3AvgFinishPosition { get; set; }
		[LoadColumn(44)] public float FinishPositionImprovement { get; set; }
		[LoadColumn(45)] public float PaceAdvantageScore { get; set; }
		[LoadColumn(72)] public float PaceStyleCompatibility { get; set; }
		[LoadColumn(46)] public float CurrentGrade { get; set; }
		[LoadColumn(47)] public float ClassUpChallenge { get; set; }
		[LoadColumn(68)] public float GradeChange { get; set; }
		[LoadColumn(48)] public float CurrentTrackCondition { get; set; }
		[LoadColumn(49)] public float TrackConditionChangeFromLast { get; set; }
		[LoadColumn(50)] public float SameCourseExperience { get; set; }
		[LoadColumn(51)] public float SameDistanceCategoryExperience { get; set; }
		[LoadColumn(52)] public float SameTrackTypeExperience { get; set; }

		public static string[] GetTimeItemNames() => new[]
		{
			// nameof(SameDistanceTimeIndex),  // 重要度0.2816 削除（案10）
			nameof(LastRaceTimeDeviation),
			// nameof(TimeConsistencyScore),  // 重要度0.2858 削除（案10）
			// nameof(AdjustedLastThreeFurlongsAvg),  // 重要度0.2725 削除（案10）
			nameof(LastRaceAdjustedLastThreeFurlongs),
			nameof(AdjustedLastThreeFurlongsDiffFromAvgInRace),
			// nameof(AverageTimeIndex),  // 重要度0.2830 削除（案10）
			// nameof(LastRaceTimeIndex),  // 重要度0.2523 削除（案9）
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

		public static string[] GetRacePositionItemNames() => new string[]
		{
			// nameof(Umaban),  // 重要度0.1866 削除（案4）
			// nameof(UmabanAdvantage),  // 重要度0.2987 削除（案12）
		};

		[LoadColumn(62)] public int Umaban { get; set; }
		[LoadColumn(67)] public float UmabanAdvantage { get; set; }

		public static string[] GetMetadataNames() => new string[]
		{
			// nameof(IsNewHorse),  // 重要度0.0558 削除
			// nameof(AptitudeReliability),  // 重要度0.0783 削除（案8）
		};

		// メタ情報
		[LoadColumn(63)] public bool IsNewHorse { get; set; }
		[LoadColumn(64)] public float AptitudeReliability { get; set; }

		// === 新規追加特徴量 ===

		public static string[] GetInteractionItemNames() => new[]
		{
			nameof(LastRaceScore_X_TimeRank),
			nameof(JockeyPlace_X_TrainerPlace),
			nameof(JockeyPlace_X_DistanceApt),
			nameof(LastRaceScore_X_JockeyPlace),
			nameof(Recent3Avg_X_JockeyRecent),
		};

		// 交互作用項
		[LoadColumn(89)] public float LastRaceScore_X_TimeRank { get; set; }
		[LoadColumn(90)] public float JockeyPlace_X_TrainerPlace { get; set; }
		[LoadColumn(91)] public float JockeyPlace_X_DistanceApt { get; set; }
		[LoadColumn(92)] public float LastRaceScore_X_JockeyPlace { get; set; }
		[LoadColumn(93)] public float Recent3Avg_X_JockeyRecent { get; set; }

		public static string[] GetRankItemNames() => new[]
		{
			nameof(JockeyRecentRankInRace),
			nameof(LastRaceScoreRankInRace),
			nameof(AgeRankInRace),
			// nameof(RestDaysRankInRace),  // 重要度0.2499 削除（案9）
			nameof(Recent3AvgRankInRace),
		};

		// レース内ランク特徴量
		[LoadColumn(94)] public float JockeyRecentRankInRace { get; set; }
		[LoadColumn(95)] public float LastRaceScoreRankInRace { get; set; }
		[LoadColumn(96)] public float AgeRankInRace { get; set; }
		[LoadColumn(97)] public float RestDaysRankInRace { get; set; }
		[LoadColumn(98)] public float Recent3AvgRankInRace { get; set; }

		public static string[] GetTrendItemNames() => new string[]
		{
			// バイナリトレンド特徴量は重要度0.0のため削除（連続値版のみ使用）
			// nameof(RecentUpwardTrend),
			// nameof(Recent1to2Improvement),
			// nameof(Recent2to3Improvement),
			// nameof(Recent1to2ImprovementAmount),  // 重要度0.2969 削除（案12）
			// nameof(Recent2to3ImprovementAmount),  // 重要度0.2439 削除（案8）
			// nameof(Interval1to2Days),  // 重要度0.2773 削除（案10）
			// nameof(Interval2to3Days),  // 重要度0.2405 削除（案8）
		};

		// トレンド特徴量
		[LoadColumn(99)] public float RecentUpwardTrend { get; set; } // 3走連続改善
		[LoadColumn(103)] public float Recent1to2Improvement { get; set; } // 前走→前々走の改善フラグ
		[LoadColumn(104)] public float Recent2to3Improvement { get; set; } // 前々走→前前々走の改善フラグ
		[LoadColumn(105)] public float Recent1to2ImprovementAmount { get; set; } // 前走→前々走の改善量
		[LoadColumn(106)] public float Recent2to3ImprovementAmount { get; set; } // 前々走→前前々走の改善量
		[LoadColumn(107)] public float Interval1to2Days { get; set; } // 前走→前々走の間隔（日数）
		[LoadColumn(108)] public float Interval2to3Days { get; set; } // 前々走→前前々走の間隔（日数）

		public static string[] GetRobustConnectionItemNames() => new[]
		{
			nameof(JockeyTrainerDistanceAptitude_Robust),
			nameof(JockeyTrainerTrackConditionAptitude_Robust),
			// nameof(JockeyTrainerPlaceAptitude_Robust),  // 重要度0.2584 削除（案9）
		};

		// 騎手×調教師強化（信頼度重み付け）
		[LoadColumn(100)] public float JockeyTrainerDistanceAptitude_Robust { get; set; }
		[LoadColumn(101)] public float JockeyTrainerTrackConditionAptitude_Robust { get; set; }
		[LoadColumn(102)] public float JockeyTrainerPlaceAptitude_Robust { get; set; }

		// ターゲットエンコーディング（実装が複雑なため保留）
		// public static string[] GetTargetEncodingItemNames() => new[]
		// {
		// 	nameof(SeasonTargetEncoded),
		// 	nameof(CurrentGradeTargetEncoded),
		// 	nameof(CurrentTrackConditionTargetEncoded),
		// };
		// [LoadColumn(103)] public float SeasonTargetEncoded { get; set; }
		// [LoadColumn(104)] public float CurrentGradeTargetEncoded { get; set; }
		// [LoadColumn(105)] public float CurrentTrackConditionTargetEncoded { get; set; }

		public static string[] GetEnsembleItemNames() => new[]
		{
			// nameof(OverallHorseQuality),  // 重要度0.2462 削除（案8）
			nameof(OverallConnectionQuality),
		};

		// アンサンブル特徴量
		[LoadColumn(106)] public float OverallHorseQuality { get; set; }
		[LoadColumn(107)] public float OverallConnectionQuality { get; set; }

		public static string[] GetOikiriItemNames() => new[]
		{
			// 優先度S: 調教特徴量（最終追い切りデータ）
			nameof(OikiriLap5Time),  // 最終ラップタイム（重要度0.7526）
			nameof(OikiriLap3Time),  // 3Fタイム（重要度0.8742、19位）
			// nameof(OikiriEvaluationScore),  // 重要度0.3513 削除（案16: 主観的評価は効果薄い）
			// nameof(TokeiColorTotalCount),  // 重要度0.3708 削除（案16: ノイズ除去）
			nameof(OikiriQualityScore),  // 総合調教質スコア（重要度0.7770）
			// 優先度A: InRace調教タイム特徴量
			nameof(OikiriLap3TimeRankInRace),  // レース内3Fタイム順位
			nameof(OikiriLap5TimeRankInRace),  // レース内最終ラップ順位
			nameof(OikiriSpeedScore),  // 速さ×持続力（Lap3×Lap5の複合）
		};

		public static string[] GetWeightItemNames() => new string[]
		{
			// 優先度S: 馬体重特徴量（仕上がり評価）
			// nameof(OptimalWeightDiffScore),  // 重要度0.2314 削除（案18: 線形スコアがノイズ化、最低重要度）
			// nameof(WeightDiffRankInRace),  // 重要度0.6907 削除（案19: 52位/56、効果薄い）
			// nameof(WeightDiff_X_OikiriQualityScore),  // 重要度0.7923 削除（案19: スケール問題で他特徴量圧迫）
		};

		public static string[] GetAdvancedItemNames() => new[]
		{
			// nameof(TopFeaturesEnsemble),  // 重要度0.2992 削除（案11: 他特徴量の単純加重和のため冗長）
			nameof(SpeedPowerScore),
			nameof(ConnectionReliabilityScore),
			// nameof(ConditionPowerScore),  // 案13: 重要度0.7056だがRecent3AvgRankInRaceを消費するため削除
			// nameof(JockeyHorseConditionScore),  // 案13: 重要度0.9022だがRecent3AvgRankInRaceを消費するため削除
			// nameof(SpeedAgeScore),  // 案14: 重要度0.2923で低いため削除（案14改）
			nameof(BloodlineTrackScore),  // 案14: 血統信頼度×コース適性
		};

		// 案6: 高度な派生特徴量
		[LoadColumn(109)] public float TopFeaturesEnsemble { get; set; }  // トップ6特徴量の加重平均
		[LoadColumn(110)] public float SpeedPowerScore { get; set; }  // 速さ×力強さ
		[LoadColumn(111)] public float ConnectionReliabilityScore { get; set; }  // 関係者信頼度

		// 案13: InRace交互作用項（第2弾）- 削除
		[LoadColumn(112)] public float ConditionPowerScore { get; set; }  // 最近の調子 × 前走成績
		[LoadColumn(113)] public float JockeyHorseConditionScore { get; set; }  // 騎手の調子 × 馬の調子

		// 案14: 新交互作用項（基本特徴量を消費しない）
		[LoadColumn(114)] public float SpeedAgeScore { get; set; }  // 速さ×年齢
		[LoadColumn(115)] public float BloodlineTrackScore { get; set; }  // 血統信頼度×コース適性

		// 優先度S: 調教特徴量（最終追い切りデータ）
		[LoadColumn(116)] public float OikiriLap5Time { get; set; }  // 最終ラップタイム（欠損=0）
		[LoadColumn(117)] public float OikiriLap3Time { get; set; }  // 3Fタイム（欠損=0）
		[LoadColumn(118)] public float OikiriEvaluationScore { get; set; }  // 評価スコア（削除予定）
		[LoadColumn(119)] public float TokeiColorTotalCount { get; set; }  // TokeiColor総数（削除予定）
		[LoadColumn(120)] public float OikiriQualityScore { get; set; }  // 総合調教質スコア

		// 優先度A: InRace調教タイム特徴量
		[LoadColumn(121)] public float OikiriLap3TimeRankInRace { get; set; }  // レース内3Fタイム順位
		[LoadColumn(122)] public float OikiriLap5TimeRankInRace { get; set; }  // レース内最終ラップ順位
		[LoadColumn(123)] public float OikiriSpeedScore { get; set; }  // 速さ×持続力

		// 優先度S: 馬体重特徴量（仕上がり評価）
		// [LoadColumn(124)] public float OptimalWeightDiffScore { get; set; }  // 重要度0.2314 削除（案18）
		// [LoadColumn(124)] public float WeightDiffRankInRace { get; set; }  // 重要度0.6907 削除（案19）
		// [LoadColumn(125)] public float WeightDiff_X_OikiriQualityScore { get; set; }  // 重要度0.7923 削除（案19）

		// ラベル・グループ情報
		[LoadColumn(65)] public uint Label { get; set; }
		[LoadColumn(66)] public string RaceId { get; set; }

		public static string[] GetFlagItemNames() => new[]
		{
			nameof(IsNewHorse),
			nameof(IsRentoFlag),
			nameof(Umaban),
			nameof(UmabanAdvantage),
		};

		public static string[] GetCategoryNames() => new[]
		{
			// Season, RaceDistance, CurrentGrade, CurrentTrackCondition は重要度0.0のため除外
			nameof(Gender),
		};

		public static string[] GetAllFeaturesNames() => GetAdjustedPerformanceItemNames()
			.Concat(GetCondition1ItemNames())
			.Concat(GetConnectionItemNames())
			.Concat(GetNewHorseItemNames())
			.Concat(GetStatusItemNames())
			.Concat(GetTimeItemNames())
			.Concat(GetRacePositionItemNames())
			.Concat(GetMetadataNames())
			// 新規追加特徴量
			.Concat(GetInteractionItemNames())
			.Concat(GetRankItemNames())
			.Concat(GetTrendItemNames())
			.Concat(GetRobustConnectionItemNames())
			// .Concat(GetTargetEncodingItemNames()) // 実装保留
			.Concat(GetEnsembleItemNames())
			.Concat(GetAdvancedItemNames())  // 案6: 高度な派生特徴量
			.Concat(GetOikiriItemNames())  // 調教特徴量
			.Concat(GetWeightItemNames())  // 馬体重特徴量
			// 重要度0.0の特徴量を除外
			.Where(name => name != nameof(Season)
				&& name != nameof(RaceDistance)
				&& name != nameof(AverageTukaInRace)
				&& name != nameof(CurrentGrade)
				&& name != nameof(CurrentTrackCondition))
			.ToArray();

		public static string[] GetAllFeaturesNamesOneHot() => GetAllFeaturesNames()
			.Select(name => GetCategoryNames().Contains(name) ? $"{name}OneHot" : name)
			.ToArray();
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
			instance.JockeyDistanceAptitude = x["JockeyDistanceAptitude"].Single();
			instance.JockeyTrackConditionAptitude = x["JockeyTrackConditionAptitude"].Single();
			instance.JockeyPlaceAptitude = x["JockeyPlaceAptitude"].Single();
			instance.TrainerRecentInverseAvg = x["TrainerRecentInverseAvg"].Single();
			instance.TrainerCurrentConditionAvg = x["TrainerCurrentConditionAvg"].Single();
			instance.TrainerDistanceAptitude = x["TrainerDistanceAptitude"].Single();
			instance.TrainerTrackConditionAptitude = x["TrainerTrackConditionAptitude"].Single();
			instance.TrainerPlaceAptitude = x["TrainerPlaceAptitude"].Single();
			instance.BreederRecentInverseAvg = x["BreederRecentInverseAvg"].Single();
			instance.BreederCurrentConditionAvg = x["BreederCurrentConditionAvg"].Single();
			instance.SireRecentInverseAvg = x["SireRecentInverseAvg"].Single();
			instance.SireCurrentConditionAvg = x["SireCurrentConditionAvg"].Single();
			instance.SireDistanceAptitude = x["SireDistanceAptitude"].Single();
			instance.SireTrackConditionAptitude = x["SireTrackConditionAptitude"].Single();
			instance.SirePlaceAptitude = x["SirePlaceAptitude"].Single();
			instance.DamSireRecentInverseAvg = x["DamSireRecentInverseAvg"].Single();
			instance.DamSireCurrentConditionAvg = x["DamSireCurrentConditionAvg"].Single();
			instance.DamSireDistanceAptitude = x["DamSireDistanceAptitude"].Single();
			instance.DamSireTrackConditionAptitude = x["DamSireTrackConditionAptitude"].Single();
			instance.DamSirePlaceAptitude = x["DamSirePlaceAptitude"].Single();
			instance.SireDamSireRecentInverseAvg = x["SireDamSireRecentInverseAvg"].Single();
			instance.SireDamSireCurrentConditionAvg = x["SireDamSireCurrentConditionAvg"].Single();
			instance.SireDamSireDistanceAptitude = x["SireDamSireDistanceAptitude"].Single();
			instance.SireDamSireTrackConditionAptitude = x["SireDamSireTrackConditionAptitude"].Single();
			instance.SireDamSirePlaceAptitude = x["SireDamSirePlaceAptitude"].Single();
			instance.JockeyTrainerRecentInverseAvg = x["JockeyTrainerRecentInverseAvg"].Single();
			instance.JockeyTrainerCurrentConditionAvg = x["JockeyTrainerCurrentConditionAvg"].Single();
			instance.JockeyTrainerDistanceAptitude = x["JockeyTrainerDistanceAptitude"].Single();
			instance.JockeyTrainerTrackConditionAptitude = x["JockeyTrainerTrackConditionAptitude"].Single();
			instance.JockeyTrainerPlaceAptitude = x["JockeyTrainerPlaceAptitude"].Single();
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
			instance.TukaAdvantage = x["TukaAdvantage"].Single();
			instance.LastRaceFinishPosition = x["LastRaceFinishPosition"].Single();
			instance.Recent3AvgFinishPosition = x["Recent3AvgFinishPosition"].Single();
			instance.FinishPositionImprovement = x["FinishPositionImprovement"].Single();
			instance.PaceAdvantageScore = x["PaceAdvantageScore"].Single();
			instance.PaceStyleCompatibility = x["PaceStyleCompatibility"].Single();
			instance.CurrentGrade = x["CurrentGrade"].Single();
			instance.ClassUpChallenge = x["ClassUpChallenge"].Single();
			instance.GradeChange = x["GradeChange"].Single();
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
			instance.UmabanAdvantage = x["UmabanAdvantage"].Single();
			instance.IsNewHorse = x["IsNewHorse"].Int32() > 0;
			instance.AptitudeReliability = x["AptitudeReliability"].Single();
			instance.Label = (uint)x["Label"].Int32();
			instance.RaceId = x["RaceId"].Str();
			instance.Horse = x["Horse"].Str();

			// 新規追加特徴量
			instance.LastRaceScore_X_TimeRank = x["LastRaceScore_X_TimeRank"].Single();
			instance.JockeyPlace_X_TrainerPlace = x["JockeyPlace_X_TrainerPlace"].Single();
			instance.JockeyPlace_X_DistanceApt = x["JockeyPlace_X_DistanceApt"].Single();
			instance.LastRaceScore_X_JockeyPlace = x["LastRaceScore_X_JockeyPlace"].Single();
			instance.Recent3Avg_X_JockeyRecent = x["Recent3Avg_X_JockeyRecent"].Single();
			instance.JockeyRecentRankInRace = x["JockeyRecentRankInRace"].Single();
			instance.LastRaceScoreRankInRace = x["LastRaceScoreRankInRace"].Single();
			instance.AgeRankInRace = x["AgeRankInRace"].Single();
			instance.RestDaysRankInRace = x["RestDaysRankInRace"].Single();
			instance.Recent3AvgRankInRace = x["Recent3AvgRankInRace"].Single();
			instance.RecentUpwardTrend = x["RecentUpwardTrend"].Single();
			instance.Recent1to2Improvement = x["Recent1to2Improvement"].Single();
			instance.Recent2to3Improvement = x["Recent2to3Improvement"].Single();
			instance.Recent1to2ImprovementAmount = x["Recent1to2ImprovementAmount"].Single();
			instance.Recent2to3ImprovementAmount = x["Recent2to3ImprovementAmount"].Single();
			instance.Interval1to2Days = x["Interval1to2Days"].Single();
			instance.Interval2to3Days = x["Interval2to3Days"].Single();
			instance.JockeyTrainerDistanceAptitude_Robust = x["JockeyTrainerDistanceAptitude_Robust"].Single();
			instance.JockeyTrainerTrackConditionAptitude_Robust = x["JockeyTrainerTrackConditionAptitude_Robust"].Single();
			instance.JockeyTrainerPlaceAptitude_Robust = x["JockeyTrainerPlaceAptitude_Robust"].Single();
			instance.OverallHorseQuality = x["OverallHorseQuality"].Single();
			instance.OverallConnectionQuality = x["OverallConnectionQuality"].Single();

			// 案6: 高度な派生特徴量
			instance.TopFeaturesEnsemble = x["TopFeaturesEnsemble"].Single();
			instance.SpeedPowerScore = x["SpeedPowerScore"].Single();
			instance.ConnectionReliabilityScore = x["ConnectionReliabilityScore"].Single();

			// 案13: InRace交互作用項（第2弾）- 削除
			instance.ConditionPowerScore = x["ConditionPowerScore"].Single();
			instance.JockeyHorseConditionScore = x["JockeyHorseConditionScore"].Single();

			// 案14: 新交互作用項（基本特徴量を消費しない）
			instance.SpeedAgeScore = x["SpeedAgeScore"].Single();
			instance.BloodlineTrackScore = x["BloodlineTrackScore"].Single();

			// 調教特徴量
			instance.OikiriLap5Time = x["OikiriLap5Time"].Single();
			instance.OikiriLap3Time = x["OikiriLap3Time"].Single();
			instance.OikiriEvaluationScore = x["OikiriEvaluationScore"].Single();
			instance.TokeiColorTotalCount = x["TokeiColorTotalCount"].Single();
			instance.OikiriQualityScore = x["OikiriQualityScore"].Single();
			instance.OikiriLap3TimeRankInRace = x["OikiriLap3TimeRankInRace"].Single();
			instance.OikiriLap5TimeRankInRace = x["OikiriLap5TimeRankInRace"].Single();
			instance.OikiriSpeedScore = x["OikiriSpeedScore"].Single();

			// 馬体重特徴量
			// instance.OptimalWeightDiffScore = x["OptimalWeightDiffScore"].Single();  // 削除（案18）
			// instance.WeightDiffRankInRace = x["WeightDiffRankInRace"].Single();  // 削除（案19）
			// instance.WeightDiff_X_OikiriQualityScore = x["WeightDiff_X_OikiriQualityScore"].Single();  // 削除（案19）

			return instance;
		}
	}
}
