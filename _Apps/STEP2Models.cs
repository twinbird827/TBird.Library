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

	public class HorseData
	{
		public HorseData(Dictionary<string, object> x)
		{
			Name = x["馬ID"].Str();
			BirthDate = x["生年月日"].Date();
			SireName = x["父ID"].Str("Unknown");
			DamSireName = x["母父ID"].Str("Unknown");
			BreederName = x["馬主ID"].Str("Unknown");
			PurchasePrice = x["購入額"].Int64();
		}

		public string Name { get; }
		public DateTime BirthDate { get; }
		public string SireName { get; }
		public string DamSireName { get; }
		public string BreederName { get; }
		public long PurchasePrice { get; }
	}

	public class HorseDetails
	{
		public HorseDetails(HorseData horseData, List<RaceData> raceHistory, DateTime asOfDate)
		{
			Source = horseData;
			Age = CalculateAge(horseData?.BirthDate ?? asOfDate.AddYears(-4), asOfDate);
			PreviousWeight = raceHistory.Skip(1).FirstOrDefault()?.Weight ?? 456;
			LastRaceDate = raceHistory.FirstOrDefault()?.RaceDate ?? DateTime.MinValue;
			RaceCount = raceHistory.Count;
		}

		private HorseData Source { get; }
		public string Name => Source.Name;
		public float Age { get; }
		public float PreviousWeight { get; }
		public string SireName => Source.SireName;
		public string DamSireName => Source.DamSireName;
		public string BreederName => Source.BreederName;
		public DateTime LastRaceDate { get; }
		public long PurchasePrice => Source.PurchasePrice;
		public int RaceCount { get; }

		private float CalculateAge(DateTime birthDate, DateTime asOfDate)
		{
			return (asOfDate - birthDate).TotalDays.Single() / 365F;
		}
	}

	public class Horse
	{
		public Horse(RaceData result, HorseDetails horseDetails, IEnumerable<RaceResult> history)
		{
			RaceSource = result;
			HorseSource = horseDetails;
			RaceHistory = history.ToArray();
		}

		private RaceData RaceSource { get; }
		private HorseDetails HorseSource { get; }

		public string Name => HorseSource.Name;
		public float Age => HorseSource.Age;
		public float Weight => RaceSource.Weight;
		public float PreviousWeight => HorseSource.Age;
		public string Jockey => RaceSource.JockeyName;
		public string Trainer => RaceSource.TrainerName;
		public string Sire => HorseSource.SireName;
		public string DamSire => HorseSource.DamSireName;
		public string Breeder => HorseSource.BreederName;
		public DateTime LastRaceDate => HorseSource.LastRaceDate;
		public long PurchasePrice => HorseSource.PurchasePrice;
		public float Odds => RaceSource.Odds;
		public int RaceCount => HorseSource.RaceCount;
		public RaceResult[] RaceHistory { get; }
	}

	public class RaceData
	{
		public RaceData(Dictionary<string, object> x)
		{
			RaceId = x["ﾚｰｽID"].Str();
			CourseName = x["ﾚｰｽ名"].Str();
			Distance = x["距離"].Int32();
			DistanceCategory = Distance.ToDistanceCategory();
			Track = x["馬場"].Str();
			TrackType = Track.ToTrackType();
			TrackCondition = x["馬場状態"].Str();
			TrackConditionType = TrackCondition.ToTrackConditionType();
			Grade = x["ﾗﾝｸ1"].Str().ToGrade();
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

		public string RaceId { get; }
		public string CourseName { get; }
		public int Distance { get; }
		public DistanceCategory DistanceCategory { get; }
		public string Track { get; }
		public TrackType TrackType { get; }
		public string TrackCondition { get; }
		public TrackConditionType TrackConditionType { get; }
		public GradeType Grade { get; }
		public long FirstPrizeMoney { get; }
		public int NumberOfHorses { get; }
		public DateTime RaceDate { get; }
		public string HorseName { get; }
		public int FinishPosition { get; }
		public float Weight { get; }
		public float Time { get; }
		public float Odds { get; }
		public string JockeyName { get; }
		public string TrainerName { get; }

		public float CalculateAdjustedInverseScore(Race race) => AdjustedPerformanceCalculator.CalculateAdjustedInverseScore(FinishPosition, race);
	}

	public class Race
	{
		public Race(RaceData r, List<RaceData> all)
		{
			Source = r;
			AverageRating = CalculateAverageRating(r.RaceId, all);
			IsInternational = Grade.IsG1() && FirstPrizeMoney > 200000000;
			IsAgedHorseRace = Grade.IsCLASSIC() == false;
		}

		private RaceData Source { get; }

		public string RaceId => Source.RaceId;
		public string CourseName => Source.CourseName;
		public int Distance => Source.Distance;
		public DistanceCategory DistanceCategory => Source.DistanceCategory;
		public TrackType TrackType => Source.TrackType;
		public TrackConditionType TrackConditionType => Source.TrackConditionType;
		public GradeType Grade => Source.Grade;
		public long FirstPrizeMoney => Source.FirstPrizeMoney;
		public int NumberOfHorses => Source.NumberOfHorses;
		public DateTime RaceDate => Source.RaceDate;
		public float AverageRating { get; }
		public bool IsInternational { get; }
		public bool IsAgedHorseRace { get; }

		private float CalculateAverageRating(string raceId, List<RaceData> all)
		{
			// 簡易レーティング計算（実際の実装では詳細な計算を行う）
			var raceHorses = all.Where(r => r.RaceId == raceId).ToList();
			if (!raceHorses.Any()) return 80.0f;

			// オッズから逆算した強さ指標
			var avgOdds = raceHorses.Average(h => h.Odds);
			return Math.Max(70.0f, Math.Min(95.0f, 100.0f - (float)Math.Log(avgOdds) * 5.0f));
		}
	}

	public class RaceResult
	{
		public RaceResult(RaceData r, List<RaceData> all)
		{
			Source = r;
			HorseExperience = CalculateHorseExperience(all, r.HorseName, r.RaceDate);
			Race = new Race(r, all);
			AdjustedInverseScore = Source.CalculateAdjustedInverseScore(Race);
		}

		private RaceData Source { get; }

		public int FinishPosition => Source.FinishPosition;
		public float Time => Source.Time;
		public int TotalHorses => Source.NumberOfHorses;
		public int HorseExperience { get; }
		public DateTime RaceDate => Source.RaceDate;
		public Race Race { get; }
		public float AdjustedInverseScore { get; }

		private int CalculateHorseExperience(List<RaceData> all, string horseName, DateTime raceDate)
		{
			return all
				.Count(r => r.HorseName == horseName && r.RaceDate < raceDate);
		}
	}

	// ===== CSVファイル用のデータクラス =====

	// ===== 特徴量クラス =====

	public class OptimizedHorseFeatures
	{
		public OptimizedHorseFeatures() : this(string.Empty)
		{

		}

		public OptimizedHorseFeatures(string raceId)
		{
			RaceId = raceId;
		}

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
		[LoadColumn(27)] public float Age { get; set; }
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

	public class OptimizedHorseFeaturesModel : OptimizedHorseFeatures
	{
		public OptimizedHorseFeaturesModel(string raceId, string horseName) : base(raceId)
		{
			HorseName = horseName;
		}

		public string HorseName { get; set; }
	}

	public enum GradeType
	{
		// G1
		G1古 = 20,

		G1ク = 19,

		G1障 = 18,

		// G2
		G2古 = 17,

		G2ク = 16,

		G2障 = 15,

		// G3
		G3古 = 14,

		G3ク = 13,

		G3障 = 12,

		// オープン
		オープン古 = 11,

		オープンク = 10,

		オープン障 = 9,

		// 条件戦
		勝3古 = 8,

		勝2古 = 7,

		勝2ク = 6,

		勝1古 = 5,

		勝1ク = 4,

		// 未勝利・新馬
		未勝利ク = 3,

		未勝利障 = 2,

		新馬ク = 1,

	}

	public enum DistanceCategory
	{
		Sprint,
		Mile,
		Middle,
		Long
	}

	public enum TrackType
	{
		// 芝
		Grass,

		// ダート
		Dirt,

		Unknown
	}

	public enum TrackConditionType
	{
		// 良
		Good,

		// 稍重
		SlightlyHeavy,

		// 重
		Heavy,

		// 不良
		Poor,

		Unknown,
	}

	public static class ModelExtensions
	{
		public static GradeType ToGrade(this string grade) => EnumUtil.ToEnum<GradeType>(grade);

		public static bool IsG1(this GradeType grade) => grade switch
		{
			GradeType.G1ク => true,
			GradeType.G1古 => true,
			GradeType.G1障 => true,
			_ => false
		};

		public static bool IsG2(this GradeType grade) => grade switch
		{
			GradeType.G2ク => true,
			GradeType.G2古 => true,
			GradeType.G2障 => true,
			_ => false
		};

		public static bool IsG3(this GradeType grade) => grade switch
		{
			GradeType.G3ク => true,
			GradeType.G3古 => true,
			GradeType.G3障 => true,
			_ => false
		};

		public static bool IsOPEN(this GradeType grade) => grade switch
		{
			GradeType.オープンク => true,
			GradeType.オープン古 => true,
			GradeType.オープン障 => true,
			_ => false
		};

		public static bool IsCLASSIC(this GradeType grade) => grade switch
		{
			GradeType.G1ク => true,
			GradeType.G2ク => true,
			GradeType.G3ク => true,
			GradeType.オープンク => true,
			GradeType.勝2ク => true,
			GradeType.勝1ク => true,
			GradeType.未勝利ク => true,
			GradeType.新馬ク => true,
			_ => false,
		};

		public static DistanceCategory ToDistanceCategory(this int distance) => distance switch
		{
			<= 1400 => DistanceCategory.Sprint,
			<= 1800 => DistanceCategory.Mile,
			<= 2200 => DistanceCategory.Middle,
			_ => DistanceCategory.Long
		};

		public static TrackType ToTrackType(this string track) => track switch
		{
			"芝" => TrackType.Grass,
			"ダート" => TrackType.Dirt,
			_ => TrackType.Unknown
		};

		public static TrackConditionType ToTrackConditionType(this string condition) => condition switch
		{
			"良" => TrackConditionType.Good,
			"稍重" => TrackConditionType.SlightlyHeavy,
			"重" => TrackConditionType.Heavy,
			"不良" => TrackConditionType.Poor,
			_ => TrackConditionType.Unknown
		};
	}
}