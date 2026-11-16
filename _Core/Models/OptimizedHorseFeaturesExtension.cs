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

			var tukas = results.Select(x => x.Tuka).Where(x => !float.IsNaN(x)).OrderBy(x => x).ToArray();
			var pace = tukas.DefaultIfEmpty(float.NaN).Average();

			results.ForEach(x =>
			{
				// 距離
				x.DistanceDiffRank = x.GetRank(results, x => Math.Abs(x.DistanceDiff), false);
				// 前走距離差（abs値で順位、小さい方がﾌｨｯﾄ）
				x.DistanceFromLastRank = x.GetRank(results, x => Math.Abs(x.DistanceFromLast), false);

				// 脚質による有利不利(同じ脚質が少ないほど有利)
				x.PaceAdvantage = 1F - Math.Abs(pace - x.Tuka);

				// 脚質の並び（自分の Tuka が無い、または有効な Tuka が 2 頭未満なら NaN）
				x.TukaOrder = x.GetRank(results, x => x.Tuka, false);
				var hasTuka = !float.IsNaN(x.Tuka) && tukas.Length >= 2;
				x.FrontCount = hasTuka ? results.Count(y => !ReferenceEquals(y, x) && y.Tuka < 0.25F) : float.NaN;
				// 2 番手は同値も数える（研究の rank(method="first") == 2）
				x.LoneFront = hasTuka ? (x.Tuka == tukas[0] && tukas[1] - x.Tuka >= 0.1F ? 1F : 0F) : float.NaN;
				x.TukaPace = hasTuka ? pace * x.Tuka : float.NaN;

				// 過去 5 走の相手の強さと期待超過
				x.OppElo5Rank = x.GetRank(results, x => x.OppElo5, true);
				x.Excess5Rank = x.GetRank(results, x => x.Excess5, true);

				// Eloﾚｰﾃｨﾝｸﾞ（高い方が有利）
				x.TrainerEloRank = x.GetRank(results, x => x.TrainerElo, true);

				// 追切情報
				x.OikiriAdjustedTime5Rank = x.GetRank(results, x => x.OikiriAdjustedTime5, false);
				x.OikiriAdaptationRank = x.GetRank(results, x => x.OikiriAdaptation, true);
				x.OikiriTimeRatingRank = x.GetRank(results, x => x.OikiriTimeRating, true);
				x.OikiriTotalScoreZScore = x.GetZScore(results, x => x.OikiriTotalScore);

				// 過去ﾚｰｽの休養日数（個別）Rank
				x.RestDaysN1Rank = x.GetRank(results, x => x.RestDaysN1, true);
				x.RestDaysN2Rank = x.GetRank(results, x => x.RestDaysN2, true);
				x.RestDaysN3Rank = x.GetRank(results, x => x.RestDaysN3, true);
				// 過去ﾚｰｽの経過日数（個別）Rank
				x.DaysAgoN2Rank = x.GetRank(results, x => x.DaysAgoN2, false);
				x.DaysAgoN3Rank = x.GetRank(results, x => x.DaysAgoN3, false);
				x.DaysAgoN4Rank = x.GetRank(results, x => x.DaysAgoN4, false);

				// 着順（個別）Rank
				x.FinishPositionN2Rank = x.GetRank(results, x => x.FinishPositionN2, true);
				x.FinishPositionN3Rank = x.GetRank(results, x => x.FinishPositionN3, true);
				x.FinishPositionN4Rank = x.GetRank(results, x => x.FinishPositionN4, true);
				// 着順トレンド
				x.FinishPositionTrendZScore = x.GetZScore(results, x => x.FinishPositionTrend);

				// ﾄｯﾌﾟとのﾀｲﾑ差（個別）Rank
				x.Time2TopN3Rank = x.GetRank(results, x => x.Time2TopN3, false);
				x.Time2TopN4Rank = x.GetRank(results, x => x.Time2TopN4, false);

				// 同条件ﾚｰｽとのﾀｲﾑ差
				x.AvgTime2Condition3Rank = x.GetRank(results, x => x.AvgTime2Condition3, false);
				x.AvgTime2Condition5Rank = x.GetRank(results, x => x.AvgTime2Condition5, false);
				// 同条件ﾚｰｽとのﾀｲﾑ差（個別）Rank
				x.Time2ConditionN2Rank = x.GetRank(results, x => x.Time2ConditionN2, false);
				x.Time2ConditionN3Rank = x.GetRank(results, x => x.Time2ConditionN3, false);
				x.Time2ConditionN4Rank = x.GetRank(results, x => x.Time2ConditionN4, false);

				// 同条件ﾚｰｽとの3ﾊﾛﾝ差
				x.AvgLastThreeFurlongs2Condition3Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition3, false);
				x.AvgLastThreeFurlongs2Condition5Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition5, false);

				// 父馬-母父馬の兄弟馬の情報
				x.SireDamSireBrosAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireBrosAvgPrizeMoney, true);
				x.SireDamSireBrosAvgRatingRank = x.GetRank(results, x => x.SireDamSireBrosAvgRating, true);
				x.SireDamSireBrosDistanceDiffRank = x.GetRank(results, x => x.SireDamSireBrosDistanceDiff, false);
				x.SireDamSireBrosAvgFinishPositionZScore = x.GetZScore(results, x => x.SireDamSireBrosAvgFinishPosition);
				// 小さいほど良いので符号反転
				x.SireDamSireBrosAvgTime2TopZScore = -x.GetZScore(results, x => x.SireDamSireBrosAvgTime2Top);
				x.SireDamSireBrosAvgTime2ConditionZScore = -x.GetZScore(results, x => x.SireDamSireBrosAvgTime2Condition);
				x.SireDamSireBrosAvgLastThreeFurlongs2ConditionZScore = -x.GetZScore(results, x => x.SireDamSireBrosAvgLastThreeFurlongs2Condition);

				// 父馬-母父馬-馬場の情報
				x.SireDamSireTrackAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireTrackAvgPrizeMoney, true);
				x.SireDamSireTrackAvgRatingRank = x.GetRank(results, x => x.SireDamSireTrackAvgRating, true);
				// 父馬-母父馬-馬場状態の情報
				x.SireDamSireTrackConditionAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgPrizeMoney, true);
				x.SireDamSireTrackConditionAvgRatingRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgRating, true);
				x.SireDamSireTrackConditionAvgTime2TopRank = x.GetRank(results, x => x.SireDamSireTrackConditionAvgTime2Top, false);
				// 父馬-母父馬-距離の情報
				x.SireDamSireDistanceAvgTime2ConditionRank = x.GetRank(results, x => x.SireDamSireDistanceAvgTime2Condition, false);

				// 騎手-馬場状態の情報
				x.JockeyTrackConditionAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyTrackConditionAvgTime2Condition, false);

				// 生産者・調教師-生産者の情報
				x.BreederAvgTime2ConditionRank = x.GetRank(results, x => x.BreederAvgTime2Condition, false);
				x.TrainerBreederAvgTime2ConditionRank = x.GetRank(results, x => x.TrainerBreederAvgTime2Condition, false);
			});

			return results;
		}
	}
}
