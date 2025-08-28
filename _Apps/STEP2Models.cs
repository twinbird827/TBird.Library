using MathNet.Numerics;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Media3D;
using TBird.Core;

namespace Netkeiba
{
	// ===== データモデル =====

	public class Horse
	{
		public Horse(RaceResultData result, HorseDetails horseDetails)
		{
			Name = result.HorseName;
			Age = horseDetails.Age;
			Weight = result.Weight;
			PreviousWeight = horseDetails.PreviousWeight;
			Jockey = result.JockeyName;
			Trainer = result.TrainerName;
			Sire = horseDetails.SireName;
			DamSire = horseDetails.DamSireName;
			Breeder = horseDetails.BreederName;
			LastRaceDate = horseDetails.LastRaceDate;
			PurchasePrice = horseDetails.PurchasePrice;
			Odds = result.Odds;
			RaceCount = horseDetails.RaceCount;
		}

		public string Name { get; set; }
		public int Age { get; set; }
		public float Weight { get; set; }
		public float PreviousWeight { get; set; }
		public string Jockey { get; set; }
		public string Trainer { get; set; }
		public string Sire { get; set; }
		public string DamSire { get; set; }
		public string Breeder { get; set; }
		public DateTime LastRaceDate { get; set; }
		public long PurchasePrice { get; set; }
		public float Odds { get; set; }
		public int RaceCount { get; set; }
		public List<RaceResult> RaceHistory { get; set; } = new();
	}

	public class Race
	{
		public string RaceId { get; set; }
		public string CourseName { get; set; }
		public int Distance { get; set; }
		public string TrackType { get; set; } // 芝, ダート
		public string TrackCondition { get; set; } // 良, 稍重, 重, 不良
		public string Grade { get; set; } // G1, G2, G3, OP, 3勝, 2勝, 1勝, 未勝利, 新馬
		public long FirstPrizeMoney { get; set; }
		public int NumberOfHorses { get; set; }
		public float AverageRating { get; set; }
		public bool IsInternational { get; set; }
		public bool IsAgedHorseRace { get; set; }
		public DateTime RaceDate { get; set; }
		public List<Horse> Horses { get; set; } = new();
	}

	public class RaceResult
	{
		public RaceResult(RaceData r, List<RaceData> all)
		{
			FinishPosition = r.FinishPosition;
			Time = r.Time;
			TotalHorses = r.NumberOfHorses;
			RaceDate = r.RaceDate;
			HorseExperience = CalculateHorseExperience(all, r.HorseName, r.RaceDate);
			Race = new Race
			{
				RaceId = r.RaceId,
				Distance = r.Distance,
				TrackType = r.TrackType,
				TrackCondition = r.TrackCondition,
				Grade = r.Grade,
				CourseName = r.CourseName,
				FirstPrizeMoney = r.FirstPrizeMoney,
				NumberOfHorses = r.NumberOfHorses,
				RaceDate = r.RaceDate
			};
		}

		private int CalculateHorseExperience(List<RaceData> all, string horseName, DateTime raceDate)
		{
			return all
				.Count(r => r.HorseName == horseName && r.RaceDate < raceDate);
		}

		public Race Race { get; set; }
		public int FinishPosition { get; set; }
		public float Time { get; set; }
		public int TotalHorses { get; set; }
		public int HorseExperience { get; set; }
		public DateTime RaceDate { get; set; }

		public float CalculateAdjustedInverseScore() => AdjustedPerformanceCalculator.CalculateAdjustedInverseScore(FinishPosition, Race);
	}

	public class RaceResultData
	{
		public string RaceId { get; set; }
		public string HorseName { get; set; }
		public int FinishPosition { get; set; }
		public float Weight { get; set; }
		public float Time { get; set; }
		public float Odds { get; set; }
		public string JockeyName { get; set; }
		public string TrainerName { get; set; }
		public DateTime RaceDate { get; set; }

		public float CalculateAdjustedInverseScore(Race race) => AdjustedPerformanceCalculator.CalculateAdjustedInverseScore(FinishPosition, race);

	}

	public class HorseDetails
	{
		public string Name { get; set; }
		public int Age { get; set; }
		public float PreviousWeight { get; set; }
		public string SireName { get; set; }
		public string DamSireName { get; set; }
		public string BreederName { get; set; }
		public DateTime LastRaceDate { get; set; }
		public long PurchasePrice { get; set; }
		public int RaceCount { get; set; }
	}

	public class ConnectionDetails
	{
		public string JockeyName { get; set; }
		public string TrainerName { get; set; }
		public DateTime AsOfDate { get; set; }
	}

	// ===== CSVファイル用のデータクラス =====

	public class RaceData
	{
		public RaceData(Dictionary<string, object> x)
		{
			RaceId = x["ﾚｰｽID"].Str();
			CourseName = x["ﾚｰｽ名"].Str();
			Distance = x["距離"].Int32();
			TrackType = x["馬場"].Str();
			TrackCondition = x["馬場状態"].Str();
			Grade = x["ﾗﾝｸ1"].Str();
			FirstPrizeMoney = x["優勝賞金"].Int64();
			NumberOfHorses = x["頭数"].Int32();
			RaceDate = x["開催日"].Date();
			HorseName = x["馬ID"].Str();
			FinishPosition = x["着順"].Int32();
			Weight = x["体重"].Single();
			Time = x["ﾀｲﾑ変換"].Single();
			Odds = x["単勝"].Single();
			JockeyName = x["騎手ID"].Str();
			TrainerName = x["調教師ID"].Str();
		}

