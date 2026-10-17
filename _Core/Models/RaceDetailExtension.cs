using System;
using System.Collections.Generic;
using System.Linq;
using TBird.Core;

namespace Netkeiba.Models
{
	public static class RaceDetailExtension
	{
		private static float DefaultFinishPosition = float.NaN;
		private static float DefaultRating = float.NaN;
		private static float DefaultTime2Top = float.NaN;
		private static float DefaultLastThreeFurlongs2Top = float.NaN;

		public static OptimizedHorseFeatures ExtractFeatures(this RaceDetail detail, RaceDetail[] inraces)
		{

			var horses = PreviousDataSets.GetHorses(detail);
			// 対象馬の情報
			var horses1 = GetHorseScoreMetrics(detail, horses.Take(1).ToList());
			var horses3 = GetHorseScoreMetrics(detail, horses.Take(3).ToList());
			var horses5 = GetHorseScoreMetrics(detail, horses.Take(5).ToList());
			// 個別レースの値
			var fpArr = horses.Select(CalculateFinishPosition).Take(5).ToArray();
			var t2tArr = horses.Select(x => x.Time2Top).Take(5).ToArray();
			var t2cArr = horses.Select(x => x.Time2Avg).Take(5).ToArray();
			var restDaysArr = Enumerable.Range(0, Math.Min(5, horses.Count)).Select(i =>
				i + 1 < horses.Count
				? Math.Min((float)(horses[i].Race.RaceDate - horses[i + 1].Race.RaceDate).TotalDays, 365F)
				: float.NaN
			).ToArray();
			var daysAgoArr = horses.Select(x =>
				(float)(detail.Race.RaceDate - x.Race.RaceDate).TotalDays
			).Take(5).ToArray();
			// 兄弟馬
			var sirebros = GetHorseScoreMetrics(detail, PreviousDataSets.GetSires(detail));
			var siredamsirebros = GetHorseScoreMetrics(detail, PreviousDataSets.GetSireDamSires(detail));
			// 父馬-馬場・父馬母父馬-馬場情報
			var siretracks = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireTracks(detail));
			var siredamsiretracks = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireDamSireTracks(detail));
			// 父馬-馬場状態・父馬母父馬-馬場状態情報
			var siretrackconditions = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireTrackConditions(detail));
			var siredamsiretrackconditions = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireDamSireTrackConditions(detail));
			// 父馬・父馬母父馬-距離情報
			var siredistances = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireDistances(detail));
			var siredamsiredistances = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireDamSireDistances(detail));
			// 関係者情報
			var jockeys = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeys(detail));
			var jockeyplaces = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyPlaces(detail));
			var jockeytracks = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyTracks(detail));
			var jockeytrackconditions = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyTrackConditions(detail));
			var jockeydistances = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyDistances(detail));
			var jockeytrainers = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyTrainers(detail));
			var trainers = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainers(detail));
			var trainerplaces = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainerPlaces(detail));
			var breeders = GetConnectionScoreMetrics(detail, PreviousDataSets.GetBreeders(detail));
			var trainerbreeders = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainerBreeders(detail));

			// 休養日数とベル型変換（交差特徴量で使用）
			var restDays = detail.Last != null
				? Math.Min((float)(detail.Race.RaceDate - detail.Last.Race.RaceDate).Days, 365F)
				: float.NaN;
			var restDaysFactor = restDays <= 90F ? restDays / 90F
				: restDays <= 180F ? 1.0F
				: Math.Max(0F, 1F - (restDays - 180F) / 185F);

			var features = new OptimizedHorseFeatures()
			{
				// **********
				// ｷｰ情報
				RaceId = detail.RaceId,
				Horse = detail.Horse,

				// **********
				// 当日の情報
				// 前走からの経過日数
				RestDays = restDays,
				// 出走経験数
				RaceCount = horses.Count,
				// 年齢
				Age = detail.Age,
				// 年齢ﾗﾝｸ(距離ごとの最適年齢に近い方が良い)
				AgeRank = detail.GetRank(inraces, x => Math.Abs(CalculateBestAge(x) - x.Age), false),
				// 性別(牡：1.0, セ：0.5, 牝：0.0)
				Gender = detail.Gender switch
				{
					"牝" => 0.0F,
					"セ" => 0.5F,
					_ => 1.0F
				},
				// 枠番・馬番
				Wakuban = detail.Wakuban,
				Umaban = detail.Umaban,
				// 前走変化
				JockeyChange = detail.Last == null ? float.NaN : detail.Jockey != detail.Last.Jockey ? 1F : 0F,
				SurfaceChange = detail.Last == null ? float.NaN : detail.Race.TrackType != detail.Last.Race.TrackType ? 1F : 0F,
				// 過去ﾚｰｽの休養日数（個別）
				RestDaysN1 = restDaysArr.Length > 0 ? restDaysArr[0] : float.NaN,
				RestDaysN2 = restDaysArr.Length > 1 ? restDaysArr[1] : float.NaN,
				RestDaysN3 = restDaysArr.Length > 2 ? restDaysArr[2] : float.NaN,
				RestDaysN4 = restDaysArr.Length > 3 ? restDaysArr[3] : float.NaN,
				RestDaysN5 = restDaysArr.Length > 4 ? restDaysArr[4] : float.NaN,
				// 過去ﾚｰｽの経過日数（個別）
				DaysAgoN1 = daysAgoArr.Length > 0 ? daysAgoArr[0] : float.NaN,
				DaysAgoN2 = daysAgoArr.Length > 1 ? daysAgoArr[1] : float.NaN,
				DaysAgoN3 = daysAgoArr.Length > 2 ? daysAgoArr[2] : float.NaN,
				DaysAgoN4 = daysAgoArr.Length > 3 ? daysAgoArr[3] : float.NaN,
				DaysAgoN5 = daysAgoArr.Length > 4 ? daysAgoArr[4] : float.NaN,
				// 購入金額ﾗﾝｸ
				PurchasePrice = detail.PurchasePrice,
				PurchasePriceRank = detail.GetRank(inraces, x => x.PurchasePrice, true),
				// 斤量ﾗﾝｸ(軽い方が良い)
				JockeyWeight = detail.JockeyWeight,
				JockeyWeightRank = detail.GetRank(inraces, x => x.JockeyWeight, false),
				// 体重ﾗﾝｸ(重い方が良い)
				Weight = detail.Weight,
				WeightRank = detail.GetRank(inraces, x => x.Weight, true),
				// 体重増減ﾗﾝｸ(少ない方が良い)
				WeightDiff = detail.WeightChange,
				WeightDiffRank = detail.GetRank(inraces, x => Math.Abs(x.WeightChange), false),

				// 獲得賞金
				AvgPrizeMoney = horses5.AvgPrizeMoney,
				MaxPrizeMoney = horses5.MaxPrizeMoney,

				//// ﾚｰﾃｨﾝｸﾞ
				AvgRating = horses5.AvgRating,
				MaxRating = horses5.MaxRating,

				// 距離
				DistanceDiff = horses5.DistanceDiff,
				// 前走距離差
				DistanceFromLast = detail.Last != null ? detail.Race.Distance - detail.Last.Race.Distance : float.NaN,
				// クラス変化
				GradeChange = detail.Last != null
					? detail.Race.Grade.GetGradeFeatures() - detail.Last.Race.Grade.GetGradeFeatures()
					: float.NaN,

				// 通過順
				Tuka = horses5.Tuka,

				// Eloﾚｰﾃｨﾝｸﾞ — 通常(K=24)
				HorseElo = PreviousDataSets.GetHorseElo(detail),
				JockeyElo = PreviousDataSets.GetJockeyElo(detail),
				TrainerElo = PreviousDataSets.GetTrainerElo(detail),
				// Eloﾚｰﾃｨﾝｸﾞ — 早期収束(K=48)
				FastHorseElo = PreviousDataSets.GetFastHorseElo(detail),
				FastJockeyElo = PreviousDataSets.GetFastJockeyElo(detail),
				FastTrainerElo = PreviousDataSets.GetFastTrainerElo(detail),

				// 追切情報
				OikiriAdjustedTime5 = detail.Oikiri.AdjustedTime5,
				OikiriRating = detail.Oikiri.Rating,
				OikiriAdaptation = detail.Oikiri.Adaptation,
				OikiriTimeRating = detail.Oikiri.TimeRating,
				OikiriTotalScore = detail.Oikiri.TotalScore,

				// 着順
				AvgFinishPosition3 = horses3.AvgFinishPosition,
				MaxFinishPosition3 = horses3.MaxFinishPosition,
				AvgFinishPosition5 = horses5.AvgFinishPosition,
				MaxFinishPosition5 = horses5.MaxFinishPosition,
				// 着順（個別）
				FinishPositionN1 = fpArr.Length > 0 ? fpArr[0] : float.NaN,
				FinishPositionN2 = fpArr.Length > 1 ? fpArr[1] : float.NaN,
				FinishPositionN3 = fpArr.Length > 2 ? fpArr[2] : float.NaN,
				FinishPositionN4 = fpArr.Length > 3 ? fpArr[3] : float.NaN,
				FinishPositionN5 = fpArr.Length > 4 ? fpArr[4] : float.NaN,
				// 着順トレンド（N1-N2、正=改善）
				FinishPositionTrend = fpArr.Length >= 2 ? fpArr[0] - fpArr[1] : float.NaN,

				// ﾄｯﾌﾟとのﾀｲﾑ差
				AvgTime2Top3 = horses3.AvgTime2Top,
				MaxTime2Top3 = horses3.MaxTime2Top,
				AvgTime2Top5 = horses5.AvgTime2Top,
				MaxTime2Top5 = horses5.MaxTime2Top,
				// ﾄｯﾌﾟとのﾀｲﾑ差（個別）
				Time2TopN1 = t2tArr.Length > 0 ? t2tArr[0] : float.NaN,
				Time2TopN2 = t2tArr.Length > 1 ? t2tArr[1] : float.NaN,
				Time2TopN3 = t2tArr.Length > 2 ? t2tArr[2] : float.NaN,
				Time2TopN4 = t2tArr.Length > 3 ? t2tArr[3] : float.NaN,
				Time2TopN5 = t2tArr.Length > 4 ? t2tArr[4] : float.NaN,

				// ﾄｯﾌﾟとの3ﾊﾛﾝ差
				AvgLastThreeFurlongs2Top1 = horses1.AvgLastThreeFurlongs2Top,
				AvgLastThreeFurlongs2Top3 = horses3.AvgLastThreeFurlongs2Top,
				MaxLastThreeFurlongs2Top3 = horses3.MaxLastThreeFurlongs2Top,
				AvgLastThreeFurlongs2Top5 = horses5.AvgLastThreeFurlongs2Top,
				MaxLastThreeFurlongs2Top5 = horses5.MaxLastThreeFurlongs2Top,
				// 同条件ﾚｰｽとのﾀｲﾑ差
				AvgTime2Condition1 = horses1.AvgTime2Condition,
				AvgTime2Condition3 = horses3.AvgTime2Condition,
				MaxTime2Condition3 = horses3.MaxTime2Condition,
				AvgTime2Condition5 = horses5.AvgTime2Condition,
				MaxTime2Condition5 = horses5.MaxTime2Condition,
				// 同条件ﾚｰｽとのﾀｲﾑ差（個別）
				Time2ConditionN1 = t2cArr.Length > 0 ? t2cArr[0] : float.NaN,
				Time2ConditionN2 = t2cArr.Length > 1 ? t2cArr[1] : float.NaN,
				Time2ConditionN3 = t2cArr.Length > 2 ? t2cArr[2] : float.NaN,
				Time2ConditionN4 = t2cArr.Length > 3 ? t2cArr[3] : float.NaN,
				Time2ConditionN5 = t2cArr.Length > 4 ? t2cArr[4] : float.NaN,

				// 同条件ﾚｰｽとの3ﾊﾛﾝ差
				AvgLastThreeFurlongs2Condition1 = horses1.AvgLastThreeFurlongs2Condition,
				AvgLastThreeFurlongs2Condition3 = horses3.AvgLastThreeFurlongs2Condition,
				MaxLastThreeFurlongs2Condition3 = horses3.MaxLastThreeFurlongs2Condition,
				AvgLastThreeFurlongs2Condition5 = horses5.AvgLastThreeFurlongs2Condition,
				MaxLastThreeFurlongs2Condition5 = horses5.MaxLastThreeFurlongs2Condition,
				// 父馬の兄弟馬の実績
				SireBrosAvgPrizeMoney = sirebros.AvgPrizeMoney,
				SireBrosAvgRating = sirebros.AvgRating,
				SireBrosDistanceDiff = sirebros.DistanceDiff,
				SireBrosAvgFinishPosition = sirebros.AvgFinishPosition,
				SireBrosAvgTime2Top = sirebros.AvgTime2Top,
				SireBrosAvgLastThreeFurlongs2Top = sirebros.AvgLastThreeFurlongs2Top,
				SireBrosAvgTime2Condition = sirebros.AvgTime2Condition,
				SireBrosAvgLastThreeFurlongs2Condition = sirebros.AvgLastThreeFurlongs2Condition,

				// 父馬-母父馬の兄弟馬の実績
				SireDamSireBrosAvgPrizeMoney = siredamsirebros.AvgPrizeMoney,
				SireDamSireBrosAvgRating = siredamsirebros.AvgRating,
				SireDamSireBrosDistanceDiff = siredamsirebros.DistanceDiff,
				SireDamSireBrosAvgFinishPosition = siredamsirebros.AvgFinishPosition,
				SireDamSireBrosAvgTime2Top = siredamsirebros.AvgTime2Top,
				SireDamSireBrosAvgLastThreeFurlongs2Top = siredamsirebros.AvgLastThreeFurlongs2Top,
				SireDamSireBrosAvgTime2Condition = siredamsirebros.AvgTime2Condition,
				SireDamSireBrosAvgLastThreeFurlongs2Condition = siredamsirebros.AvgLastThreeFurlongs2Condition,

				// 父馬-馬場の情報
				SireTrackAvgPrizeMoney = siretracks.AvgPrizeMoney,
				SireTrackAvgRating = siretracks.AvgRating,
				SireTrackAvgFinishPosition = siretracks.AvgFinishPosition,
				SireTrackAvgTime2Top = siretracks.AvgTime2Top,
				SireTrackAvgTime2Condition = siretracks.AvgTime2Condition,
				// 父馬-馬場状態の情報
				SireTrackConditionAvgPrizeMoney = siretrackconditions.AvgPrizeMoney,
				SireTrackConditionAvgRating = siretrackconditions.AvgRating,
				SireTrackConditionAvgFinishPosition = siretrackconditions.AvgFinishPosition,
				SireTrackConditionAvgTime2Top = siretrackconditions.AvgTime2Top,
				SireTrackConditionAvgTime2Condition = siretrackconditions.AvgTime2Condition,

				// 父馬-母父馬-馬場の情報
				SireDamSireTrackAvgPrizeMoney = siredamsiretracks.AvgPrizeMoney,
				SireDamSireTrackAvgRating = siredamsiretracks.AvgRating,
				SireDamSireTrackAvgFinishPosition = siredamsiretracks.AvgFinishPosition,
				SireDamSireTrackAvgTime2Top = siredamsiretracks.AvgTime2Top,
				SireDamSireTrackAvgTime2Condition = siredamsiretracks.AvgTime2Condition,
				// 父馬-母父馬-馬場状態の情報
				SireDamSireTrackConditionAvgPrizeMoney = siredamsiretrackconditions.AvgPrizeMoney,
				SireDamSireTrackConditionAvgRating = siredamsiretrackconditions.AvgRating,
				SireDamSireTrackConditionAvgFinishPosition = siredamsiretrackconditions.AvgFinishPosition,
				SireDamSireTrackConditionAvgTime2Top = siredamsiretrackconditions.AvgTime2Top,
				SireDamSireTrackConditionAvgTime2Condition = siredamsiretrackconditions.AvgTime2Condition,

				// 父馬-距離の情報
				SireDistanceAvgPrizeMoney = siredistances.AvgPrizeMoney,
				SireDistanceAvgRating = siredistances.AvgRating,
				SireDistanceAvgFinishPosition = siredistances.AvgFinishPosition,
				SireDistanceAvgTime2Top = siredistances.AvgTime2Top,
				SireDistanceAvgTime2Condition = siredistances.AvgTime2Condition,

				// 父馬-母父馬-距離の情報
				SireDamSireDistanceAvgPrizeMoney = siredamsiredistances.AvgPrizeMoney,
				SireDamSireDistanceAvgRating = siredamsiredistances.AvgRating,
				SireDamSireDistanceAvgFinishPosition = siredamsiredistances.AvgFinishPosition,
				SireDamSireDistanceAvgTime2Top = siredamsiredistances.AvgTime2Top,
				SireDamSireDistanceAvgTime2Condition = siredamsiredistances.AvgTime2Condition,

				// 騎手の情報
				JockeyAvgPrizeMoney = jockeys.AvgPrizeMoney,
				JockeyAvgRating = jockeys.AvgRating,
				JockeyAvgFinishPosition = jockeys.AvgFinishPosition,
				JockeyAvgTime2Top = jockeys.AvgTime2Top,
				JockeyAvgTime2Condition = jockeys.AvgTime2Condition,

				// 騎手-場所相性の情報
				JockeyPlaceAvgPrizeMoney = jockeyplaces.AvgPrizeMoney,
				JockeyPlaceAvgRating = jockeyplaces.AvgRating,
				JockeyPlaceAvgFinishPosition = jockeyplaces.AvgFinishPosition,
				JockeyPlaceAvgTime2Top = jockeyplaces.AvgTime2Top,
				JockeyPlaceAvgTime2Condition = jockeyplaces.AvgTime2Condition,

				// 騎手-馬場相性の情報
				JockeyTrackAvgPrizeMoney = jockeytracks.AvgPrizeMoney,
				JockeyTrackAvgRating = jockeytracks.AvgRating,
				JockeyTrackAvgFinishPosition = jockeytracks.AvgFinishPosition,
				JockeyTrackAvgTime2Top = jockeytracks.AvgTime2Top,
				JockeyTrackAvgTime2Condition = jockeytracks.AvgTime2Condition,
				// 騎手-馬場状態の情報
				JockeyTrackConditionAvgPrizeMoney = jockeytrackconditions.AvgPrizeMoney,
				JockeyTrackConditionAvgRating = jockeytrackconditions.AvgRating,
				JockeyTrackConditionAvgFinishPosition = jockeytrackconditions.AvgFinishPosition,
				JockeyTrackConditionAvgTime2Top = jockeytrackconditions.AvgTime2Top,
				JockeyTrackConditionAvgTime2Condition = jockeytrackconditions.AvgTime2Condition,

				// 騎手-距離の情報
				JockeyDistanceAvgPrizeMoney = jockeydistances.AvgPrizeMoney,
				JockeyDistanceAvgRating = jockeydistances.AvgRating,
				JockeyDistanceAvgFinishPosition = jockeydistances.AvgFinishPosition,
				JockeyDistanceAvgTime2Top = jockeydistances.AvgTime2Top,
				JockeyDistanceAvgTime2Condition = jockeydistances.AvgTime2Condition,

				// 騎手-調教師相性の情報
				JockeyTrainerAvgPrizeMoney = jockeytrainers.AvgPrizeMoney,
				JockeyTrainerAvgRating = jockeytrainers.AvgRating,
				JockeyTrainerAvgFinishPosition = jockeytrainers.AvgFinishPosition,
				JockeyTrainerAvgTime2Top = jockeytrainers.AvgTime2Top,
				JockeyTrainerAvgTime2Condition = jockeytrainers.AvgTime2Condition,

				// 調教師の情報
				TrainerAvgPrizeMoney = trainers.AvgPrizeMoney,
				TrainerAvgRating = trainers.AvgRating,
				TrainerAvgFinishPosition = trainers.AvgFinishPosition,
				TrainerAvgTime2Top = trainers.AvgTime2Top,
				TrainerAvgTime2Condition = trainers.AvgTime2Condition,

				// 調教師-場所の情報
				TrainerPlaceAvgPrizeMoney = trainerplaces.AvgPrizeMoney,
				TrainerPlaceAvgRating = trainerplaces.AvgRating,
				TrainerPlaceAvgFinishPosition = trainerplaces.AvgFinishPosition,
				TrainerPlaceAvgTime2Top = trainerplaces.AvgTime2Top,
				TrainerPlaceAvgTime2Condition = trainerplaces.AvgTime2Condition,

				// 生産者の情報
				BreederAvgPrizeMoney = breeders.AvgPrizeMoney,
				BreederAvgRating = breeders.AvgRating,
				BreederAvgFinishPosition = breeders.AvgFinishPosition,
				BreederAvgTime2Top = breeders.AvgTime2Top,
				BreederAvgTime2Condition = breeders.AvgTime2Condition,

				// 調教師-生産者の情報
				TrainerBreederAvgPrizeMoney = trainerbreeders.AvgPrizeMoney,
				TrainerBreederAvgRating = trainerbreeders.AvgRating,
				TrainerBreederAvgFinishPosition = trainerbreeders.AvgFinishPosition,
				TrainerBreederAvgTime2Top = trainerbreeders.AvgTime2Top,
				TrainerBreederAvgTime2Condition = trainerbreeders.AvgTime2Condition,

				// 交差特徴量
				CrossOikiriFinishPos = (detail.Oikiri.Rating / 20F) * horses5.AvgFinishPosition,
				CrossOikiriTime2Top = (detail.Oikiri.Rating / 20F) * ((5F - horses5.AvgTime2Top) / 5F),
				CrossDistanceFitFinishPos = (1F / (1F + Math.Abs(horses5.DistanceDiff) / 200F)) * horses5.AvgFinishPosition,
				CrossRestDaysFinishPos = restDaysFactor * horses5.AvgFinishPosition,
				CrossRatingOikiri = Math.Min(horses5.AvgRating / 100F, 1F) * (detail.Oikiri.Rating / 20F),
				CrossOikiriJockey = (detail.Oikiri.Rating / 20F) * jockeys.AvgFinishPosition,

			};

			return features;
		}

		private static float CalculateBestAge(RaceDetail detail)
		{
			return 4.1F + (5.5F - 4.1F) / (3600F - 1000F) * (detail.Race.Distance - 1000F);
		}

		private static float CalculateFinishPosition(RaceDetail detail)
		{
			var tmp = detail.FinishDiff switch
			{
				"ハナ" => 0.75F,
				"アタマ" => 0.5F,
				"クビ" => 0.25F,
				_ => 0.00F
			};

			return 1F / (detail.FinishPosition - tmp);
		}

		private static HorseScoreMetrics GetHorseScoreMetrics(this RaceDetail detail, List<RaceDetail> horses)
		{
			return new HorseScoreMetrics()
			{
				// 獲得賞金
				AvgPrizeMoney = horses.Median(x => x.PrizeMoney, float.NaN),
				MaxPrizeMoney = horses.Max(x => x.PrizeMoney, float.NaN),

				// ﾚｰﾃｨﾝｸﾞ
				AvgRating = horses.Median(x => x.TimeIndex, DefaultRating),
				MaxRating = horses.Max(x => x.TimeIndex, DefaultRating),

				// 距離
				DistanceDiff = detail.Race.Distance - horses.Median(x => x.Race.Distance, float.NaN),

				// 通過順
				Tuka = horses.Median(x => x.Tuka, float.NaN),

				// 着順
				AvgFinishPosition = horses.Median(CalculateFinishPosition, DefaultFinishPosition),
				MaxFinishPosition = horses.Max(CalculateFinishPosition, DefaultFinishPosition),

				// ﾄｯﾌﾟとのﾀｲﾑ差
				AvgTime2Top = horses.Median(x => x.Time2Top, DefaultTime2Top),
				MaxTime2Top = horses.Min(x => x.Time2Top, DefaultTime2Top),

				// ﾄｯﾌﾟとの3ﾊﾛﾝ差
				AvgLastThreeFurlongs2Top = horses.Median(x => x.LastThreeFurlongs2Top, DefaultLastThreeFurlongs2Top),
				MaxLastThreeFurlongs2Top = horses.Min(x => x.LastThreeFurlongs2Top, DefaultLastThreeFurlongs2Top),

				// 同条件ﾚｰｽとのﾀｲﾑ差
				AvgTime2Condition = horses.Median(x => x.Time2Avg, DefaultTime2Top),
				MaxTime2Condition = horses.Min(x => x.Time2Avg, DefaultTime2Top),

				// 同条件ﾚｰｽとの3ﾊﾛﾝ差
				AvgLastThreeFurlongs2Condition = horses.Median(x => x.LastThreeFurlongs2Avg, DefaultLastThreeFurlongs2Top),
				MaxLastThreeFurlongs2Condition = horses.Min(x => x.LastThreeFurlongs2Avg, DefaultLastThreeFurlongs2Top),
			};
		}

		private static ConnectionScoreMetrics GetConnectionScoreMetrics(this RaceDetail detail, List<RaceDetail> horses)
		{
			return new ConnectionScoreMetrics()
			{
				// 獲得賞金
				AvgPrizeMoney = horses.Median(x => x.PrizeMoney, float.NaN),

				// ﾚｰﾃｨﾝｸﾞ
				AvgRating = horses.Median(x => x.TimeIndex, DefaultRating),

				// 着順
				AvgFinishPosition = horses.Median(CalculateFinishPosition, DefaultFinishPosition),

				// ﾄｯﾌﾟとのﾀｲﾑ差
				AvgTime2Top = horses.Median(x => x.Time2Top, DefaultTime2Top),

				// 同条件ﾚｰｽとのﾀｲﾑ差
				AvgTime2Condition = horses.Median(x => x.Time2Avg, DefaultTime2Top),
			};
		}
	}
}
