using Codeplex.Data;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using TBird.Core;

namespace Netkeiba.Models
{
	public partial class OptimizedHorseFeatures
	{
		public OptimizedHorseFeatures()
		{
			RaceId = string.Empty;
			Horse = string.Empty;
		}

		// ｷｰ情報
		public uint Label { get; set; }
		public string RaceId { get; set; }
		public string Horse { get; set; }

		// 当日の情報
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float RestDays { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float RaceCount { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float RaceCountRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float Age { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AgeRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Gender { get; set; }

		// 過去ﾚｰｽの休養日数（個別）
		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther, Normalization = true)]
		public float RestDaysN1 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float RestDaysN1Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther, Normalization = true)]
		public float RestDaysN2 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float RestDaysN2Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther, Normalization = true)]
		public float RestDaysN3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float RestDaysN3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float RestDaysN4 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float RestDaysN4Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float RestDaysN5 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float RestDaysN5Rank { get; set; }

		// 過去ﾚｰｽの経過日数（個別）
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float DaysAgoN1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float DaysAgoN1Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float DaysAgoN2 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float DaysAgoN2Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther, Normalization = true)]
		public float DaysAgoN3 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float DaysAgoN3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float DaysAgoN4 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float DaysAgoN4Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther, Normalization = true)]
		public float DaysAgoN5 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float DaysAgoN5Rank { get; set; }

		// 同ﾚｰｽ他馬との比較
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float PurchasePrice { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float PurchasePriceRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float JockeyWeight { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float JockeyWeightRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float Weight { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float WeightRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float WeightDiff { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float WeightDiffRank { get; set; }

		// 実績の集計 (直近5戦)
		// 獲得賞金
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxPrizeMoneyRank { get; set; }

		// ﾚｰﾃｨﾝｸﾞ
		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther, Normalization = true)]
		public float AvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther, Normalization = true)]
		public float MaxRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxRatingRank { get; set; }

		// 距離
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float DistanceDiff { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float DistanceDiffRank { get; set; }

		// 通過順
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float Tuka { get; set; }

		// ﾍﾟｰｽ予想
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float PaceAdvantage { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float PaceAdvantageRank { get; set; }

		// 追切情報（raw値はRank計算用に保持、特徴量としてはRankのみ使用）
		public float OikiriAdjustedTime5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float OikiriAdjustedTime5Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float OikiriRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float OikiriRatingRank { get; set; }

		public float OikiriAdaptation { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float OikiriAdaptationRank { get; set; }

		public float OikiriTimeRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float OikiriTimeRatingRank { get; set; }

		public float OikiriTotalScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float OikiriTotalScoreRank { get; set; }

		// 着順
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgFinishPosition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgFinishPosition3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxFinishPosition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxFinishPosition3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgFinishPosition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgFinishPosition5Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxFinishPosition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxFinishPosition5Rank { get; set; }

		// 着順（個別）
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float FinishPositionN1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float FinishPositionN1Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float FinishPositionN2 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float FinishPositionN2Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float FinishPositionN3 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float FinishPositionN3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float FinishPositionN4 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float FinishPositionN4Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float FinishPositionN5 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float FinishPositionN5Rank { get; set; }

		// 着順トレンド（N1-N2、正=改善）
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float FinishPositionTrend { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float FinishPositionTrendRank { get; set; }

		// ﾄｯﾌﾟとのﾀｲﾑ差
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Top3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxTime2Top3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Top5Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float MaxTime2Top5Rank { get; set; }

		// ﾄｯﾌﾟとのﾀｲﾑ差（個別）
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float Time2TopN1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float Time2TopN1Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2TopN2 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float Time2TopN2Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2TopN3 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float Time2TopN3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2TopN4 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float Time2TopN4Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2TopN5 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float Time2TopN5Rank { get; set; }

		// 同条件ﾚｰｽとのﾀｲﾑ差
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Condition1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Condition1Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Condition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float AvgTime2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Condition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxTime2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Condition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Condition5Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Condition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float MaxTime2Condition5Rank { get; set; }

		// 同条件ﾚｰｽとのﾀｲﾑ差（個別）
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2ConditionN1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float Time2ConditionN1Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float Time2ConditionN2 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float Time2ConditionN2Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2ConditionN3 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float Time2ConditionN3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2ConditionN4 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float Time2ConditionN4Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float Time2ConditionN5 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float Time2ConditionN5Rank { get; set; }

		// ﾄｯﾌﾟとの3ﾊﾛﾝ差
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Top1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top1Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float MaxLastThreeFurlongs2Top3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float AvgLastThreeFurlongs2Top5Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Top5Rank { get; set; }

		// 同条件ﾚｰｽとの3ﾊﾛﾝ差
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Condition1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float AvgLastThreeFurlongs2Condition1Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Condition3 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float AvgLastThreeFurlongs2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Condition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Condition5 { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float AvgLastThreeFurlongs2Condition5Rank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Condition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.HorseOther)]
		public float MaxLastThreeFurlongs2Condition5Rank { get; set; }

		// 父馬の兄弟馬の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireBrosAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireBrosAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireBrosDistanceDiff { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosDistanceDiffRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireBrosAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireBrosAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgTime2ConditionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireBrosAvgLastThreeFurlongs2Top { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgLastThreeFurlongs2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood, Normalization = true)]
		public float SireBrosAvgLastThreeFurlongs2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgLastThreeFurlongs2ConditionRank { get; set; }

		// 父馬-母父馬の兄弟馬の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireBrosAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireBrosAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireBrosDistanceDiff { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireBrosDistanceDiffRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireBrosAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireBrosAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgTime2ConditionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireBrosAvgLastThreeFurlongs2Top { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgLastThreeFurlongs2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireBrosAvgLastThreeFurlongs2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgLastThreeFurlongs2ConditionRank { get; set; }

		// 父馬-馬場の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireTrackAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireTrackAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireTrackAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireTrackAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireTrackAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireTrackAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood, Normalization = true)]
		public float SireTrackAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireTrackAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireTrackAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireTrackAvgTime2ConditionRank { get; set; }

		// 父馬-距離の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDistanceAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDistanceAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDistanceAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDistanceAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDistanceAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDistanceAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDistanceAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDistanceAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDistanceAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDistanceAvgTime2ConditionRank { get; set; }

		// 父馬-母父馬-馬場の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireTrackAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireTrackAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireTrackAvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireTrackAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireTrackAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireTrackAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireTrackAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireTrackAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireTrackAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireTrackAvgTime2ConditionRank { get; set; }

		// 父馬-母父馬-距離の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireDistanceAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireDistanceAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireDistanceAvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireDistanceAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireDistanceAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireDistanceAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireDistanceAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireDistanceAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood, Normalization = true)]
		public float SireDamSireDistanceAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood)]
		public float SireDamSireDistanceAvgTime2ConditionRank { get; set; }

		// 騎手の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyAvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgTime2ConditionRank { get; set; }

		// 騎手-場所相性の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyPlaceAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyPlaceAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyPlaceAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyPlaceAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyPlaceAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyPlaceAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyPlaceAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyPlaceAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyPlaceAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyPlaceAvgTime2ConditionRank { get; set; }

		// 騎手-馬場相性の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrackAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrackAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrackAvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrackAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrackAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrackAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrackAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrackAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrackAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrackAvgTime2ConditionRank { get; set; }

		// 騎手-距離の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyDistanceAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyDistanceAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyDistanceAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyDistanceAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyDistanceAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyDistanceAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyDistanceAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyDistanceAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyDistanceAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyDistanceAvgTime2ConditionRank { get; set; }

		// 騎手-調教師相性の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrainerAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrainerAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrainerAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrainerAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrainerAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrainerAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrainerAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrainerAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyTrainerAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrainerAvgTime2ConditionRank { get; set; }

		// 調教師の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerAvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection, Normalization = true)]
		public float TrainerAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerAvgTime2ConditionRank { get; set; }

		// 調教師-場所の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerPlaceAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerPlaceAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerPlaceAvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerPlaceAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerPlaceAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerPlaceAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection, Normalization = true)]
		public float TrainerPlaceAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerPlaceAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerPlaceAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerPlaceAvgTime2ConditionRank { get; set; }

		// 生産者の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float BreederAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float BreederAvgRating { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float BreederAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float BreederAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgTime2ConditionRank { get; set; }

		// 調教師-生産者相性の情報
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerBreederAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerBreederAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerBreederAvgRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerBreederAvgRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerBreederAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerBreederAvgFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerBreederAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerBreederAvgTime2TopRank { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection, Normalization = true)]
		public float TrainerBreederAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection)]
		public float TrainerBreederAvgTime2ConditionRank { get; set; }

		// ========== Z-score特徴量（場内平均からの標準偏差数） ==========

		// A. パフォーマンス実績 Z-score
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgPrizeMoneyZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float MaxPrizeMoneyZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgRatingZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxRatingZScore { get; set; }

		// B. 着順指標（集計）Z-score
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgFinishPosition3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxFinishPosition3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgFinishPosition5ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float MaxFinishPosition5ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float FinishPositionTrendZScore { get; set; }

		// C. タイム差指標（集計）Z-score
		// Time2Top
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgTime2Top3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxTime2Top3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float AvgTime2Top5ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxTime2Top5ZScore { get; set; }

		// Time2Condition
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgTime2Condition1ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgTime2Condition3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxTime2Condition3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgTime2Condition5ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxTime2Condition5ZScore { get; set; }

		// LTF2Top
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top1ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Top3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top5ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Top5ZScore { get; set; }

		// LTF2Condition
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Condition1ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Condition3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Condition3ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Condition5ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Condition5ZScore { get; set; }

		// D. 追切情報 Z-score（Other: Rankと高相関のため除外）
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float OikiriAdjustedTime5ZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float OikiriRatingZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float OikiriAdaptationZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float OikiriTimeRatingZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float OikiriTotalScoreZScore { get; set; }

		// E. 基本指標 Z-score
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float PurchasePriceZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse)]
		public float WeightZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.HorseOther)]
		public float DistanceDiffZScore { get; set; }

		// F. 血統情報 Z-score
		// 父馬の兄弟馬
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgPrizeMoneyZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgRatingZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosDistanceDiffZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgFinishPositionZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgTime2TopZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgTime2ConditionZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgLastThreeFurlongs2TopZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireBrosAvgLastThreeFurlongs2ConditionZScore { get; set; }

		// 父馬-母父馬の兄弟馬
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgPrizeMoneyZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgRatingZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosDistanceDiffZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgFinishPositionZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgTime2TopZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgTime2ConditionZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgLastThreeFurlongs2TopZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireBrosAvgLastThreeFurlongs2ConditionZScore { get; set; }

		// 父馬-馬場 Z-score
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireTrackAvgPrizeMoneyZScore { get; set; }

		// 父馬-距離 Z-score
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDistanceAvgPrizeMoneyZScore { get; set; }

		// 父馬-母父馬-馬場 Z-score
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireTrackAvgPrizeMoneyZScore { get; set; }

		// 父馬-母父馬-距離 Z-score
		[Features(Type = FeaturesType.AllOther | FeaturesType.Blood)]
		public float SireDamSireDistanceAvgPrizeMoneyZScore { get; set; }

		// G. コネクション Z-score
		// 騎手（AvgPrizeMoney + AvgRating）
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgPrizeMoneyZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgRatingZScore { get; set; }

		// 調教師（AvgPrizeMoney + AvgRating）
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerAvgPrizeMoneyZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerAvgRatingZScore { get; set; }

		// 調教師-場所
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerPlaceAvgPrizeMoneyZScore { get; set; }

		// 生産者（AvgPrizeMoney + AvgRating）
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgPrizeMoneyZScore { get; set; }

		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float BreederAvgRatingZScore { get; set; }

		// 騎手-場所（Other: Rankと高相関のため除外）
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyPlaceAvgPrizeMoneyZScore { get; set; }

		// 騎手-馬場
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrackAvgPrizeMoneyZScore { get; set; }

		// 騎手-距離
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyDistanceAvgPrizeMoneyZScore { get; set; }

		// 騎手-調教師
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyTrainerAvgPrizeMoneyZScore { get; set; }

		// 調教師-生産者
		[Features(Type = FeaturesType.AllOther | FeaturesType.Connection)]
		public float TrainerBreederAvgPrizeMoneyZScore { get; set; }

		// ========== 交差特徴量（異なる情報源の乗算的交互作用） ==========
		// 追切 × 着順実績
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float CrossOikiriFinishPos { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float CrossOikiriFinishPosRank { get; set; }

		// 追切 × タイム差
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float CrossOikiriTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float CrossOikiriTime2TopRank { get; set; }

		// 距離適性 × 着順実績
		[Features(Type = FeaturesType.AllOther | FeaturesType.Horse, Normalization = true)]
		public float CrossDistanceFitFinishPos { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float CrossDistanceFitFinishPosRank { get; set; }

		// 休養 × 着順実績
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float CrossRestDaysFinishPos { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float CrossRestDaysFinishPosRank { get; set; }

		// レーティング × 追切
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float CrossRatingOikiri { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float CrossRatingOikiriRank { get; set; }

		// 追切 × 騎手実績
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float CrossOikiriJockey { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float CrossOikiriJockeyRank { get; set; }

	}

	public partial class OptimizedHorseFeatures
	{
		private static CustomProperty[]? _databaseProperties;

		private static CustomProperty[] GetDatabaseProperties() => _databaseProperties = _databaseProperties ?? GetProperties()
			.Where(x => !new[] { "Label", "RaceId", "Horse" }.Contains(x.Name))
			.ToArray();

		public static OptimizedHorseFeatures? Deserialize(Dictionary<string, object> dic)
		{
			var instance = new OptimizedHorseFeatures()
			{
				Label = dic["Label"].GetUInt32(),
				RaceId = dic["RaceId"].Str(),
				Horse = dic["Horse"].Str()
			};

			byte[] bytes = (byte[])dic["Features"];
			float[] floats = new float[bytes.Length / sizeof(float)];
			Buffer.BlockCopy(bytes, 0, floats, 0, bytes.Length);

			GetDatabaseProperties().ForEach((x, i) => x.Property.SetValue(instance, floats[i]));

			return instance;
		}

		public byte[] Serialize()
		{
			float[] floats = GetDatabaseProperties()
				.Select(x => x.Property.GetValue(this).GetSingle())
				.ToArray();
			byte[] bytes = new byte[floats.Length * sizeof(float)];
			Buffer.BlockCopy(floats, 0, bytes, 0, bytes.Length);

			return bytes;
		}

		private static CustomProperty[] GetProperties(Type type)
		{
			return type.GetProperties()
				.Select(p => new CustomProperty(
					p,
					p.Name,
					p.PropertyType,
					p.GetCustomAttribute<FeaturesAttribute>()
				)).ToArray();
		}

		public static CustomProperty[] GetProperties() => _properties = _properties ?? GetProperties(typeof(OptimizedHorseFeatures));

		public static CustomProperty[]? _properties;

		public static string[] GetFeaturesTypeNames()
		{
			return GetProperties()
				.Where(x => x.Attribute != null)
				.Select(x => x.Name)
				.ToArray();
		}

		public static string[] GetNormalizationNames()
		{
			return GetProperties()
				.Where(x => x.Attribute != null && x.Attribute.Normalization)
				.Select(x => x.Name)
				.ToArray();
		}

		public static string[] GetFeaturesTypeNames(FeaturesType type)
		{
			return GetProperties()
				.Where(x => x.Attribute != null && x.Attribute.Type.HasFlag(type))
				.Select(x => x.Name)
				.ToArray();
		}

		public static string[] GetNormalizationNames(FeaturesType type)
		{
			return GetProperties()
				.Where(x => x.Attribute != null && x.Attribute.Type.HasFlag(type) && x.Attribute.Normalization)
				.Select(x => x.Name)
				.ToArray();
		}

	}
}