using Codeplex.Data;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
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

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float RestType { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float NearestRaces { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float Age { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AgeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float Gender { get; set; }

		// 同ﾚｰｽ他馬との比較
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float PurchasePriceRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float JockeyWeightRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float WeightRank { get; set; }

		// 実績の集計 (直近5戦)
		// 獲得賞金
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxPrizeMoneyRank { get; set; }

		// ﾚｰﾃｨﾝｸﾞ
		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float AvgRating { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float AvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float MaxRating { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxRatingRank { get; set; }

		// ｸﾞﾚｰﾄﾞ
		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float AvgGrade { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float AvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float MaxGrade { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxGradeRank { get; set; }

		// 距離
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float DistanceDiff { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float DistanceDiffRank { get; set; }

		// 通過順
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float Tuka { get; set; }

		// ﾍﾟｰｽ予想
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float PaceAdvantage { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float PaceAdvantageRank { get; set; }

		// 追切情報
		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri, Normalization = true)]
		public float OikiriAdjustedTime5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriAdjustedTime5Rank { get; set; }

		//[Features(Type = FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriRatingRank { get; set; }

		//[Features(Type = FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriAdaptation { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriAdaptationRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriTimeRating { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriTimeRatingRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriTotalScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse | FeaturesType.Oikiri)]
		public float OikiriTotalScoreRank { get; set; }

		// 着順
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgFinishPosition1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgFinishPosition1Rank { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxFinishPosition1 { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxFinishPosition1Rank { get; set; }

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

		// 調整ｽｺｱ
		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgAdjustedScore1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgAdjustedScore1Rank { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxAdjustedScore1 { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxAdjustedScore1Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgAdjustedScore3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgAdjustedScore3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxAdjustedScore3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxAdjustedScore3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgAdjustedScore5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgAdjustedScore5Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxAdjustedScore5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxAdjustedScore5Rank { get; set; }

		// ﾄｯﾌﾟとのﾀｲﾑ差
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Top1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Top1Rank { get; set; }

		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float MaxTime2Top1 { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxTime2Top1Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Top3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxTime2Top3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Top5Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxTime2Top5Rank { get; set; }

		// 同条件ﾚｰｽとのﾀｲﾑ差
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Condition1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Condition1Rank { get; set; }

		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float MaxTime2Condition1 { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxTime2Condition1Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Condition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Condition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxTime2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgTime2Condition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgTime2Condition5Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxTime2Condition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxTime2Condition5Rank { get; set; }

		// ﾄｯﾌﾟとの3ﾊﾛﾝ差
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Top1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top1Rank { get; set; }

		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float MaxLastThreeFurlongs2Top1 { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxLastThreeFurlongs2Top1Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Top3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Top3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Top5Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Top5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Top5Rank { get; set; }

		// 同条件ﾚｰｽとの3ﾊﾛﾝ差
		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Condition1 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Condition1Rank { get; set; }

		//[Features(Type = FeaturesType.Horse, Normalization = true)]
		//public float MaxLastThreeFurlongs2Condition1 { get; set; }

		//[Features(Type = FeaturesType.Horse)]
		//public float MaxLastThreeFurlongs2Condition1Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Condition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Condition3 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Condition3Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float AvgLastThreeFurlongs2Condition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float AvgLastThreeFurlongs2Condition5Rank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse, Normalization = true)]
		public float MaxLastThreeFurlongs2Condition5 { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Horse)]
		public float MaxLastThreeFurlongs2Condition5Rank { get; set; }

		// 父馬の兄弟馬の情報
		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		public float SireBrosAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		public float SireBrosMaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		//public float SireBrosAvgRating { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		//public float SireBrosMaxRating { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		public float SireBrosDistanceDiff { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosDistanceDiffRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		public float SireBrosAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		//public float SireBrosMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		public float SireBrosAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		//public float SireBrosMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxTime2ConditionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		public float SireBrosAvgLastThreeFurlongs2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgLastThreeFurlongs2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		//public float SireBrosMaxLastThreeFurlongs2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxLastThreeFurlongs2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		public float SireBrosAvgLastThreeFurlongs2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		public float SireBrosAvgLastThreeFurlongs2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros, Normalization = true)]
		//public float SireBrosMaxLastThreeFurlongs2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireBros)]
		//public float SireBrosMaxLastThreeFurlongs2ConditionRank { get; set; }

		// 母父馬の兄弟馬の情報
		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosAvgPrizeMoney { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosMaxPrizeMoney { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosAvgRating { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosMaxRating { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		public float DamSireBrosDistanceDiff { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		public float DamSireBrosDistanceDiffRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		public float DamSireBrosAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		public float DamSireBrosAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		public float DamSireBrosAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		public float DamSireBrosAvgAdjustedScoreRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxAdjustedScore { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxAdjustedScoreRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosAvgTime2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosAvgTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosAvgLastThreeFurlongs2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgLastThreeFurlongs2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosMaxLastThreeFurlongs2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxLastThreeFurlongs2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosAvgLastThreeFurlongs2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosAvgLastThreeFurlongs2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros, Normalization = true)]
		//public float DamSireBrosMaxLastThreeFurlongs2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.DamSireBros)]
		//public float DamSireBrosMaxLastThreeFurlongs2ConditionRank { get; set; }

		// 父馬-母父馬の兄弟馬の情報
		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		public float SireDamSireBrosAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		public float SireDamSireBrosMaxPrizeMoney { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		//public float SireDamSireBrosAvgRating { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		//public float SireDamSireBrosMaxRating { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		public float SireDamSireBrosDistanceDiff { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosDistanceDiffRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		public float SireDamSireBrosAvgTime2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		//public float SireDamSireBrosMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		public float SireDamSireBrosAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		//public float SireDamSireBrosMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxTime2ConditionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		public float SireDamSireBrosAvgLastThreeFurlongs2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgLastThreeFurlongs2TopRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		//public float SireDamSireBrosMaxLastThreeFurlongs2Top { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxLastThreeFurlongs2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		public float SireDamSireBrosAvgLastThreeFurlongs2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		public float SireDamSireBrosAvgLastThreeFurlongs2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros, Normalization = true)]
		//public float SireDamSireBrosMaxLastThreeFurlongs2Condition { get; set; }

		//[Features(Type = FeaturesType.Blood | FeaturesType.Bros | FeaturesType.SireDamSireBros)]
		//public float SireDamSireBrosMaxLastThreeFurlongs2ConditionRank { get; set; }

		// 騎手の情報
		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyMaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		//public float JockeyAvgRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		//public float JockeyMaxRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		//public float JockeyMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		public float JockeyAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey)]
		public float JockeyAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey, Normalization = true)]
		//public float JockeyMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey)]
		//public float JockeyMaxTime2ConditionRank { get; set; }

		// 騎手-場所相性の情報
		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		public float JockeyPlaceAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		public float JockeyPlaceMaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		//public float JockeyPlaceAvgRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		//public float JockeyPlaceMaxRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		public float JockeyPlaceAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		//public float JockeyPlaceMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		public float JockeyPlaceAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		public float JockeyPlaceAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace, Normalization = true)]
		//public float JockeyPlaceMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyPlace)]
		//public float JockeyPlaceMaxTime2ConditionRank { get; set; }

		// 騎手-馬場相性の情報
		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		public float JockeyTrackAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		public float JockeyTrackMaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		//public float JockeyTrackAvgRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		//public float JockeyTrackMaxRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		public float JockeyTrackAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		//public float JockeyTrackMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		public float JockeyTrackAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		public float JockeyTrackAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack, Normalization = true)]
		//public float JockeyTrackMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Jockey | FeaturesType.JockeyTrack)]
		//public float JockeyTrackMaxTime2ConditionRank { get; set; }

		// 調教師の情報
		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		public float TrainerAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		public float TrainerMaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		//public float TrainerAvgRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		//public float TrainerMaxRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		public float TrainerAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		//public float TrainerMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		public float TrainerAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Trainer)]
		public float TrainerAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer, Normalization = true)]
		//public float TrainerMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Trainer)]
		//public float TrainerMaxTime2ConditionRank { get; set; }

		// 生産者の情報
		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		public float BreederAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		public float BreederMaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		//public float BreederAvgRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		//public float BreederMaxRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		public float BreederAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		//public float BreederMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		public float BreederAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.Breeder)]
		public float BreederAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder, Normalization = true)]
		//public float BreederMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.Breeder)]
		//public float BreederMaxTime2ConditionRank { get; set; }

		// 調教師-生産者相性の情報
		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		public float TrainerBreederAvgPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederAvgPrizeMoneyRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		public float TrainerBreederMaxPrizeMoney { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederMaxPrizeMoneyRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		//public float TrainerBreederAvgRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederAvgRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		//public float TrainerBreederMaxRating { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederMaxRatingRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederAvgGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederAvgGradeRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederMaxGrade { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederMaxGradeRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederAvgFinishPosition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederAvgFinishPositionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederMaxFinishPosition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederMaxFinishPositionRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederAvgAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederAvgAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederMaxAdjustedScore { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederMaxAdjustedScoreRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		public float TrainerBreederAvgTime2Top { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederAvgTime2TopRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		//public float TrainerBreederMaxTime2Top { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		//public float TrainerBreederMaxTime2TopRank { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		public float TrainerBreederAvgTime2Condition { get; set; }

		[Features(Type = FeaturesType.All | FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederAvgTime2ConditionRank { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder, Normalization = true)]
		public float TrainerBreederMaxTime2Condition { get; set; }

		//[Features(Type = FeaturesType.Connection | FeaturesType.TrainerBreeder)]
		public float TrainerBreederMaxTime2ConditionRank { get; set; }

	}

	public partial class OptimizedHorseFeatures
	{
		public static OptimizedHorseFeatures? Deserialize(Dictionary<string, object> x)
		{
			var instance = new OptimizedHorseFeatures();

			foreach (var p in GetProperties())
			{
				p.SetProperty(instance, x);
			}

			return instance;
		}

		public static CustomProperty[] GetProperties() => _properties = _properties ?? typeof(OptimizedHorseFeatures).GetPropertiesEX().ToArray();

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