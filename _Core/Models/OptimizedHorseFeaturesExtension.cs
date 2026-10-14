using System;
using System.Collections.Generic;
using System.Linq;
using TBird.Core;

namespace Netkeiba.Models
{
	public static class OptimizedHorseFeaturesExtension
	{
		public static OptimizedHorseFeatures[] CalculateInRaces(this IEnumerable<OptimizedHorseFeatures> features)
		{
			var results = features.ToArray();

			var pace = results.Select(x => x.Tuka).Where(x => !float.IsNaN(x)).DefaultIfEmpty(float.NaN).Average();

			results.ForEach(x =>
			{
				// 出走経験数
				x.RaceCountRank = x.GetRank(results, x => x.RaceCount, true);

				// 獲得賞金
				x.AvgPrizeMoneyRank = x.GetRank(results, x => x.AvgPrizeMoney, true);
				x.MaxPrizeMoneyRank = x.GetRank(results, x => x.MaxPrizeMoney, true);

				//// ﾚｰﾃｨﾝｸﾞ
				x.AvgRatingRank = x.GetRank(results, x => x.AvgRating, true);
				x.MaxRatingRank = x.GetRank(results, x => x.MaxRating, true);

				// 距離
				x.DistanceDiffRank = x.GetRank(results, x => Math.Abs(x.DistanceDiff), false);
				// 前走距離差（abs値で順位、小さい方がﾌｨｯﾄ）
				x.DistanceFromLastRank = x.GetRank(results, x => Math.Abs(x.DistanceFromLast), false);
				// クラス変化（昇級が大きいほど不利）
				x.GradeChangeRank = x.GetRank(results, x => x.GradeChange, false);

				// 脚質による有利不利(同じ脚質が少ないほど有利)
				x.PaceAdvantage = 1F - Math.Abs(pace - x.Tuka);

				// Eloﾚｰﾃｨﾝｸﾞ — 通常（高い方が有利）
				x.HorseEloRank = x.GetRank(results, x => x.HorseElo, true);
				x.JockeyEloRank = x.GetRank(results, x => x.JockeyElo, true);
				x.TrainerEloRank = x.GetRank(results, x => x.TrainerElo, true);
				// Eloﾚｰﾃｨﾝｸﾞ — 早期収束（高い方が有利）
				x.FastHorseEloRank = x.GetRank(results, x => x.FastHorseElo, true);
				x.FastJockeyEloRank = x.GetRank(results, x => x.FastJockeyElo, true);
				x.FastTrainerEloRank = x.GetRank(results, x => x.FastTrainerElo, true);

				// 追切情報
				x.OikiriAdjustedTime5Rank = x.GetRank(results, x => x.OikiriAdjustedTime5, false);
				x.OikiriRatingRank = x.GetRank(results, x => x.OikiriRating, true);
				x.OikiriAdaptationRank = x.GetRank(results, x => x.OikiriAdaptation, true);
				x.OikiriTimeRatingRank = x.GetRank(results, x => x.OikiriTimeRating, true);
				x.OikiriTotalScoreRank = x.GetRank(results, x => x.OikiriTotalScore, true);

				// 過去ﾚｰｽの休養日数（個別）Rank
				x.RestDaysN1Rank = x.GetRank(results, x => x.RestDaysN1, true);
				x.RestDaysN2Rank = x.GetRank(results, x => x.RestDaysN2, true);
				x.RestDaysN3Rank = x.GetRank(results, x => x.RestDaysN3, true);
				x.RestDaysN4Rank = x.GetRank(results, x => x.RestDaysN4, true);
				x.RestDaysN5Rank = x.GetRank(results, x => x.RestDaysN5, true);
				// 過去ﾚｰｽの経過日数（個別）Rank
				x.DaysAgoN1Rank = x.GetRank(results, x => x.DaysAgoN1, false);
				x.DaysAgoN2Rank = x.GetRank(results, x => x.DaysAgoN2, false);
				x.DaysAgoN3Rank = x.GetRank(results, x => x.DaysAgoN3, false);
				x.DaysAgoN4Rank = x.GetRank(results, x => x.DaysAgoN4, false);
				x.DaysAgoN5Rank = x.GetRank(results, x => x.DaysAgoN5, false);

				// 着順
				x.AvgFinishPosition3Rank = x.GetRank(results, x => x.AvgFinishPosition3, true);
				x.MaxFinishPosition3Rank = x.GetRank(results, x => x.MaxFinishPosition3, true);
				x.AvgFinishPosition5Rank = x.GetRank(results, x => x.AvgFinishPosition5, true);
				x.MaxFinishPosition5Rank = x.GetRank(results, x => x.MaxFinishPosition5, true);
				// 着順（個別）Rank
				x.FinishPositionN1Rank = x.GetRank(results, x => x.FinishPositionN1, true);
				x.FinishPositionN2Rank = x.GetRank(results, x => x.FinishPositionN2, true);
				x.FinishPositionN3Rank = x.GetRank(results, x => x.FinishPositionN3, true);
				x.FinishPositionN4Rank = x.GetRank(results, x => x.FinishPositionN4, true);
				x.FinishPositionN5Rank = x.GetRank(results, x => x.FinishPositionN5, true);
				// 着順トレンドRank
				x.FinishPositionTrendRank = x.GetRank(results, x => x.FinishPositionTrend, true);

				// ﾄｯﾌﾟとのﾀｲﾑ差
				x.AvgTime2Top3Rank = x.GetRank(results, x => x.AvgTime2Top3, false);
				x.MaxTime2Top3Rank = x.GetRank(results, x => x.MaxTime2Top3, false);
				x.AvgTime2Top5Rank = x.GetRank(results, x => x.AvgTime2Top5, false);
				x.MaxTime2Top5Rank = x.GetRank(results, x => x.MaxTime2Top5, false);
				// ﾄｯﾌﾟとのﾀｲﾑ差（個別）Rank
				x.Time2TopN1Rank = x.GetRank(results, x => x.Time2TopN1, false);
				x.Time2TopN2Rank = x.GetRank(results, x => x.Time2TopN2, false);
				x.Time2TopN3Rank = x.GetRank(results, x => x.Time2TopN3, false);
				x.Time2TopN4Rank = x.GetRank(results, x => x.Time2TopN4, false);
				x.Time2TopN5Rank = x.GetRank(results, x => x.Time2TopN5, false);

				// ﾄｯﾌﾟとの3ﾊﾛﾝ差
				x.AvgLastThreeFurlongs2Top1Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Top1, false);
				x.AvgLastThreeFurlongs2Top3Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Top3, false);
				x.MaxLastThreeFurlongs2Top3Rank = x.GetRank(results, x => x.MaxLastThreeFurlongs2Top3, false);
				x.AvgLastThreeFurlongs2Top5Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Top5, false);
				x.MaxLastThreeFurlongs2Top5Rank = x.GetRank(results, x => x.MaxLastThreeFurlongs2Top5, false);
				// 同条件ﾚｰｽとのﾀｲﾑ差
				x.AvgTime2Condition1Rank = x.GetRank(results, x => x.AvgTime2Condition1, false);
				x.AvgTime2Condition3Rank = x.GetRank(results, x => x.AvgTime2Condition3, false);
				x.MaxTime2Condition3Rank = x.GetRank(results, x => x.MaxTime2Condition3, false);
				x.AvgTime2Condition5Rank = x.GetRank(results, x => x.AvgTime2Condition5, false);
				x.MaxTime2Condition5Rank = x.GetRank(results, x => x.MaxTime2Condition5, false);
				// 同条件ﾚｰｽとのﾀｲﾑ差（個別）Rank
				x.Time2ConditionN1Rank = x.GetRank(results, x => x.Time2ConditionN1, false);
				x.Time2ConditionN2Rank = x.GetRank(results, x => x.Time2ConditionN2, false);
				x.Time2ConditionN3Rank = x.GetRank(results, x => x.Time2ConditionN3, false);
				x.Time2ConditionN4Rank = x.GetRank(results, x => x.Time2ConditionN4, false);
				x.Time2ConditionN5Rank = x.GetRank(results, x => x.Time2ConditionN5, false);

