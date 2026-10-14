using Codeplex.Data;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class RaceDetail
	{
		private RaceDetail(Race race, Oikiri oikiri, int wakuban, int umaban, string horse, string jockey, string trainer,
			string sire, string damSire, string breeder, int finishPosition, float time, float prizeMoney,
			float purchasePrice, DateTime birthDate, float jockeyWeight, string tuka, float agari,
			string gender, float timeIndex, string finishDiff, float weight, float weightChange, float odds)
		{
			Race = race;
			Oikiri = oikiri;
			Wakuban = wakuban;
			Umaban = umaban;
			Horse = horse;
			Jockey = jockey;
			Trainer = trainer;
			Sire = sire;
			DamSire = damSire;
			Breeder = breeder;
			FinishPosition = (uint)finishPosition;
			Time = time;
			PrizeMoney = prizeMoney;
			PurchasePrice = purchasePrice;
			BirthDate = birthDate;
			JockeyWeight = jockeyWeight;
			// 通過順＝0:前方～1:後方
			Tuka = tuka
				.Run(y => y.Split('-'))
				.Run(y => y.Length switch
				{
					0 => (Race.NumberOfHorses / 2).Str(),
					1 or 2 => y.Last(),
					_ => y[y.Length - 2]
				})
				.Run(y => (object)y)
				.Single() / (float)Race.NumberOfHorses;
			Tuka = Math.Min(Tuka, 1.0F);
			LastThreeFurlongs = GetLastThreeFurlongs(agari);
			Gender = gender;
			Age = (Race.RaceDate - BirthDate).TotalDays.Single() / 365F;
			TimeIndex = timeIndex.MinMax(40F, 135F);
			FinishDiff = finishDiff;
			Weight = weight;
			WeightChange = weightChange;
			// 単勝は 1.0 未満が存在しないため、0 以下は取得失敗・発売前の欠損として扱う
			Odds = 0 < odds ? odds : float.NaN;
		}

		public RaceDetail(DbDataReader r, Race race, int offset = 10) : this(
			race, new Oikiri(r, offset: 32),
			r.GetValue(offset + 0).Int32(),       // 枠番
			r.GetValue(offset + 1).Int32(),        // 馬番
			r.GetValue(offset + 2).Str(),          // 馬ID
			r.GetValue(offset + 3).Str(),          // 騎手ID
			r.GetValue(offset + 4).Str(),          // 調教師ID
			r.GetValue(offset + 5).Str(),          // 父ID
			r.GetValue(offset + 6).Str(),          // 母父ID
			r.GetValue(offset + 7).Str(),          // 生産者ID
			r.GetValue(offset + 8).Int32(),        // 着順
			r.GetValue(offset + 9).Single(),       // ﾀｲﾑ変換
			r.GetValue(offset + 10).Single(),      // 賞金
			r.GetValue(offset + 11).Single(),      // 評価額
			r.GetValue(offset + 12).Date(),        // 生年月日
			r.GetValue(offset + 13).Single(),      // 斤量
			r.GetValue(offset + 14).Str(),         // 通過
			r.GetValue(offset + 15).Single(),      // 上り
			r.GetValue(offset + 16).Str(),         // 馬性
			r.GetValue(offset + 17).Single(),      // ﾀｲﾑ指数
			r.GetValue(offset + 18).Str(),         // 着差
			r.GetValue(offset + 19).Single(),      // 体重
			r.GetValue(offset + 20).Single(),      // 増減
			r.GetValue(offset + 21).Single()       // 単勝
		)
		{ }

		public RaceDetail(Dictionary<string, object> x, Race race) : this(
			race, new Oikiri(x),
			x.Get("枠番").Int32(),
			x.Get("馬番").Int32(),
			x.Get("馬ID").Str(),
			x.Get("騎手ID").Str(),
			x.Get("調教師ID").Str(),
			x.Get("父ID").Str(),
			x.Get("母父ID").Str(),
			x.Get("生産者ID").Str(),
			x.Get("着順").Int32(),
			x.Get("ﾀｲﾑ変換").Single(),
			x.Get("賞金").Single(),
			x.Get("評価額").Single(),
			x.Get("生年月日").Date(),
			x.Get("斤量").Single(),
			x.Get("通過").Str(),
			x.Get("上り").Single(),
			x.Get("馬性").Str(),
			x.Get("ﾀｲﾑ指数").Single(),
			x.Get("着差").Str(),
			x.Get("体重").Single(),
			x.Get("増減").Single(),
			x.Get("単勝").Single()
		)
		{ }

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
		public float WeightChange { get; }
		public float JockeyWeight { get; }
		public float Odds { get; }
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
			LastThreeFurlongs2Avg = (LastThreeFurlongs - GetLastThreeFurlongs(tcd.LastThreeFurlongs)).MinMax(-4.9F, 4.9F);

			// 直近の情報から計算
			Last = horses.FirstOrDefault();
			Weight = Weight > 300F ? Weight : Last != null ? Last.Weight : 460F;
			LastRaceDate = Last != null ? Last.Race.RaceDate : Race.RaceDate.AddDays(-60);

			// 直近5戦の情報
			AverageRating = horses.Median(x => x.TimeIndex, 60F);
		}
	}
}