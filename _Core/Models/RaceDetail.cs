using Codeplex.Data;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class RaceDetail
	{
		public RaceDetail(Dictionary<string, object> x, Race race)
		{
			try
			{
				Race = race;
				Oikiri = new Oikiri(x, this);
				Wakuban = x.Get("枠番").Int32();
				Umaban = x.Get("馬番").Int32();
				Horse = x.Get("馬ID").Str();
				Jockey = x.Get("騎手ID").Str();
				Trainer = x.Get("調教師ID").Str();
				Sire = x.Get("父ID").Str();
				DamSire = x.Get("母父ID").Str();
				Breeder = x.Get("生産者ID").Str();
				FinishPosition = (uint)x.Get("着順").Int32();
				FinishDiff = x.Get("着差").Str();
				Time = x.Get("ﾀｲﾑ変換").Single();
				Weight = x.Get("体重").Single();
				WeightChange = x.Get("増減").Single();
				PrizeMoney = x.Get("賞金").Single();
				PurchasePrice = x.Get("評価額").Single();
				BirthDate = x.Get("生年月日").Date();
				JockeyWeight = x.Get("斤量").Single();
				// 通過順＝0:前方～1:後方
				Tuka = x.Get("通過").Str()
					// ﾊｲﾌﾝ区切り
					.Run(y => y.Split('-'))
					.Run(y => y.Length switch
					{
						// ﾃﾞｰﾀがない＝出走頭数の半分
						0 => (Race.NumberOfHorses / 2).Str(),
						// 短距離＝最後のﾊﾛﾝ
						1 or 2 => y.Last(),
						// その他＝1つ手前のﾊﾛﾝ
						_ => y[y.Length - 2]
					})
					.Run(y => (object)y)
					.Single() / (float)Race.NumberOfHorses;
				Tuka = Math.Min(Tuka, 1.0F);
				LastThreeFurlongs = GetLastThreeFurlongs(x.Get("上り").Single());
				Gender = x.Get("馬性").Str();
				Age = (Race.RaceDate - BirthDate).TotalDays.Single() / 365F;
				TimeIndex = x.Get("ﾀｲﾑ指数").Single().MinMax(40F, 135F);
			}
			catch (Exception ex)
			{
				MessageService.Debug(ex.ToString());
				throw;
			}
		}

		// 当日取得できる情報
		public Race Race { get; }
		public Oikiri Oikiri { get; }
		public string RaceId => Race.RaceId;
		public int Wakuban { get; }
		public int Umaban { get; }
		public string Horse { get; }
		public string Jockey { get; }
		public string Trainer { get; }
		public string Sire { get; }
		public string DamSire { get; }
		public string Breeder { get; }
		public float PurchasePrice { get; }
		public DateTime BirthDate { get; }
		public float Age { get; }
		public float Weight { get; private set; }
		public float WeightChange { get; private set; }
		public float JockeyWeight { get; }
		public string Gender { get; set; }

		// これまでの実績に紐づく情報
		public int RaceCount { get; private set; }
		public float AverageRating { get; private set; }

		// ﾚｰｽ結果
		public float Tuka { get; }
		public float LastThreeFurlongs { get; }
		public float PrizeMoney { get; }
		public uint FinishPosition { get; }
		public string FinishDiff { get; private set; }
		public float Time { get; }
		public float AdjustedTime => Time + (JockeyWeight - 55f) * 0.2f;
		public float TimeIndex { get; }
		public RaceDetail? Last { get; private set; }
		public DateTime LastRaceDate { get; private set; }

		// ﾚｰｽ結果(ﾄｯﾌﾟとの差)
		public float Time2Top { get; private set; }
		public float LastThreeFurlongs2Top { get; private set; }

		// ﾚｰｽ結果(ﾚｰｽ平均との差)
		public float Time2Avg { get; private set; }
		public float LastThreeFurlongs2Avg { get; private set; }

		private float GetLastThreeFurlongs(float basevalue)
		{
			//return basevalue;
			return Race.TrackType == TrackType.Grass
				? basevalue * (0.94F + Race.Distance / 20000F)
				: Race.TrackType == TrackType.Dirt
				? basevalue * (1.01F + Race.Distance / 20000F)
				: basevalue * (0.36F + Race.Distance * 1.5F / 100000F);
		}

		public void SetHistoricalData(List<RaceDetail> horses, RaceDetail[] inraces, TrackConditionDistance tcd)
		{
			RaceCount = horses.Count;

			// ﾚｰｽ結果を元に計算
			Time2Top = (Time - inraces.Min(x => x.Time)).MinMax(-4.9F, 4.9F);
			LastThreeFurlongs2Top = (LastThreeFurlongs - inraces.Min(x => x.LastThreeFurlongs)).MinMax(-4.9F, 4.9F);
			FinishDiff = FinishDiff == "同着" ? inraces.First(x => x.FinishPosition == FinishPosition && x.Umaban != Umaban).FinishDiff : FinishDiff;

			// ﾚｰｽ平均を元に計算
			Time2Avg = (Time - tcd.Time).MinMax(-4.9F, 4.9F);
			LastThreeFurlongs2Avg = (LastThreeFurlongs - tcd.LastThreeFurlongs).MinMax(-4.9F, 4.9F);

			// 直近の情報から計算
			Last = horses.FirstOrDefault();
			Weight = Weight > 300F ? Weight : Last != null ? Last.Weight : 460F;
			LastRaceDate = Last != null ? Last.Race.RaceDate : Race.RaceDate.AddDays(-60);

			// 直近5戦の情報
			AverageRating = horses.Median(x => x.TimeIndex, 60F);
		}
	}

	public static class RaceDetailExtensions
	{
		private static float DefaultFinishPosition = float.NaN;
		private static float DefaultAdjustedScore = float.NaN;
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
			// 父馬・父馬母父馬-距離情報
			var siredistances = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireDistances(detail));
			var siredamsiredistances = GetConnectionScoreMetrics(detail, PreviousDataSets.GetSireDamSireDistances(detail));
			// 関係者情報
			var jockeys = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeys(detail));
			var jockeyplaces = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyPlaces(detail));
			var jockeytracks = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyTracks(detail));
			var jockeydistances = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyDistances(detail));
			var jockeytrainers = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyTrainers(detail));
			var trainers = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainers(detail));
			var trainerplaces = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainerPlaces(detail));
			var breeders = GetConnectionScoreMetrics(detail, PreviousDataSets.GetBreeders(detail));
			var trainerbreeders = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainerBreeders(detail));

			// 休養日数とベル型変換（交差特徴量で使用）
			var restDays = Math.Min((float)(detail.Race.RaceDate - detail.LastRaceDate).Days, 365F);
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
				// 年齢ﾗﾝｸ(5歳に近い方が良い)
				AgeRank = detail.GetRank(inraces, x => Math.Abs(CalculateBestAge(x) - x.Age), true),
				// 性別(牡：1.0, セ：0.5, 牝：0.0)
				Gender = detail.Gender switch
				{
					"牝" => 0.0F,
					"セ" => 0.5F,
					_ => 1.0F
				},
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

				// 通過順
				Tuka = horses5.Tuka,

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
				//SireBrosTuka = sirebros.Tuka,
				SireBrosAvgFinishPosition = sirebros.AvgFinishPosition,
				SireBrosAvgTime2Top = sirebros.AvgTime2Top,
				SireBrosAvgLastThreeFurlongs2Top = sirebros.AvgLastThreeFurlongs2Top,
				SireBrosAvgTime2Condition = sirebros.AvgTime2Condition,
				SireBrosAvgLastThreeFurlongs2Condition = sirebros.AvgLastThreeFurlongs2Condition,

				// 父馬-母父馬の兄弟馬の実績
				SireDamSireBrosAvgPrizeMoney = siredamsirebros.AvgPrizeMoney,
				SireDamSireBrosAvgRating = siredamsirebros.AvgRating,
				SireDamSireBrosDistanceDiff = siredamsirebros.DistanceDiff,
				//SireDamSireBrosTuka = siredamsirebros.Tuka,
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

				// 父馬-母父馬-馬場の情報
				SireDamSireTrackAvgPrizeMoney = siredamsiretracks.AvgPrizeMoney,
				SireDamSireTrackAvgRating = siredamsiretracks.AvgRating,
				SireDamSireTrackAvgFinishPosition = siredamsiretracks.AvgFinishPosition,
				SireDamSireTrackAvgTime2Top = siredamsiretracks.AvgTime2Top,
				SireDamSireTrackAvgTime2Condition = siredamsiretracks.AvgTime2Condition,

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

		public static OptimizedHorseFeatures[] CalculateInRaces(this IEnumerable<OptimizedHorseFeatures> features)
		{
			var results = features.ToArray();

			var pace = results.Average(x => x.Tuka);

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

				// 脚質による有利不利(同じ脚質が少ないほど有利)
				x.PaceAdvantage = 1F - Math.Abs(pace - x.Tuka);

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

		private static float CalculateBestAge(RaceDetail detail)
		{
			return (5.5F - 4.1F) / (3600F - 1000F) * detail.Race.Distance;
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

		private static float CalculateAdjustedScore(RaceDetail x)
		{
			var score = 0.0F;

			score += CalculateFinishPosition(x) * 0.2F;

			score += (4.9F - x.Time2Avg) / 9.8F * 0.2F;

			score += (4.9F - x.LastThreeFurlongs2Avg) / 9.8F * 0.2F;

			score += x.Race.Grade.GetGradeFeatures() * 0.15F;

			score += (x.Race.AverageRating - 40F).MinMax(0F, 95F) / 95F * 0.15F;

			score += (x.Race.NumberOfHorses.Single().MinMax(5F, 18F) - 5F) / 13F * 0.1F;

			return score;
		}

		private static HorseScoreMetrics GetHorseScoreMetrics(this RaceDetail detail, List<RaceDetail> horses)
		{
			return new HorseScoreMetrics()
			{
				// 獲得賞金
				AvgPrizeMoney = horses.Median(x => x.PrizeMoney, 0F),
				MaxPrizeMoney = horses.Max(x => x.PrizeMoney, 0F),

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

				// 調整ｽｺｱ
				AvgAdjustedScore = horses.Median(CalculateAdjustedScore, DefaultAdjustedScore),
				MaxAdjustedScore = horses.Max(CalculateAdjustedScore, DefaultAdjustedScore),

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
				AvgPrizeMoney = horses.Median(x => x.PrizeMoney, 0F),

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