				// 同条件ﾚｰｽとの3ﾊﾛﾝ差
				x.AvgLastThreeFurlongs2Condition1Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition1, false);
				x.AvgLastThreeFurlongs2Condition3Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition3, false);
				x.MaxLastThreeFurlongs2Condition3Rank = x.GetRank(results, x => x.MaxLastThreeFurlongs2Condition3, false);
				x.AvgLastThreeFurlongs2Condition5Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition5, false);
				x.MaxLastThreeFurlongs2Condition5Rank = x.GetRank(results, x => x.MaxLastThreeFurlongs2Condition5, false);
				// 父馬の兄弟馬の実績
				x.SireBrosAvgPrizeMoneyRank = x.GetRank(results, x => x.SireBrosAvgPrizeMoney, true);
				x.SireBrosAvgRatingRank = x.GetRank(results, x => x.SireBrosAvgRating, true);
				x.SireBrosDistanceDiffRank = x.GetRank(results, x => x.SireBrosDistanceDiff, false);
				x.SireBrosAvgFinishPositionRank = x.GetRank(results, x => x.SireBrosAvgFinishPosition, true);
				x.SireBrosAvgTime2TopRank = x.GetRank(results, x => x.SireBrosAvgTime2Top, false);
				x.SireBrosAvgLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireBrosAvgLastThreeFurlongs2Top, false);
				x.SireBrosAvgTime2ConditionRank = x.GetRank(results, x => x.SireBrosAvgTime2Condition, false);
				x.SireBrosAvgLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireBrosAvgLastThreeFurlongs2Condition, false);

				// 父馬-母父馬の兄弟馬の情報
				x.SireDamSireBrosAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireBrosAvgPrizeMoney, true);
				x.SireDamSireBrosAvgRatingRank = x.GetRank(results, x => x.SireDamSireBrosAvgRating, true);
				x.SireDamSireBrosDistanceDiffRank = x.GetRank(results, x => x.SireDamSireBrosDistanceDiff, false);
				x.SireDamSireBrosAvgFinishPositionRank = x.GetRank(results, x => x.SireDamSireBrosAvgFinishPosition, true);
				x.SireDamSireBrosAvgTime2TopRank = x.GetRank(results, x => x.SireDamSireBrosAvgTime2Top, false);
				x.SireDamSireBrosAvgLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireDamSireBrosAvgLastThreeFurlongs2Top, false);
				x.SireDamSireBrosAvgTime2ConditionRank = x.GetRank(results, x => x.SireDamSireBrosAvgTime2Condition, false);
				x.SireDamSireBrosAvgLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireDamSireBrosAvgLastThreeFurlongs2Condition, false);

				// 父馬-馬場の情報
				x.SireTrackAvgPrizeMoneyRank = x.GetRank(results, x => x.SireTrackAvgPrizeMoney, true);
				x.SireTrackAvgRatingRank = x.GetRank(results, x => x.SireTrackAvgRating, true);
				x.SireTrackAvgFinishPositionRank = x.GetRank(results, x => x.SireTrackAvgFinishPosition, true);
				x.SireTrackAvgTime2TopRank = x.GetRank(results, x => x.SireTrackAvgTime2Top, false);
				x.SireTrackAvgTime2ConditionRank = x.GetRank(results, x => x.SireTrackAvgTime2Condition, false);
				// 父馬-馬場状態の情報
				x.SireTrackConditionAvgPrizeMoneyRank = x.GetRank(results, x => x.SireTrackConditionAvgPrizeMoney, true);
				x.SireTrackConditionAvgRatingRank = x.GetRank(results, x => x.SireTrackConditionAvgRating, true);
				x.SireTrackConditionAvgFinishPositionRank = x.GetRank(results, x => x.SireTrackConditionAvgFinishPosition, true);
				x.SireTrackConditionAvgTime2TopRank = x.GetRank(results, x => x.SireTrackConditionAvgTime2Top, false);
				x.SireTrackConditionAvgTime2ConditionRank = x.GetRank(results, x => x.SireTrackConditionAvgTime2Condition, false);

				// 父馬-距離の情報
				x.SireDistanceAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDistanceAvgPrizeMoney, true);
				x.SireDistanceAvgRatingRank = x.GetRank(results, x => x.SireDistanceAvgRating, true);
				x.SireDistanceAvgFinishPositionRank = x.GetRank(results, x => x.SireDistanceAvgFinishPosition, true);
				x.SireDistanceAvgTime2TopRank = x.GetRank(results, x => x.SireDistanceAvgTime2Top, false);
				x.SireDistanceAvgTime2ConditionRank = x.GetRank(results, x => x.SireDistanceAvgTime2Condition, false);

				// 父馬-母父馬-馬場の情報
				x.SireDamSireTrackAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireTrackAvgPrizeMoney, true);
				x.SireDamSireTrackAvgRatingRank = x.GetRank(results, x => x.SireDamSireTrackAvgRating, true);
				x.SireDamSireTrackAvgFinishPositionRank = x.GetRank(results, x => x.SireDamSireTrackAvgFinishPosition, true);
				x.SireDamSireTrackAvgTime2TopRank = x.GetRank(results, x => x.SireDamSireTrackAvgTime2Top, false);
				x.SireDamSireTrackAvgTime2ConditionRank = x.GetRank(results, x => x.SireDamSireTrackAvgTime2Condition, false);
				// 父馬-母父馬-馬場状態の情報
				x.SireDamSireTrackConditionAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgPrizeMoney, true);
				x.SireDamSireTrackConditionAvgRatingRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgRating, true);
				x.SireDamSireTrackConditionAvgFinishPositionRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgFinishPosition, true);
				x.SireDamSireTrackConditionAvgTime2TopRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgTime2Top, false);
				x.SireDamSireTrackConditionAvgTime2ConditionRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgTime2Condition, false);

				// 父馬-母父馬-距離の情報
				x.SireDamSireDistanceAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireDistanceAvgPrizeMoney, true);
				x.SireDamSireDistanceAvgRatingRank = x.GetRank(results, x => x.SireDamSireDistanceAvgRating, true);
				x.SireDamSireDistanceAvgFinishPositionRank = x.GetRank(results, x => x.SireDamSireDistanceAvgFinishPosition, true);
				x.SireDamSireDistanceAvgTime2TopRank = x.GetRank(results, x => x.SireDamSireDistanceAvgTime2Top, false);
				x.SireDamSireDistanceAvgTime2ConditionRank = x.GetRank(results, x => x.SireDamSireDistanceAvgTime2Condition, false);

				// 騎手の情報
				x.JockeyAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyAvgPrizeMoney, true);
				x.JockeyAvgRatingRank = x.GetRank(results, x => x.JockeyAvgRating, true);
				x.JockeyAvgFinishPositionRank = x.GetRank(results, x => x.JockeyAvgFinishPosition, true);
				x.JockeyAvgTime2TopRank = x.GetRank(results, x => x.JockeyAvgTime2Top, false);
				x.JockeyAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyAvgTime2Condition, false);

				// 騎手-ｺｰｽの情報
				x.JockeyPlaceAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyPlaceAvgPrizeMoney, true);
				x.JockeyPlaceAvgRatingRank = x.GetRank(results, x => x.JockeyPlaceAvgRating, true);
				x.JockeyPlaceAvgFinishPositionRank = x.GetRank(results, x => x.JockeyPlaceAvgFinishPosition, true);
				x.JockeyPlaceAvgTime2TopRank = x.GetRank(results, x => x.JockeyPlaceAvgTime2Top, false);
				x.JockeyPlaceAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyPlaceAvgTime2Condition, false);

				// 騎手-馬場の情報
				x.JockeyTrackAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyTrackAvgPrizeMoney, true);
				x.JockeyTrackAvgRatingRank = x.GetRank(results, x => x.JockeyTrackAvgRating, true);
				x.JockeyTrackAvgFinishPositionRank = x.GetRank(results, x => x.JockeyTrackAvgFinishPosition, true);
				x.JockeyTrackAvgTime2TopRank = x.GetRank(results, x => x.JockeyTrackAvgTime2Top, false);
				x.JockeyTrackAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyTrackAvgTime2Condition, false);
				// 騎手-馬場状態の情報
				x.JockeyTrackConditionAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyTrackConditionAvgPrizeMoney, true);
				x.JockeyTrackConditionAvgRatingRank = x.GetRank(results, x => x.JockeyTrackConditionAvgRating, true);
				x.JockeyTrackConditionAvgFinishPositionRank = x.GetRank(results, x => x.JockeyTrackConditionAvgFinishPosition, true);
				x.JockeyTrackConditionAvgTime2TopRank = x.GetRank(results, x => x.JockeyTrackConditionAvgTime2Top, false);
				x.JockeyTrackConditionAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyTrackConditionAvgTime2Condition, false);

				// 騎手-距離の情報
				x.JockeyDistanceAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyDistanceAvgPrizeMoney, true);
				x.JockeyDistanceAvgRatingRank = x.GetRank(results, x => x.JockeyDistanceAvgRating, true);
				x.JockeyDistanceAvgFinishPositionRank = x.GetRank(results, x => x.JockeyDistanceAvgFinishPosition, true);
				x.JockeyDistanceAvgTime2TopRank = x.GetRank(results, x => x.JockeyDistanceAvgTime2Top, false);
				x.JockeyDistanceAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyDistanceAvgTime2Condition, false);

				// 騎手-調教師の情報
				x.JockeyTrainerAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyTrainerAvgPrizeMoney, true);
				x.JockeyTrainerAvgRatingRank = x.GetRank(results, x => x.JockeyTrainerAvgRating, true);
				x.JockeyTrainerAvgFinishPositionRank = x.GetRank(results, x => x.JockeyTrainerAvgFinishPosition, true);
				x.JockeyTrainerAvgTime2TopRank = x.GetRank(results, x => x.JockeyTrainerAvgTime2Top, false);
				x.JockeyTrainerAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyTrainerAvgTime2Condition, false);

				// 調教師の情報
				x.TrainerAvgPrizeMoneyRank = x.GetRank(results, x => x.TrainerAvgPrizeMoney, true);
				x.TrainerAvgRatingRank = x.GetRank(results, x => x.TrainerAvgRating, true);
				x.TrainerAvgFinishPositionRank = x.GetRank(results, x => x.TrainerAvgFinishPosition, true);
				x.TrainerAvgTime2TopRank = x.GetRank(results, x => x.TrainerAvgTime2Top, false);
				x.TrainerAvgTime2ConditionRank = x.GetRank(results, x => x.TrainerAvgTime2Condition, false);

				// 調教師-場所の情報
				x.TrainerPlaceAvgPrizeMoneyRank = x.GetRank(results, x => x.TrainerPlaceAvgPrizeMoney, true);
				x.TrainerPlaceAvgRatingRank = x.GetRank(results, x => x.TrainerPlaceAvgRating, true);
				x.TrainerPlaceAvgFinishPositionRank = x.GetRank(results, x => x.TrainerPlaceAvgFinishPosition, true);
				x.TrainerPlaceAvgTime2TopRank = x.GetRank(results, x => x.TrainerPlaceAvgTime2Top, false);
				x.TrainerPlaceAvgTime2ConditionRank = x.GetRank(results, x => x.TrainerPlaceAvgTime2Condition, false);

				// 生産者の情報
				x.BreederAvgPrizeMoneyRank = x.GetRank(results, x => x.BreederAvgPrizeMoney, true);
				x.BreederAvgRatingRank = x.GetRank(results, x => x.BreederAvgRating, true);
				x.BreederAvgFinishPositionRank = x.GetRank(results, x => x.BreederAvgFinishPosition, true);
				x.BreederAvgTime2TopRank = x.GetRank(results, x => x.BreederAvgTime2Top, false);
				x.BreederAvgTime2ConditionRank = x.GetRank(results, x => x.BreederAvgTime2Condition, false);

				// 調教師-生産者の情報
				x.TrainerBreederAvgPrizeMoneyRank = x.GetRank(results, x => x.TrainerBreederAvgPrizeMoney, true);
				x.TrainerBreederAvgRatingRank = x.GetRank(results, x => x.TrainerBreederAvgRating, true);
				x.TrainerBreederAvgFinishPositionRank = x.GetRank(results, x => x.TrainerBreederAvgFinishPosition, true);
				x.TrainerBreederAvgTime2TopRank = x.GetRank(results, x => x.TrainerBreederAvgTime2Top, false);
				x.TrainerBreederAvgTime2ConditionRank = x.GetRank(results, x => x.TrainerBreederAvgTime2Condition, false);

				// 交差特徴量
				x.CrossOikiriFinishPosRank = x.GetRank(results, x => x.CrossOikiriFinishPos, true);
				x.CrossOikiriTime2TopRank = x.GetRank(results, x => x.CrossOikiriTime2Top, true);
				x.CrossDistanceFitFinishPosRank = x.GetRank(results, x => x.CrossDistanceFitFinishPos, true);
				x.CrossRestDaysFinishPosRank = x.GetRank(results, x => x.CrossRestDaysFinishPos, true);
				x.CrossRatingOikiriRank = x.GetRank(results, x => x.CrossRatingOikiri, true);
				x.CrossOikiriJockeyRank = x.GetRank(results, x => x.CrossOikiriJockey, true);

				// ========== Z-score計算 ==========
				// A. パフォーマンス実績
				x.AvgPrizeMoneyZScore = x.GetZScore(results, x => x.AvgPrizeMoney);
				x.MaxPrizeMoneyZScore = x.GetZScore(results, x => x.MaxPrizeMoney);
				x.AvgRatingZScore = x.GetZScore(results, x => x.AvgRating);
				x.MaxRatingZScore = x.GetZScore(results, x => x.MaxRating);

				// B. 着順指標（集計）
				x.AvgFinishPosition3ZScore = x.GetZScore(results, x => x.AvgFinishPosition3);
				x.MaxFinishPosition3ZScore = x.GetZScore(results, x => x.MaxFinishPosition3);
				x.AvgFinishPosition5ZScore = x.GetZScore(results, x => x.AvgFinishPosition5);
				x.MaxFinishPosition5ZScore = x.GetZScore(results, x => x.MaxFinishPosition5);
				x.FinishPositionTrendZScore = x.GetZScore(results, x => x.FinishPositionTrend);

				// C. タイム差指標（集計）- 小さいほど良いので符号反転
				x.AvgTime2Top3ZScore = -x.GetZScore(results, x => x.AvgTime2Top3);
				x.MaxTime2Top3ZScore = -x.GetZScore(results, x => x.MaxTime2Top3);
				x.AvgTime2Top5ZScore = -x.GetZScore(results, x => x.AvgTime2Top5);
				x.MaxTime2Top5ZScore = -x.GetZScore(results, x => x.MaxTime2Top5);
				x.AvgTime2Condition1ZScore = -x.GetZScore(results, x => x.AvgTime2Condition1);
				x.AvgTime2Condition3ZScore = -x.GetZScore(results, x => x.AvgTime2Condition3);
				x.MaxTime2Condition3ZScore = -x.GetZScore(results, x => x.MaxTime2Condition3);
				x.AvgTime2Condition5ZScore = -x.GetZScore(results, x => x.AvgTime2Condition5);
				x.MaxTime2Condition5ZScore = -x.GetZScore(results, x => x.MaxTime2Condition5);
				x.AvgLastThreeFurlongs2Top1ZScore = -x.GetZScore(results, x => x.AvgLastThreeFurlongs2Top1);
				x.AvgLastThreeFurlongs2Top3ZScore = -x.GetZScore(results, x => x.AvgLastThreeFurlongs2Top3);
				x.MaxLastThreeFurlongs2Top3ZScore = -x.GetZScore(results, x => x.MaxLastThreeFurlongs2Top3);
				x.AvgLastThreeFurlongs2Top5ZScore = -x.GetZScore(results, x => x.AvgLastThreeFurlongs2Top5);
				x.MaxLastThreeFurlongs2Top5ZScore = -x.GetZScore(results, x => x.MaxLastThreeFurlongs2Top5);
				x.AvgLastThreeFurlongs2Condition1ZScore = -x.GetZScore(results, x => x.AvgLastThreeFurlongs2Condition1);
				x.AvgLastThreeFurlongs2Condition3ZScore = -x.GetZScore(results, x => x.AvgLastThreeFurlongs2Condition3);
				x.MaxLastThreeFurlongs2Condition3ZScore = -x.GetZScore(results, x => x.MaxLastThreeFurlongs2Condition3);
				x.AvgLastThreeFurlongs2Condition5ZScore = -x.GetZScore(results, x => x.AvgLastThreeFurlongs2Condition5);
				x.MaxLastThreeFurlongs2Condition5ZScore = -x.GetZScore(results, x => x.MaxLastThreeFurlongs2Condition5);

				// D. 追切情報
				x.OikiriAdjustedTime5ZScore = -x.GetZScore(results, x => x.OikiriAdjustedTime5); // 小さいほど良い
				x.OikiriRatingZScore = x.GetZScore(results, x => x.OikiriRating);
				x.OikiriAdaptationZScore = x.GetZScore(results, x => x.OikiriAdaptation);
				x.OikiriTimeRatingZScore = x.GetZScore(results, x => x.OikiriTimeRating);
				x.OikiriTotalScoreZScore = x.GetZScore(results, x => x.OikiriTotalScore);

				// E. 基本指標
				x.PurchasePriceZScore = x.GetZScore(results, x => x.PurchasePrice);
				x.WeightZScore = x.GetZScore(results, x => x.Weight);
				x.DistanceDiffZScore = -x.GetZScore(results, x => Math.Abs(x.DistanceDiff)); // 小さいほど良い

				// F. 血統情報 - 父馬の兄弟馬
				x.SireBrosAvgPrizeMoneyZScore = x.GetZScore(results, x => x.SireBrosAvgPrizeMoney);
				x.SireBrosAvgRatingZScore = x.GetZScore(results, x => x.SireBrosAvgRating);
				x.SireBrosDistanceDiffZScore = -x.GetZScore(results, x => x.SireBrosDistanceDiff); // 小さいほど良い
				x.SireBrosAvgFinishPositionZScore = x.GetZScore(results, x => x.SireBrosAvgFinishPosition);
				x.SireBrosAvgTime2TopZScore = -x.GetZScore(results, x => x.SireBrosAvgTime2Top); // 小さいほど良い
				x.SireBrosAvgTime2ConditionZScore = -x.GetZScore(results, x => x.SireBrosAvgTime2Condition);
				x.SireBrosAvgLastThreeFurlongs2TopZScore = -x.GetZScore(results, x => x.SireBrosAvgLastThreeFurlongs2Top);
				x.SireBrosAvgLastThreeFurlongs2ConditionZScore = -x.GetZScore(results, x => x.SireBrosAvgLastThreeFurlongs2Condition);
				// 父馬-母父馬の兄弟馬
				x.SireDamSireBrosAvgPrizeMoneyZScore = x.GetZScore(results, x => x.SireDamSireBrosAvgPrizeMoney);
				x.SireDamSireBrosAvgRatingZScore = x.GetZScore(results, x => x.SireDamSireBrosAvgRating);
				x.SireDamSireBrosDistanceDiffZScore = -x.GetZScore(results, x => x.SireDamSireBrosDistanceDiff);
				x.SireDamSireBrosAvgFinishPositionZScore = x.GetZScore(results, x => x.SireDamSireBrosAvgFinishPosition);
				x.SireDamSireBrosAvgTime2TopZScore = -x.GetZScore(results, x => x.SireDamSireBrosAvgTime2Top);
				x.SireDamSireBrosAvgTime2ConditionZScore = -x.GetZScore(results, x => x.SireDamSireBrosAvgTime2Condition);
				x.SireDamSireBrosAvgLastThreeFurlongs2TopZScore = -x.GetZScore(results, x => x.SireDamSireBrosAvgLastThreeFurlongs2Top);
				x.SireDamSireBrosAvgLastThreeFurlongs2ConditionZScore = -x.GetZScore(results, x => x.SireDamSireBrosAvgLastThreeFurlongs2Condition);
				// 父馬-馬場
				x.SireTrackAvgPrizeMoneyZScore = x.GetZScore(results, x => x.SireTrackAvgPrizeMoney);
				x.SireDistanceAvgPrizeMoneyZScore = x.GetZScore(results, x => x.SireDistanceAvgPrizeMoney);
				// 父馬-母父馬-馬場
				x.SireDamSireTrackAvgPrizeMoneyZScore = x.GetZScore(results, x => x.SireDamSireTrackAvgPrizeMoney);
				x.SireDamSireDistanceAvgPrizeMoneyZScore = x.GetZScore(results, x => x.SireDamSireDistanceAvgPrizeMoney);

				// G. コネクション
				x.JockeyAvgPrizeMoneyZScore = x.GetZScore(results, x => x.JockeyAvgPrizeMoney);
				x.JockeyAvgRatingZScore = x.GetZScore(results, x => x.JockeyAvgRating);
				x.TrainerAvgPrizeMoneyZScore = x.GetZScore(results, x => x.TrainerAvgPrizeMoney);
				x.TrainerAvgRatingZScore = x.GetZScore(results, x => x.TrainerAvgRating);
				x.TrainerPlaceAvgPrizeMoneyZScore = x.GetZScore(results, x => x.TrainerPlaceAvgPrizeMoney);
				x.BreederAvgPrizeMoneyZScore = x.GetZScore(results, x => x.BreederAvgPrizeMoney);
				x.BreederAvgRatingZScore = x.GetZScore(results, x => x.BreederAvgRating);
				x.JockeyPlaceAvgPrizeMoneyZScore = x.GetZScore(results, x => x.JockeyPlaceAvgPrizeMoney);
				x.JockeyTrackAvgPrizeMoneyZScore = x.GetZScore(results, x => x.JockeyTrackAvgPrizeMoney);
				x.JockeyDistanceAvgPrizeMoneyZScore = x.GetZScore(results, x => x.JockeyDistanceAvgPrizeMoney);
				x.JockeyTrainerAvgPrizeMoneyZScore = x.GetZScore(results, x => x.JockeyTrainerAvgPrizeMoney);
				x.TrainerBreederAvgPrizeMoneyZScore = x.GetZScore(results, x => x.TrainerBreederAvgPrizeMoney);

			});

			// 上記設定した内容を使用した集計
			results.ForEach(x =>
			{
				// 脚質による有利不利(同じ脚質が少ないほど有利)
				x.PaceAdvantageRank = x.GetRank(results, x => x.PaceAdvantage, true);
			});

			return results;
		}
	}
}