		public string RaceId { get; set; }
		public string CourseName { get; set; }
		public int Distance { get; set; }
		public string TrackType { get; set; }
		public string TrackCondition { get; set; }
		public string Grade { get; set; }
		public long FirstPrizeMoney { get; set; }
		public int NumberOfHorses { get; set; }
		public DateTime RaceDate { get; set; }
		public string HorseName { get; set; }
		public int FinishPosition { get; set; }
		public float Weight { get; set; }
		public float Time { get; set; }
		public float Odds { get; set; }
		public string JockeyName { get; set; }
		public string TrainerName { get; set; }
	}

	public class HorseData
	{
		public HorseData(Dictionary<string, object> x)
		{
			Name = x["馬ID"].Str();
			BirthDate = x["生年月日"].Date();
			SireName = x["父ID"].Str();
			DamSireName = x["母父ID"].Str();
			BreederName = x["馬主ID"].Str();
			PurchasePrice = x["購入額"].Int64();
		}

		public string Name { get; set; }
		public DateTime BirthDate { get; set; }
		public string SireName { get; set; }
		public string DamSireName { get; set; }
		public string BreederName { get; set; }
		public long PurchasePrice { get; set; }
	}

	public class ConnectionData
	{
		public string HorseName { get; set; }
		public string JockeyName { get; set; }
		public string TrainerName { get; set; }
		public DateTime FromDate { get; set; }
		public DateTime? ToDate { get; set; }
		public bool IsActive { get; set; }
	}

	// ===== 特徴量クラス =====

	public class OptimizedHorseFeatures
	{
		// 馬の基本実績（難易度調整済み）
		[LoadColumn(0)] public float Recent3AdjustedAvg { get; set; }
		[LoadColumn(1)] public float Recent5AdjustedAvg { get; set; }
		[LoadColumn(2)] public float LastRaceAdjustedScore { get; set; }
		[LoadColumn(3)] public float BestAdjustedScore { get; set; }
		[LoadColumn(4)] public float AdjustedConsistency { get; set; }
		[LoadColumn(5)] public float OverallAdjustedAvg { get; set; }

		// 条件適性（今回のレース条件に対する適性）
		[LoadColumn(6)] public float CurrentDistanceAptitude { get; set; }
		[LoadColumn(7)] public float CurrentTrackTypeAptitude { get; set; }
		[LoadColumn(8)] public float CurrentTrackConditionAptitude { get; set; }
		[LoadColumn(9)] public float HeavyTrackAptitude { get; set; }
		[LoadColumn(10)] public float SpecificCourseAptitude { get; set; }

		// 距離カテゴリ別適性
		[LoadColumn(11)] public float SprintAptitude { get; set; }
		[LoadColumn(12)] public float MileAptitude { get; set; }
		[LoadColumn(13)] public float MiddleDistanceAptitude { get; set; }
		[LoadColumn(14)] public float LongDistanceAptitude { get; set; }

		// 関係者実績（条件特化）
		[LoadColumn(15)] public float JockeyCurrentConditionAvg { get; set; }
		[LoadColumn(16)] public float TrainerCurrentConditionAvg { get; set; }
		[LoadColumn(17)] public float JockeyOverallInverseAvg { get; set; }
		[LoadColumn(18)] public float TrainerOverallInverseAvg { get; set; }

		// 新馬用特徴量
		[LoadColumn(19)] public float TrainerNewHorseInverse { get; set; }
		[LoadColumn(20)] public float JockeyNewHorseInverse { get; set; }
		[LoadColumn(21)] public float SireNewHorseInverse { get; set; }
		[LoadColumn(22)] public float BreederSuccessRate { get; set; }

		// 体重関連（改良版）
		[LoadColumn(23)] public float WeightChange { get; set; }
		[LoadColumn(24)] public float PersonalWeightDeviation { get; set; }
		[LoadColumn(25)] public bool IsRapidWeightChange { get; set; }

		// 馬の状態・変化指標
		[LoadColumn(26)] public int RestDays { get; set; }
		[LoadColumn(27)] public int Age { get; set; }
		[LoadColumn(28)] public float Popularity { get; set; }
		[LoadColumn(29)] public float PerformanceTrend { get; set; }
		[LoadColumn(30)] public float DistanceChangeAdaptation { get; set; }
		[LoadColumn(31)] public float ClassChangeAdaptation { get; set; }

		// タイム関連（正規化済み）
		[LoadColumn(32)] public float SameDistanceTimeIndex { get; set; }
		[LoadColumn(33)] public float LastRaceTimeDeviation { get; set; }
		[LoadColumn(34)] public float TimeConsistencyScore { get; set; }

		// メタ情報
		[LoadColumn(35)] public bool IsNewHorse { get; set; }
		[LoadColumn(36)] public bool HasRaceExperience { get; set; }
		[LoadColumn(37)] public float AptitudeReliability { get; set; }

		// ラベル・グループ情報
		[LoadColumn(38)] public float Label { get; set; }
		[LoadColumn(39)] public string RaceId { get; set; }
	}
}