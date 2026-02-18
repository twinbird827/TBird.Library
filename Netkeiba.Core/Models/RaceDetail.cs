using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
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
				TimeIndex = x.Get("ﾀｲﾑ指数").Single().Run(x => Math.Max(x, 40F));
				//RaceCount = x.Get("出走数").Int32();
				//AverageRating = x.Get("レーティング").Single();
				//LastRaceDate = x.Get("前回出走日").Date();
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
		public string SireDamSire => $"{Sire}-{DamSire}";
		public string JockeyTrainer => $"{Jockey}-{Trainer}";
		public string Breeder { get; }
		public float PurchasePrice { get; }
		public DateTime BirthDate { get; }
		public float Age { get; }
		public float Weight { get; private set; }
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
			Time2Top = Math.Min(Time - inraces.Min(x => x.Time), 2.0F);
			LastThreeFurlongs2Top = Math.Min(LastThreeFurlongs - inraces.Min(x => x.LastThreeFurlongs), 4.9F);
			FinishDiff = FinishDiff == "同着" ? inraces.First(x => x.FinishPosition == FinishPosition && x.Umaban != Umaban).FinishDiff : FinishDiff;

			// ﾚｰｽ平均を元に計算
			Time2Avg = Math.Min(Time - tcd.Time, 2.0F);
			LastThreeFurlongs2Avg = Math.Min(LastThreeFurlongs - tcd.LastThreeFurlongs, 4.9F);

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
		private static float DefaultFinishPosition = 1F / 7F;
		private static float DefaultAdjustedScore = 0.20F;
		private static float DefaultRating = 60F;
		private static float DefaultGrade = GradeType.新馬ク.GetGradeFeatures();
		private static float DefaultTime2Top = 1.5F;
		private static float DefaultLastThreeFurlongs2Top = 2.5F;

		public static OptimizedHorseFeatures ExtractFeatures(this RaceDetail detail, RaceDetail[] inraces)
		{

			var horses = PreviousDataSets.GetHorses(detail);
			// 対象馬の情報
			var horses1 = GetHorseScoreMetrics(detail, horses.Take(1).ToList());
			var horses3 = GetHorseScoreMetrics(detail, horses.Take(3).ToList());
			var horses5 = GetHorseScoreMetrics(detail, horses.Take(5).ToList());
			// 血統情報
			var sires = GetHorseScoreMetrics(detail, PreviousDataSets.GetHorses(detail.Sire, detail));
			var damsires = GetHorseScoreMetrics(detail, PreviousDataSets.GetHorses(detail.DamSire, detail));
			// 兄弟馬
			var sirebros = GetHorseScoreMetrics(detail, PreviousDataSets.GetSires(detail));
			var damsirebros = GetHorseScoreMetrics(detail, PreviousDataSets.GetDamSires(detail));
			var siredamsirebros = GetHorseScoreMetrics(detail, PreviousDataSets.GetSireDamSires(detail));
			// 関係者情報
			var jockeys = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeys(detail));
			var jockeyplaces = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyPlaces(detail));
			var jockeytracks = GetConnectionScoreMetrics(detail, PreviousDataSets.GetJockeyTracks(detail));
			var trainers = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainers(detail));
			var breeders = GetConnectionScoreMetrics(detail, PreviousDataSets.GetBreeders(detail));
			var trainerbreeders = GetConnectionScoreMetrics(detail, PreviousDataSets.GetTrainerBreeders(detail));
			var restDays = Math.Min((detail.Race.RaceDate - detail.LastRaceDate).Days, 365F);
			var nearestRaces = horses.Count(x => x.Race.RaceDate > detail.Race.RaceDate.AddDays(-90));

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
				// 前走の間隔ｽﾃｰﾀｽ
				RestType = (restDays, horses.Count) switch
				{
					(_, 0) => 1,           // 初出走(8.22)
					( < 10, _) => 3,       // 連闘（7.91）
					( < 31, _) => 5,       // 通常ローテ（7.09-7.31）★良好
					( < 90, _) => 4,       // 中期休養（7.88-8.00）
					( < 175, _) => 2,      // 長期休養（8.02-8.70）
					_ => 0                 // 超長期休養（9.41）★要注意
				} / 5F,
				// 直近のﾚｰｽ数
				NearestRaces = nearestRaces switch
				{
					0 => 1,
					1 => 2,
					2 => 3,
					3 => 4,
					4 => 4,
					5 => 4,
					6 => 3,
					_ => 0
				} / 4F,
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
				// 購入金額ﾗﾝｸ
				PurchasePriceRank = detail.GetRank(inraces, x => x.PurchasePrice, true),
				// 斤量ﾗﾝｸ(軽い方が良い)
				JockeyWeightRank = detail.GetRank(inraces, x => x.JockeyWeight, false),
				// 体重ﾗﾝｸ(重い方が良い)
				WeightRank = detail.GetRank(inraces, x => x.Weight, true),

				// 獲得賞金
				AvgPrizeMoney = horses5.AvgPrizeMoney,
				MaxPrizeMoney = horses5.MaxPrizeMoney,

				//// ﾚｰﾃｨﾝｸﾞ
				//AvgRating = horses5.AvgRating,
				//MaxRating = horses5.MaxRating,

				//// ｸﾞﾚｰﾄﾞ
				//AvgGrade = horses5.AvgGrade,
				//MaxGrade = horses5.MaxGrade,

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
				AvgFinishPosition1 = horses1.AvgFinishPosition,
				AvgFinishPosition3 = horses3.AvgFinishPosition,
				MaxFinishPosition3 = horses3.MaxFinishPosition,
				AvgFinishPosition5 = horses5.AvgFinishPosition,
				MaxFinishPosition5 = horses5.MaxFinishPosition,

				// 着順
				AvgAdjustedScore1 = horses1.AvgAdjustedScore,
				AvgAdjustedScore3 = horses3.AvgAdjustedScore,
				MaxAdjustedScore3 = horses3.MaxAdjustedScore,
				AvgAdjustedScore5 = horses5.AvgAdjustedScore,
				MaxAdjustedScore5 = horses5.MaxAdjustedScore,

				// ﾄｯﾌﾟとのﾀｲﾑ差
				AvgTime2Top1 = horses1.AvgTime2Top,
				AvgTime2Top3 = horses3.AvgTime2Top,
				MaxTime2Top3 = horses3.MaxTime2Top,
				AvgTime2Top5 = horses5.AvgTime2Top,
				MaxTime2Top5 = horses5.MaxTime2Top,

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

				// 同条件ﾚｰｽとの3ﾊﾛﾝ差
				AvgLastThreeFurlongs2Condition1 = horses1.AvgLastThreeFurlongs2Condition,
				AvgLastThreeFurlongs2Condition3 = horses3.AvgLastThreeFurlongs2Condition,
				MaxLastThreeFurlongs2Condition3 = horses3.MaxLastThreeFurlongs2Condition,
				AvgLastThreeFurlongs2Condition5 = horses5.AvgLastThreeFurlongs2Condition,
				MaxLastThreeFurlongs2Condition5 = horses5.MaxLastThreeFurlongs2Condition,

				//// 父馬の実績
				//SireAvgPrizeMoney = sires.AvgPrizeMoney,
				//SireMaxPrizeMoney = sires.MaxPrizeMoney,
				//SireAvgRating = sires.AvgRating,
				//SireMaxRating = sires.MaxRating,
				//SireAvgGrade = sires.AvgGrade,
				//SireMaxGrade = sires.MaxGrade,
				//SireDistanceDiff = sires.DistanceDiff,
				////SireTuka = sires.Tuka,
				//SireAvgFinishPosition = sires.AvgFinishPosition,
				//SireMaxFinishPosition = sires.MaxFinishPosition,
				//SireAvgAdjustedScore = sires.AvgAdjustedScore,
				//SireMaxAdjustedScore = sires.MaxAdjustedScore,
				//SireAvgTime2Top = sires.AvgTime2Top,
				//SireMaxTime2Top = sires.MaxTime2Top,
				//SireAvgLastThreeFurlongs2Top = sires.AvgLastThreeFurlongs2Top,
				//SireMaxLastThreeFurlongs2Top = sires.MaxLastThreeFurlongs2Top,
				//SireAvgTime2Condition = sires.AvgTime2Condition,
				//SireMaxTime2Condition = sires.MaxTime2Condition,
				//SireAvgLastThreeFurlongs2Condition = sires.AvgLastThreeFurlongs2Condition,
				//SireMaxLastThreeFurlongs2Condition = sires.MaxLastThreeFurlongs2Condition,

				//// 母父馬の実績
				//DamSireAvgPrizeMoney = damsires.AvgPrizeMoney,
				//DamSireMaxPrizeMoney = damsires.MaxPrizeMoney,
				//DamSireAvgRating = damsires.AvgRating,
				//DamSireMaxRating = damsires.MaxRating,
				//DamSireAvgGrade = damsires.AvgGrade,
				//DamSireMaxGrade = damsires.MaxGrade,
				//DamSireDistanceDiff = damsires.DistanceDiff,
				////DamSireTuka = damsires.Tuka,
				//DamSireAvgFinishPosition = damsires.AvgFinishPosition,
				//DamSireMaxFinishPosition = damsires.MaxFinishPosition,
				//DamSireAvgAdjustedScore = damsires.AvgAdjustedScore,
				//DamSireMaxAdjustedScore = damsires.MaxAdjustedScore,
				//DamSireAvgTime2Top = damsires.AvgTime2Top,
				//DamSireMaxTime2Top = damsires.MaxTime2Top,
				//DamSireAvgLastThreeFurlongs2Top = damsires.AvgLastThreeFurlongs2Top,
				//DamSireMaxLastThreeFurlongs2Top = damsires.MaxLastThreeFurlongs2Top,
				//DamSireAvgTime2Condition = damsires.AvgTime2Condition,
				//DamSireMaxTime2Condition = damsires.MaxTime2Condition,
				//DamSireAvgLastThreeFurlongs2Condition = damsires.AvgLastThreeFurlongs2Condition,
				//DamSireMaxLastThreeFurlongs2Condition = damsires.MaxLastThreeFurlongs2Condition,

				// 父馬の兄弟馬の実績
				SireBrosAvgPrizeMoney = sirebros.AvgPrizeMoney,
				SireBrosMaxPrizeMoney = sirebros.MaxPrizeMoney,
				//SireBrosAvgRating = sirebros.AvgRating,
				//SireBrosMaxRating = sirebros.MaxRating,
				//SireBrosAvgGrade = sirebros.AvgGrade,
				//SireBrosMaxGrade = sirebros.MaxGrade,
				SireBrosDistanceDiff = sirebros.DistanceDiff,
				//SireBrosTuka = sirebros.Tuka,
				SireBrosAvgFinishPosition = sirebros.AvgFinishPosition,
				//SireBrosMaxFinishPosition = sirebros.MaxFinishPosition,
				SireBrosAvgAdjustedScore = sirebros.AvgAdjustedScore,
				SireBrosMaxAdjustedScore = sirebros.MaxAdjustedScore,
				SireBrosAvgTime2Top = sirebros.AvgTime2Top,
				//SireBrosMaxTime2Top = sirebros.MaxTime2Top,
				SireBrosAvgLastThreeFurlongs2Top = sirebros.AvgLastThreeFurlongs2Top,
				//SireBrosMaxLastThreeFurlongs2Top = sirebros.MaxLastThreeFurlongs2Top,
				SireBrosAvgTime2Condition = sirebros.AvgTime2Condition,
				//SireBrosMaxTime2Condition = sirebros.MaxTime2Condition,
				SireBrosAvgLastThreeFurlongs2Condition = sirebros.AvgLastThreeFurlongs2Condition,
				//SireBrosMaxLastThreeFurlongs2Condition = sirebros.MaxLastThreeFurlongs2Condition,

				// 母父馬の兄弟馬の実績
				//DamSireBrosAvgPrizeMoney = damsires.AvgPrizeMoney,
				//DamSireBrosMaxPrizeMoney = damsires.MaxPrizeMoney,
				//DamSireBrosAvgRating = damsires.AvgRating,
				//DamSireBrosMaxRating = damsires.MaxRating,
				//DamSireBrosAvgGrade = damsires.AvgGrade,
				//DamSireBrosMaxGrade = damsires.MaxGrade,
				DamSireBrosDistanceDiff = damsires.DistanceDiff,
				//DamSireBrosTuka = damsires.Tuka,
				DamSireBrosAvgFinishPosition = damsires.AvgFinishPosition,
				//DamSireBrosMaxFinishPosition = damsires.MaxFinishPosition,
				DamSireBrosAvgAdjustedScore = damsires.AvgAdjustedScore,
				//DamSireBrosMaxAdjustedScore = damsires.MaxAdjustedScore,
				//DamSireBrosAvgTime2Top = damsires.AvgTime2Top,
				//DamSireBrosMaxTime2Top = damsires.MaxTime2Top,
				//DamSireBrosAvgLastThreeFurlongs2Top = damsires.AvgLastThreeFurlongs2Top,
				//DamSireBrosMaxLastThreeFurlongs2Top = damsires.MaxLastThreeFurlongs2Top,
				//DamSireBrosAvgTime2Condition = damsires.AvgTime2Condition,
				//DamSireBrosMaxTime2Condition = damsires.MaxTime2Condition,
				//DamSireBrosAvgLastThreeFurlongs2Condition = damsires.AvgLastThreeFurlongs2Condition,
				//DamSireBrosMaxLastThreeFurlongs2Condition = damsires.MaxLastThreeFurlongs2Condition,

				// 父馬-母父馬の兄弟馬の実績
				SireDamSireBrosAvgPrizeMoney = siredamsirebros.AvgPrizeMoney,
				SireDamSireBrosMaxPrizeMoney = siredamsirebros.MaxPrizeMoney,
				//SireDamSireBrosAvgRating = siredamsirebros.AvgRating,
				//SireDamSireBrosMaxRating = siredamsirebros.MaxRating,
				//SireDamSireBrosAvgGrade = siredamsirebros.AvgGrade,
				//SireDamSireBrosMaxGrade = siredamsirebros.MaxGrade,
				SireDamSireBrosDistanceDiff = siredamsirebros.DistanceDiff,
				//SireDamSireBrosTuka = siredamsirebros.Tuka,
				SireDamSireBrosAvgFinishPosition = siredamsirebros.AvgFinishPosition,
				//SireDamSireBrosMaxFinishPosition = siredamsirebros.MaxFinishPosition,
				SireDamSireBrosAvgAdjustedScore = siredamsirebros.AvgAdjustedScore,
				SireDamSireBrosMaxAdjustedScore = siredamsirebros.MaxAdjustedScore,
				SireDamSireBrosAvgTime2Top = siredamsirebros.AvgTime2Top,
				//SireDamSireBrosMaxTime2Top = siredamsirebros.MaxTime2Top,
				SireDamSireBrosAvgLastThreeFurlongs2Top = siredamsirebros.AvgLastThreeFurlongs2Top,
				//SireDamSireBrosMaxLastThreeFurlongs2Top = siredamsirebros.MaxLastThreeFurlongs2Top,
				SireDamSireBrosAvgTime2Condition = siredamsirebros.AvgTime2Condition,
				//SireDamSireBrosMaxTime2Condition = siredamsirebros.MaxTime2Condition,
				SireDamSireBrosAvgLastThreeFurlongs2Condition = siredamsirebros.AvgLastThreeFurlongs2Condition,
				//SireDamSireBrosMaxLastThreeFurlongs2Condition = siredamsirebros.MaxLastThreeFurlongs2Condition,

				// 騎手の情報
				JockeyAvgPrizeMoney = jockeys.AvgPrizeMoney,
				JockeyMaxPrizeMoney = jockeys.MaxPrizeMoney,
				//JockeyAvgRating = jockeys.AvgRating,
				//JockeyMaxRating = jockeys.MaxRating,
				//JockeyAvgGrade = jockeys.AvgGrade,
				//JockeyMaxGrade = jockeys.MaxGrade,
				JockeyAvgFinishPosition = jockeys.AvgFinishPosition,
				//JockeyMaxFinishPosition = jockeys.MaxFinishPosition,
				JockeyAvgAdjustedScore = jockeys.AvgAdjustedScore,
				JockeyMaxAdjustedScore = jockeys.MaxAdjustedScore,
				JockeyAvgTime2Top = jockeys.AvgTime2Top,
				//JockeyMaxTime2Top = jockeys.MaxTime2Top,
				JockeyAvgTime2Condition = jockeys.AvgTime2Condition,
				//JockeyMaxTime2Condition = jockeys.MaxTime2Condition,

				// 騎手-場所相性の情報
				JockeyPlaceAvgPrizeMoney = jockeyplaces.AvgPrizeMoney,
				JockeyPlaceMaxPrizeMoney = jockeyplaces.MaxPrizeMoney,
				//JockeyPlaceAvgRating = jockeyplaces.AvgRating,
				//JockeyPlaceMaxRating = jockeyplaces.MaxRating,
				//JockeyPlaceAvgGrade = jockeyplaces.AvgGrade,
				//JockeyPlaceMaxGrade = jockeyplaces.MaxGrade,
				JockeyPlaceAvgFinishPosition = jockeyplaces.AvgFinishPosition,
				//JockeyPlaceMaxFinishPosition = jockeyplaces.MaxFinishPosition,
				JockeyPlaceAvgAdjustedScore = jockeyplaces.AvgAdjustedScore,
				JockeyPlaceMaxAdjustedScore = jockeyplaces.MaxAdjustedScore,
				JockeyPlaceAvgTime2Top = jockeyplaces.AvgTime2Top,
				//JockeyPlaceMaxTime2Top = jockeyplaces.MaxTime2Top,
				JockeyPlaceAvgTime2Condition = jockeyplaces.AvgTime2Condition,
				//JockeyPlaceMaxTime2Condition = jockeyplaces.MaxTime2Condition,

				// 騎手-馬場相性の情報
				JockeyTrackAvgPrizeMoney = jockeytracks.AvgPrizeMoney,
				JockeyTrackMaxPrizeMoney = jockeytracks.MaxPrizeMoney,
				//JockeyTrackAvgRating = jockeytracks.AvgRating,
				//JockeyTrackMaxRating = jockeytracks.MaxRating,
				//JockeyTrackAvgGrade = jockeytracks.AvgGrade,
				//JockeyTrackMaxGrade = jockeytracks.MaxGrade,
				JockeyTrackAvgFinishPosition = jockeytracks.AvgFinishPosition,
				//JockeyTrackMaxFinishPosition = jockeytracks.MaxFinishPosition,
				JockeyTrackAvgAdjustedScore = jockeytracks.AvgAdjustedScore,
				JockeyTrackMaxAdjustedScore = jockeytracks.MaxAdjustedScore,
				JockeyTrackAvgTime2Top = jockeytracks.AvgTime2Top,
				//JockeyTrackMaxTime2Top = jockeytracks.MaxTime2Top,
				JockeyTrackAvgTime2Condition = jockeytracks.AvgTime2Condition,
				//JockeyTrackMaxTime2Condition = jockeytracks.MaxTime2Condition,

				// 調教師の情報
				TrainerAvgPrizeMoney = trainers.AvgPrizeMoney,
				TrainerMaxPrizeMoney = trainers.MaxPrizeMoney,
				//TrainerAvgRating = trainers.AvgRating,
				//TrainerMaxRating = trainers.MaxRating,
				//TrainerAvgGrade = trainers.AvgGrade,
				//TrainerMaxGrade = trainers.MaxGrade,
				TrainerAvgFinishPosition = trainers.AvgFinishPosition,
				//TrainerMaxFinishPosition = trainers.MaxFinishPosition,
				TrainerAvgAdjustedScore = trainers.AvgAdjustedScore,
				TrainerMaxAdjustedScore = trainers.MaxAdjustedScore,
				TrainerAvgTime2Top = trainers.AvgTime2Top,
				//TrainerMaxTime2Top = trainers.MaxTime2Top,
				TrainerAvgTime2Condition = trainers.AvgTime2Condition,
				//TrainerMaxTime2Condition = trainers.MaxTime2Condition,

				// 生産者の情報
				BreederAvgPrizeMoney = breeders.AvgPrizeMoney,
				BreederMaxPrizeMoney = breeders.MaxPrizeMoney,
				//BreederAvgRating = breeders.AvgRating,
				//BreederMaxRating = breeders.MaxRating,
				//BreederAvgGrade = breeders.AvgGrade,
				//BreederMaxGrade = breeders.MaxGrade,
				BreederAvgFinishPosition = breeders.AvgFinishPosition,
				//BreederMaxFinishPosition = breeders.MaxFinishPosition,
				BreederAvgAdjustedScore = breeders.AvgAdjustedScore,
				BreederMaxAdjustedScore = breeders.MaxAdjustedScore,
				BreederAvgTime2Top = breeders.AvgTime2Top,
				//BreederMaxTime2Top = breeders.MaxTime2Top,
				BreederAvgTime2Condition = breeders.AvgTime2Condition,
				//BreederMaxTime2Condition = breeders.MaxTime2Condition,

				// 調教師-生産者の情報
				TrainerBreederAvgPrizeMoney = trainerbreeders.AvgPrizeMoney,
				TrainerBreederMaxPrizeMoney = trainerbreeders.MaxPrizeMoney,
				//TrainerBreederAvgRating = trainerbreeders.AvgRating,
				//TrainerBreederMaxRating = trainerbreeders.MaxRating,
				//TrainerBreederAvgGrade = trainerbreeders.AvgGrade,
				//TrainerBreederMaxGrade = trainerbreeders.MaxGrade,
				TrainerBreederAvgFinishPosition = trainerbreeders.AvgFinishPosition,
				//TrainerBreederMaxFinishPosition = trainerbreeders.MaxFinishPosition,
				TrainerBreederAvgAdjustedScore = trainerbreeders.AvgAdjustedScore,
				TrainerBreederMaxAdjustedScore = trainerbreeders.MaxAdjustedScore,
				TrainerBreederAvgTime2Top = trainerbreeders.AvgTime2Top,
				//TrainerBreederMaxTime2Top = trainerbreeders.MaxTime2Top,
				TrainerBreederAvgTime2Condition = trainerbreeders.AvgTime2Condition,
				TrainerBreederMaxTime2Condition = trainerbreeders.MaxTime2Condition,

			};

			return features;
		}

		public static OptimizedHorseFeatures[] CalculateInRaces(this IEnumerable<OptimizedHorseFeatures> features)
		{
			var results = features.ToArray();

			var pace = results.Average(x => x.Tuka);

			results.ForEach(x =>
			{
				// 獲得賞金
				x.AvgPrizeMoneyRank = x.GetRank(results, x => x.AvgPrizeMoney, true);
				x.MaxPrizeMoneyRank = x.GetRank(results, x => x.MaxPrizeMoney, true);

				//// ﾚｰﾃｨﾝｸﾞ
				//x.AvgRatingRank = x.GetRank(results, x => x.AvgRating, true);
				//x.MaxRatingRank = x.GetRank(results, x => x.MaxRating, true);

				//// ｸﾞﾚｰﾄﾞ
				//x.AvgGradeRank = x.GetRank(results, x => x.AvgGrade, true);
				//x.MaxGradeRank = x.GetRank(results, x => x.MaxGrade, true);

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

				// 着順
				x.AvgFinishPosition1Rank = x.GetRank(results, x => x.AvgFinishPosition1, true);
				x.AvgFinishPosition3Rank = x.GetRank(results, x => x.AvgFinishPosition3, true);
				x.MaxFinishPosition3Rank = x.GetRank(results, x => x.MaxFinishPosition3, true);
				x.AvgFinishPosition5Rank = x.GetRank(results, x => x.AvgFinishPosition5, true);
				x.MaxFinishPosition5Rank = x.GetRank(results, x => x.MaxFinishPosition5, true);

				// 調整ｽｺｱ
				x.AvgAdjustedScore1Rank = x.GetRank(results, x => x.AvgAdjustedScore1, true);
				x.AvgAdjustedScore3Rank = x.GetRank(results, x => x.AvgAdjustedScore3, true);
				x.MaxAdjustedScore3Rank = x.GetRank(results, x => x.MaxAdjustedScore3, true);
				x.AvgAdjustedScore5Rank = x.GetRank(results, x => x.AvgAdjustedScore5, true);
				x.MaxAdjustedScore5Rank = x.GetRank(results, x => x.MaxAdjustedScore5, true);

				// ﾄｯﾌﾟとのﾀｲﾑ差
				x.AvgTime2Top1Rank = x.GetRank(results, x => x.AvgTime2Top1, false);
				x.AvgTime2Top3Rank = x.GetRank(results, x => x.AvgTime2Top3, false);
				x.MaxTime2Top3Rank = x.GetRank(results, x => x.MaxTime2Top3, false);
				x.AvgTime2Top5Rank = x.GetRank(results, x => x.AvgTime2Top5, false);
				x.MaxTime2Top5Rank = x.GetRank(results, x => x.MaxTime2Top5, false);

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

				// 同条件ﾚｰｽとの3ﾊﾛﾝ差
				x.AvgLastThreeFurlongs2Condition1Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition1, false);
				x.AvgLastThreeFurlongs2Condition3Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition3, false);
				x.MaxLastThreeFurlongs2Condition3Rank = x.GetRank(results, x => x.MaxLastThreeFurlongs2Condition3, false);
				x.AvgLastThreeFurlongs2Condition5Rank = x.GetRank(results, x => x.AvgLastThreeFurlongs2Condition5, false);
				x.MaxLastThreeFurlongs2Condition5Rank = x.GetRank(results, x => x.MaxLastThreeFurlongs2Condition5, false);

				//// 父馬の実績
				//x.SireAvgPrizeMoneyRank = x.GetRank(results, x => x.SireAvgPrizeMoney, true);
				//x.SireMaxPrizeMoneyRank = x.GetRank(results, x => x.SireMaxPrizeMoney, true);
				//x.SireAvgRatingRank = x.GetRank(results, x => x.SireAvgRating, true);
				//x.SireMaxRatingRank = x.GetRank(results, x => x.SireMaxRating, true);
				//x.SireAvgGradeRank = x.GetRank(results, x => x.SireAvgGrade, true);
				//x.SireMaxGradeRank = x.GetRank(results, x => x.SireMaxGrade, true);
				//x.SireDistanceDiffRank = x.GetRank(results, x => x.SireDistanceDiff, false);
				//x.SireAvgFinishPositionRank = x.GetRank(results, x => x.SireAvgFinishPosition, true);
				//x.SireMaxFinishPositionRank = x.GetRank(results, x => x.SireMaxFinishPosition, true);
				//x.SireAvgAdjustedScoreRank = x.GetRank(results, x => x.SireAvgAdjustedScore, true);
				//x.SireMaxAdjustedScoreRank = x.GetRank(results, x => x.SireMaxAdjustedScore, true);
				//x.SireAvgTime2TopRank = x.GetRank(results, x => x.SireAvgTime2Top, false);
				//x.SireMaxTime2TopRank = x.GetRank(results, x => x.SireMaxTime2Top, false);
				//x.SireAvgLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireAvgLastThreeFurlongs2Top, false);
				//x.SireMaxLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireMaxLastThreeFurlongs2Top, false);
				//x.SireAvgTime2ConditionRank = x.GetRank(results, x => x.SireAvgTime2Condition, false);
				//x.SireMaxTime2ConditionRank = x.GetRank(results, x => x.SireMaxTime2Condition, false);
				//x.SireAvgLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireAvgLastThreeFurlongs2Condition, false);
				//x.SireMaxLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireMaxLastThreeFurlongs2Condition, false);

				//// 母父馬の実績
				//x.DamSireAvgPrizeMoneyRank = x.GetRank(results, x => x.DamSireAvgPrizeMoney, true);
				//x.DamSireMaxPrizeMoneyRank = x.GetRank(results, x => x.DamSireMaxPrizeMoney, true);
				//x.DamSireAvgRatingRank = x.GetRank(results, x => x.DamSireAvgRating, true);
				//x.DamSireMaxRatingRank = x.GetRank(results, x => x.DamSireMaxRating, true);
				//x.DamSireAvgGradeRank = x.GetRank(results, x => x.DamSireAvgGrade, true);
				//x.DamSireMaxGradeRank = x.GetRank(results, x => x.DamSireMaxGrade, true);
				//x.DamSireDistanceDiffRank = x.GetRank(results, x => x.DamSireDistanceDiff, false);
				//x.DamSireAvgFinishPositionRank = x.GetRank(results, x => x.DamSireAvgFinishPosition, true);
				//x.DamSireMaxFinishPositionRank = x.GetRank(results, x => x.DamSireMaxFinishPosition, true);
				//x.DamSireAvgAdjustedScoreRank = x.GetRank(results, x => x.DamSireAvgAdjustedScore, true);
				//x.DamSireMaxAdjustedScoreRank = x.GetRank(results, x => x.DamSireMaxAdjustedScore, true);
				//x.DamSireAvgTime2TopRank = x.GetRank(results, x => x.DamSireAvgTime2Top, false);
				//x.DamSireMaxTime2TopRank = x.GetRank(results, x => x.DamSireMaxTime2Top, false);
				//x.DamSireAvgLastThreeFurlongs2TopRank = x.GetRank(results, x => x.DamSireAvgLastThreeFurlongs2Top, false);
				//x.DamSireMaxLastThreeFurlongs2TopRank = x.GetRank(results, x => x.DamSireMaxLastThreeFurlongs2Top, false);
				//x.DamSireAvgTime2ConditionRank = x.GetRank(results, x => x.DamSireAvgTime2Condition, false);
				//x.DamSireMaxTime2ConditionRank = x.GetRank(results, x => x.DamSireMaxTime2Condition, false);
				//x.DamSireAvgLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.DamSireAvgLastThreeFurlongs2Condition, false);
				//x.DamSireMaxLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.DamSireMaxLastThreeFurlongs2Condition, false);

				// 父馬の兄弟馬の実績
				x.SireBrosAvgPrizeMoneyRank = x.GetRank(results, x => x.SireBrosAvgPrizeMoney, true);
				x.SireBrosMaxPrizeMoneyRank = x.GetRank(results, x => x.SireBrosMaxPrizeMoney, true);
				//x.SireBrosAvgRatingRank = x.GetRank(results, x => x.SireBrosAvgRating, true);
				//x.SireBrosMaxRatingRank = x.GetRank(results, x => x.SireBrosMaxRating, true);
				//x.SireBrosAvgGradeRank = x.GetRank(results, x => x.SireBrosAvgGrade, true);
				//x.SireBrosMaxGradeRank = x.GetRank(results, x => x.SireBrosMaxGrade, true);
				x.SireBrosDistanceDiffRank = x.GetRank(results, x => x.SireBrosDistanceDiff, false);
				x.SireBrosAvgFinishPositionRank = x.GetRank(results, x => x.SireBrosAvgFinishPosition, true);
				//x.SireBrosMaxFinishPositionRank = x.GetRank(results, x => x.SireBrosMaxFinishPosition, true);
				x.SireBrosAvgAdjustedScoreRank = x.GetRank(results, x => x.SireBrosAvgAdjustedScore, true);
				x.SireBrosMaxAdjustedScoreRank = x.GetRank(results, x => x.SireBrosMaxAdjustedScore, true);
				x.SireBrosAvgTime2TopRank = x.GetRank(results, x => x.SireBrosAvgTime2Top, false);
				//x.SireBrosMaxTime2TopRank = x.GetRank(results, x => x.SireBrosMaxTime2Top, false);
				x.SireBrosAvgLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireBrosAvgLastThreeFurlongs2Top, false);
				//x.SireBrosMaxLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireBrosMaxLastThreeFurlongs2Top, false);
				x.SireBrosAvgTime2ConditionRank = x.GetRank(results, x => x.SireBrosAvgTime2Condition, false);
				//x.SireBrosMaxTime2ConditionRank = x.GetRank(results, x => x.SireBrosMaxTime2Condition, false);
				x.SireBrosAvgLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireBrosAvgLastThreeFurlongs2Condition, false);
				//x.SireBrosMaxLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireBrosMaxLastThreeFurlongs2Condition, false);

				// 母父馬の兄弟馬の実績
				//x.DamSireBrosAvgPrizeMoneyRank = x.GetRank(results, x => x.DamSireBrosAvgPrizeMoney, true);
				//x.DamSireBrosMaxPrizeMoneyRank = x.GetRank(results, x => x.DamSireBrosMaxPrizeMoney, true);
				//x.DamSireBrosAvgRatingRank = x.GetRank(results, x => x.DamSireBrosAvgRating, true);
				//x.DamSireBrosMaxRatingRank = x.GetRank(results, x => x.DamSireBrosMaxRating, true);
				//x.DamSireBrosAvgGradeRank = x.GetRank(results, x => x.DamSireBrosAvgGrade, true);
				//x.DamSireBrosMaxGradeRank = x.GetRank(results, x => x.DamSireBrosMaxGrade, true);
				x.DamSireBrosDistanceDiffRank = x.GetRank(results, x => x.DamSireBrosDistanceDiff, false);
				x.DamSireBrosAvgFinishPositionRank = x.GetRank(results, x => x.DamSireBrosAvgFinishPosition, true);
				//x.DamSireBrosMaxFinishPositionRank = x.GetRank(results, x => x.DamSireBrosMaxFinishPosition, true);
				x.DamSireBrosAvgAdjustedScoreRank = x.GetRank(results, x => x.DamSireBrosAvgAdjustedScore, true);
				//x.DamSireBrosMaxAdjustedScoreRank = x.GetRank(results, x => x.DamSireBrosMaxAdjustedScore, true);
				//x.DamSireBrosAvgTime2TopRank = x.GetRank(results, x => x.DamSireBrosAvgTime2Top, false);
				//x.DamSireBrosMaxTime2TopRank = x.GetRank(results, x => x.DamSireBrosMaxTime2Top, false);
				//x.DamSireBrosAvgLastThreeFurlongs2TopRank = x.GetRank(results, x => x.DamSireBrosAvgLastThreeFurlongs2Top, false);
				//x.DamSireBrosMaxLastThreeFurlongs2TopRank = x.GetRank(results, x => x.DamSireBrosMaxLastThreeFurlongs2Top, false);
				//x.DamSireBrosAvgTime2ConditionRank = x.GetRank(results, x => x.DamSireBrosAvgTime2Condition, false);
				//x.DamSireBrosMaxTime2ConditionRank = x.GetRank(results, x => x.DamSireBrosMaxTime2Condition, false);
				//x.DamSireBrosAvgLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.DamSireBrosAvgLastThreeFurlongs2Condition, false);
				//x.DamSireBrosMaxLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.DamSireBrosMaxLastThreeFurlongs2Condition, false);

				// 父馬-母父馬の兄弟馬の情報
				x.SireDamSireBrosAvgPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireBrosAvgPrizeMoney, true);
				//x.SireDamSireBrosMaxPrizeMoneyRank = x.GetRank(results, x => x.SireDamSireBrosMaxPrizeMoney, true);
				//x.SireDamSireBrosAvgRatingRank = x.GetRank(results, x => x.SireDamSireBrosAvgRating, true);
				//x.SireDamSireBrosMaxRatingRank = x.GetRank(results, x => x.SireDamSireBrosMaxRating, true);
				//x.SireDamSireBrosAvgGradeRank = x.GetRank(results, x => x.SireDamSireBrosAvgGrade, true);
				//x.SireDamSireBrosMaxGradeRank = x.GetRank(results, x => x.SireDamSireBrosMaxGrade, true);
				x.SireDamSireBrosDistanceDiffRank = x.GetRank(results, x => x.SireDamSireBrosDistanceDiff, false);
				x.SireDamSireBrosAvgFinishPositionRank = x.GetRank(results, x => x.SireDamSireBrosAvgFinishPosition, true);
				//x.SireDamSireBrosMaxFinishPositionRank = x.GetRank(results, x => x.SireDamSireBrosMaxFinishPosition, true);
				x.SireDamSireBrosAvgAdjustedScoreRank = x.GetRank(results, x => x.SireDamSireBrosAvgAdjustedScore, true);
				x.SireDamSireBrosMaxAdjustedScoreRank = x.GetRank(results, x => x.SireDamSireBrosMaxAdjustedScore, true);
				//x.SireDamSireBrosAvgTime2TopRank = x.GetRank(results, x => x.SireDamSireBrosAvgTime2Top, false);
				//x.SireDamSireBrosMaxTime2TopRank = x.GetRank(results, x => x.SireDamSireBrosMaxTime2Top, false);
				x.SireDamSireBrosAvgLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireDamSireBrosAvgLastThreeFurlongs2Top, false);
				//x.SireDamSireBrosMaxLastThreeFurlongs2TopRank = x.GetRank(results, x => x.SireDamSireBrosMaxLastThreeFurlongs2Top, false);
				x.SireDamSireBrosAvgTime2ConditionRank = x.GetRank(results, x => x.SireDamSireBrosAvgTime2Condition, false);
				//x.SireDamSireBrosMaxTime2ConditionRank = x.GetRank(results, x => x.SireDamSireBrosMaxTime2Condition, false);
				x.SireDamSireBrosAvgLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireDamSireBrosAvgLastThreeFurlongs2Condition, false);
				//x.SireDamSireBrosMaxLastThreeFurlongs2ConditionRank = x.GetRank(results, x => x.SireDamSireBrosMaxLastThreeFurlongs2Condition, false);

				// 騎手の情報
				x.JockeyAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyAvgPrizeMoney, true);
				x.JockeyMaxPrizeMoneyRank = x.GetRank(results, x => x.JockeyMaxPrizeMoney, true);
				//x.JockeyAvgRatingRank = x.GetRank(results, x => x.JockeyAvgRating, true);
				//x.JockeyMaxRatingRank = x.GetRank(results, x => x.JockeyMaxRating, true);
				//x.JockeyAvgGradeRank = x.GetRank(results, x => x.JockeyAvgGrade, true);
				//x.JockeyMaxGradeRank = x.GetRank(results, x => x.JockeyMaxGrade, true);
				x.JockeyAvgFinishPositionRank = x.GetRank(results, x => x.JockeyAvgFinishPosition, true);
				//x.JockeyMaxFinishPositionRank = x.GetRank(results, x => x.JockeyMaxFinishPosition, true);
				x.JockeyAvgAdjustedScoreRank = x.GetRank(results, x => x.JockeyAvgAdjustedScore, true);
				x.JockeyMaxAdjustedScoreRank = x.GetRank(results, x => x.JockeyMaxAdjustedScore, true);
				x.JockeyAvgTime2TopRank = x.GetRank(results, x => x.JockeyAvgTime2Top, false);
				//x.JockeyMaxTime2TopRank = x.GetRank(results, x => x.JockeyMaxTime2Top, false);
				x.JockeyAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyAvgTime2Condition, false);
				//x.JockeyMaxTime2ConditionRank = x.GetRank(results, x => x.JockeyMaxTime2Condition, false);

				// 騎手-ｺｰｽの情報
				x.JockeyPlaceAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyPlaceAvgPrizeMoney, true);
				x.JockeyPlaceMaxPrizeMoneyRank = x.GetRank(results, x => x.JockeyPlaceMaxPrizeMoney, true);
				//x.JockeyPlaceAvgRatingRank = x.GetRank(results, x => x.JockeyPlaceAvgRating, true);
				//x.JockeyPlaceMaxRatingRank = x.GetRank(results, x => x.JockeyPlaceMaxRating, true);
				//x.JockeyPlaceAvgGradeRank = x.GetRank(results, x => x.JockeyPlaceAvgGrade, true);
				//x.JockeyPlaceMaxGradeRank = x.GetRank(results, x => x.JockeyPlaceMaxGrade, true);
				x.JockeyPlaceAvgFinishPositionRank = x.GetRank(results, x => x.JockeyPlaceAvgFinishPosition, true);
				//x.JockeyPlaceMaxFinishPositionRank = x.GetRank(results, x => x.JockeyPlaceMaxFinishPosition, true);
				x.JockeyPlaceAvgAdjustedScoreRank = x.GetRank(results, x => x.JockeyPlaceAvgAdjustedScore, true);
				x.JockeyPlaceMaxAdjustedScoreRank = x.GetRank(results, x => x.JockeyPlaceMaxAdjustedScore, true);
				x.JockeyPlaceAvgTime2TopRank = x.GetRank(results, x => x.JockeyPlaceAvgTime2Top, false);
				//x.JockeyPlaceMaxTime2TopRank = x.GetRank(results, x => x.JockeyPlaceMaxTime2Top, false);
				x.JockeyPlaceAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyPlaceAvgTime2Condition, false);
				//x.JockeyPlaceMaxTime2ConditionRank = x.GetRank(results, x => x.JockeyPlaceMaxTime2Condition, false);

				// 騎手-馬場の情報
				x.JockeyTrackAvgPrizeMoneyRank = x.GetRank(results, x => x.JockeyTrackAvgPrizeMoney, true);
				x.JockeyTrackMaxPrizeMoneyRank = x.GetRank(results, x => x.JockeyTrackMaxPrizeMoney, true);
				//x.JockeyTrackAvgRatingRank = x.GetRank(results, x => x.JockeyTrackAvgRating, true);
				//x.JockeyTrackMaxRatingRank = x.GetRank(results, x => x.JockeyTrackMaxRating, true);
				//x.JockeyTrackAvgGradeRank = x.GetRank(results, x => x.JockeyTrackAvgGrade, true);
				//x.JockeyTrackMaxGradeRank = x.GetRank(results, x => x.JockeyTrackMaxGrade, true);
				x.JockeyTrackAvgFinishPositionRank = x.GetRank(results, x => x.JockeyTrackAvgFinishPosition, true);
				//x.JockeyTrackMaxFinishPositionRank = x.GetRank(results, x => x.JockeyTrackMaxFinishPosition, true);
				x.JockeyTrackAvgAdjustedScoreRank = x.GetRank(results, x => x.JockeyTrackAvgAdjustedScore, true);
				x.JockeyTrackMaxAdjustedScoreRank = x.GetRank(results, x => x.JockeyTrackMaxAdjustedScore, true);
				x.JockeyTrackAvgTime2TopRank = x.GetRank(results, x => x.JockeyTrackAvgTime2Top, false);
				//x.JockeyTrackMaxTime2TopRank = x.GetRank(results, x => x.JockeyTrackMaxTime2Top, false);
				x.JockeyTrackAvgTime2ConditionRank = x.GetRank(results, x => x.JockeyTrackAvgTime2Condition, false);
				//x.JockeyTrackMaxTime2ConditionRank = x.GetRank(results, x => x.JockeyTrackMaxTime2Condition, false);

				// 調教師の情報
				x.TrainerAvgPrizeMoneyRank = x.GetRank(results, x => x.TrainerAvgPrizeMoney, true);
				x.TrainerMaxPrizeMoneyRank = x.GetRank(results, x => x.TrainerMaxPrizeMoney, true);
				//x.TrainerAvgRatingRank = x.GetRank(results, x => x.TrainerAvgRating, true);
				//x.TrainerMaxRatingRank = x.GetRank(results, x => x.TrainerMaxRating, true);
				//x.TrainerAvgGradeRank = x.GetRank(results, x => x.TrainerAvgGrade, true);
				//x.TrainerMaxGradeRank = x.GetRank(results, x => x.TrainerMaxGrade, true);
				x.TrainerAvgFinishPositionRank = x.GetRank(results, x => x.TrainerAvgFinishPosition, true);
				//x.TrainerMaxFinishPositionRank = x.GetRank(results, x => x.TrainerMaxFinishPosition, true);
				x.TrainerAvgAdjustedScoreRank = x.GetRank(results, x => x.TrainerAvgAdjustedScore, true);
				x.TrainerMaxAdjustedScoreRank = x.GetRank(results, x => x.TrainerMaxAdjustedScore, true);
				x.TrainerAvgTime2TopRank = x.GetRank(results, x => x.TrainerAvgTime2Top, false);
				//x.TrainerMaxTime2TopRank = x.GetRank(results, x => x.TrainerMaxTime2Top, false);
				x.TrainerAvgTime2ConditionRank = x.GetRank(results, x => x.TrainerAvgTime2Condition, false);
				//x.TrainerMaxTime2ConditionRank = x.GetRank(results, x => x.TrainerMaxTime2Condition, false);

				// 生産者の情報
				x.BreederAvgPrizeMoneyRank = x.GetRank(results, x => x.BreederAvgPrizeMoney, true);
				x.BreederMaxPrizeMoneyRank = x.GetRank(results, x => x.BreederMaxPrizeMoney, true);
				//x.BreederAvgRatingRank = x.GetRank(results, x => x.BreederAvgRating, true);
				//x.BreederMaxRatingRank = x.GetRank(results, x => x.BreederMaxRating, true);
				//x.BreederAvgGradeRank = x.GetRank(results, x => x.BreederAvgGrade, true);
				//x.BreederMaxGradeRank = x.GetRank(results, x => x.BreederMaxGrade, true);
				x.BreederAvgFinishPositionRank = x.GetRank(results, x => x.BreederAvgFinishPosition, true);
				//x.BreederMaxFinishPositionRank = x.GetRank(results, x => x.BreederMaxFinishPosition, true);
				x.BreederAvgAdjustedScoreRank = x.GetRank(results, x => x.BreederAvgAdjustedScore, true);
				x.BreederMaxAdjustedScoreRank = x.GetRank(results, x => x.BreederMaxAdjustedScore, true);
				x.BreederAvgTime2TopRank = x.GetRank(results, x => x.BreederAvgTime2Top, false);
				//x.BreederMaxTime2TopRank = x.GetRank(results, x => x.BreederMaxTime2Top, false);
				x.BreederAvgTime2ConditionRank = x.GetRank(results, x => x.BreederAvgTime2Condition, false);
				//x.BreederMaxTime2ConditionRank = x.GetRank(results, x => x.BreederMaxTime2Condition, false);

				// 調教師-生産者の情報
				x.TrainerBreederAvgPrizeMoneyRank = x.GetRank(results, x => x.TrainerBreederAvgPrizeMoney, true);
				x.TrainerBreederMaxPrizeMoneyRank = x.GetRank(results, x => x.TrainerBreederMaxPrizeMoney, true);
				//x.TrainerBreederAvgRatingRank = x.GetRank(results, x => x.TrainerBreederAvgRating, true);
				//x.TrainerBreederMaxRatingRank = x.GetRank(results, x => x.TrainerBreederMaxRating, true);
				//x.TrainerBreederAvgGradeRank = x.GetRank(results, x => x.TrainerBreederAvgGrade, true);
				//x.TrainerBreederMaxGradeRank = x.GetRank(results, x => x.TrainerBreederMaxGrade, true);
				x.TrainerBreederAvgFinishPositionRank = x.GetRank(results, x => x.TrainerBreederAvgFinishPosition, true);
				//x.TrainerBreederMaxFinishPositionRank = x.GetRank(results, x => x.TrainerBreederMaxFinishPosition, true);
				x.TrainerBreederAvgAdjustedScoreRank = x.GetRank(results, x => x.TrainerBreederAvgAdjustedScore, true);
				x.TrainerBreederMaxAdjustedScoreRank = x.GetRank(results, x => x.TrainerBreederMaxAdjustedScore, true);
				x.TrainerBreederAvgTime2TopRank = x.GetRank(results, x => x.TrainerBreederAvgTime2Top, false);
				//x.TrainerBreederMaxTime2TopRank = x.GetRank(results, x => x.TrainerBreederMaxTime2Top, false);
				x.TrainerBreederAvgTime2ConditionRank = x.GetRank(results, x => x.TrainerBreederAvgTime2Condition, false);
				x.TrainerBreederMaxTime2ConditionRank = x.GetRank(results, x => x.TrainerBreederMaxTime2Condition, false);
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

			score += (0.1F / Math.Max(x.Time2Avg, 0.1F)) * 0.2F;

			score += (0.1F / Math.Max(x.Time2Top, 0.1F)) * 0.2F;

			score += x.Race.Grade.GetGradeFeatures() * 0.15F;

			score += (x.Race.AverageRating - 40F).MinMax(0F, 95F) / 95F * 0.15F;

			score += x.Race.NumberOfHorses.Single().MinMax(5F, 18F) / 18F * 0.1F;

			return score;
		}

		private static HorseScoreMetrics GetHorseScoreMetrics(this RaceDetail detail, List<RaceDetail> horses)
		{
			return new HorseScoreMetrics()
			{
				// 獲得賞金
				AvgPrizeMoney = horses.Median(x => x.TimeIndex, 0F),
				MaxPrizeMoney = horses.Max(x => x.TimeIndex, 0F),

				// ﾚｰﾃｨﾝｸﾞ
				AvgRating = horses.Median(x => x.TimeIndex, DefaultRating),
				MaxRating = horses.Max(x => x.TimeIndex, DefaultRating),

				// ｸﾞﾚｰﾄﾞ
				AvgGrade = horses.Median(x => x.Race.Grade.GetGradeFeatures(), DefaultGrade),
				MaxGrade = horses.Max(x => x.Race.Grade.GetGradeFeatures(), DefaultGrade),

				// 距離
				DistanceDiff = detail.Race.Distance - horses.Median(x => x.Race.Distance, 1400F),

				// 通過順
				Tuka = horses.Median(x => x.Tuka, 0.5F),

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
				AvgPrizeMoney = horses.Median(x => x.TimeIndex, 0F),
				MaxPrizeMoney = horses.Max(x => x.TimeIndex, 0F),

				// ﾚｰﾃｨﾝｸﾞ
				AvgRating = horses.Median(x => x.TimeIndex, DefaultRating),
				MaxRating = horses.Max(x => x.TimeIndex, DefaultRating),

				// ｸﾞﾚｰﾄﾞ
				AvgGrade = horses.Median(x => x.Race.Grade.GetGradeFeatures(), DefaultGrade),
				MaxGrade = horses.Max(x => x.Race.Grade.GetGradeFeatures(), DefaultGrade),

				// 着順
				AvgFinishPosition = horses.Median(CalculateFinishPosition, DefaultFinishPosition),
				MaxFinishPosition = horses.Max(CalculateFinishPosition, DefaultFinishPosition),

				// 調整ｽｺｱ
				AvgAdjustedScore = horses.Median(CalculateAdjustedScore, DefaultAdjustedScore),
				MaxAdjustedScore = horses.Max(CalculateAdjustedScore, DefaultAdjustedScore),

				// ﾄｯﾌﾟとのﾀｲﾑ差
				AvgTime2Top = horses.Median(x => x.Time2Top, DefaultTime2Top),
				MaxTime2Top = horses.Min(x => x.Time2Top, DefaultTime2Top),

				// 同条件ﾚｰｽとのﾀｲﾑ差
				AvgTime2Condition = horses.Median(x => x.Time2Avg, DefaultTime2Top),
				MaxTime2Condition = horses.Min(x => x.Time2Avg, DefaultTime2Top),
			};
		}
	}
}