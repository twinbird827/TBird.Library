using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using Tensorflow;

namespace Netkeiba.Models
{
	public class RaceDetail
	{
		public RaceDetail(Dictionary<string, object> x, Race race)
		{
			try
			{
				Race = race;
				Umaban = x.Get("馬番").Int32();
				Horse = x.Get("馬ID").Str();
				Jockey = x.Get("騎手ID").Str();
				Trainer = x.Get("調教師ID").Str();
				Sire = x.Get("父ID").Str();
				DamSire = x.Get("母父ID").Str();
				Breeder = x.Get("生産者ID").Str();
				FinishPosition = (uint)x.Get("着順").Int32();
				Time = x.Get("ﾀｲﾑ変換").Single();
				PrizeMoney = x.Get("賞金").Single();
				PurchasePrice = x.Get("評価額").Single();
				BirthDate = x.Get("生年月日").Date();
				JockeyWeight = x.Get("斤量").Single();
				Tuka = x.Get("通過").Str()
					// ﾊｲﾌﾝ区切り
					.Run(y => y.Split('-'))
					// 2ﾊﾛﾝ以上あるﾚｰｽは2ﾊﾛﾝ目、短距離は1ﾊﾛﾝ目、ﾃﾞｰﾀがない場合は出走頭数の半分
					.Run(y => 1 < y.Length ? y[1] : y.Length == 1 ? y[0] : (Race.NumberOfHorses / 2).Str())
					.Run(y => (object)y)
					.Single() / (float)Race.NumberOfHorses;
				LastThreeFurlongs = x.Get("上り").Single();

				Age = (Race.RaceDate - BirthDate).TotalDays.Single() / 365F;
			}
			catch (Exception ex)
			{
				MessageService.Debug(ex.ToString());
				throw;
			}
		}

		public Race Race { get; }

		public string RaceId => Race.RaceId;
		public int Umaban { get; }
		public string Horse { get; }
		public string Jockey { get; }
		public string Trainer { get; }
		public string Sire { get; }
		public string DamSire { get; }
		public string SireDamSire => $"{Sire}-{DamSire}";
		public string JockeyTrainer => $"{Jockey}-{Trainer}";
		public string Breeder { get; }
		public uint FinishPosition { get; }
		public float Time { get; }
		public float PrizeMoney { get; }
		public float PurchasePrice { get; }
		public DateTime BirthDate { get; }
		public float Age { get; }
		public float JockeyWeight { get; }
		public float Tuka { get; }
		public float LastThreeFurlongs { get; }

		/// <summary>
		/// 斤量で補正したタイム（基準斤量55kg、1kgあたり0.2秒）
		/// </summary>
		public float AdjustedTime => Time + (JockeyWeight - 55f) * 0.2f;

		public int RaceCount { get; private set; }
		public float AverageRating { get; private set; }
		public DateTime LastRaceDate { get; private set; }

		public float CalculateAdjustedInverseScore() => AdjustedPerformanceCalculator.CalculateAdjustedInverseScore(FinishPosition, Race);

		public void Initialize(List<RaceDetail> horses)
		{
			RaceCount = horses.Count;
			AverageRating = horses.Select(x => x.PrizeMoney)
				.DefaultIfEmpty(PurchasePrice / 10000F / 10F)
				.Average();

			if (horses.Count == 0)
			{
				LastRaceDate = Race.RaceDate.AddMonths(-2);
			}
			else
			{
				LastRaceDate = horses.First().Race.RaceDate;
			}
		}

		public OptimizedHorseFeaturesModel ExtractFeatures(
				List<RaceDetail> horses, RaceDetail[] inRaces, List<RaceDetail> jockeys, List<RaceDetail> trainers, List<RaceDetail> sires, List<RaceDetail> damsires, List<RaceDetail> siredamsires, List<RaceDetail> breeders, List<RaceDetail> jockeytrainers
			)
		{
			float GetStandardTime(int distance)
			{
				return distance switch
				{
					1000 => 58.5f,
					1200 => 70.5f,
					1400 => 83.2f,
					1600 => 95.1f,
					1800 => 109.8f,
					2000 => 123.2f,
					2400 => 148.5f,
					_ => distance * 0.061f // 概算
				};
			}

			float CalculateDistanceChangeAdaptation()
			{
				if (!horses.Any()) return 0.5f;
				var lastDistance = horses.First().Race.Distance;
				var distanceChange = Math.Abs(Race.Distance - lastDistance);
				return 1.0f / (1.0f + distanceChange / 400.0f); // 400m変化で半減
			}

			float CalculateClassChangeAdaptation()
			{
				if (!horses.Any()) return 0.5f;
				var lastGrade = horses.First().Race.Grade;
				var gradeChange = Race.Grade.Int32() - lastGrade.Int32();
				return gradeChange <= 0 ? 1.0f : 1.0f / (1.0f + gradeChange * 0.2f);
			}

			float CalculateSameDistanceTimeIndex(int distance)
			{
				var sameDistanceRaces = horses.Where(r => r.Race.Distance == distance);
				if (!sameDistanceRaces.Any()) return 50.0f; // デフォルト偏差値

				// 簡易タイム偏差値計算（斤量補正済み）
				var times = sameDistanceRaces.Select(r => r.AdjustedTime);
				var avgTime = times.Average();
				var standardTime = GetStandardTime(distance);
				return (standardTime - avgTime) / 2.0f + 50.0f; // 偏差値化
			}

			float CalculateLastRaceTimeDeviation()
			{
				if (!horses.Any()) return 0;
				var lastRace = horses.First();
				var standardTime = GetStandardTime(lastRace.Race.Distance);
				return standardTime - lastRace.AdjustedTime;
			}

			float CalculateTimeConsistency()
			{
				if (horses.Count < 2) return 1.0f;
				var timeDeviations = horses.Take(5).Select(r => GetStandardTime(r.Race.Distance) - r.AdjustedTime);
				var stdDev = AppUtil.CalculateStandardDeviation(timeDeviations.ToArray());
				return 1.0f / (stdDev + 1.0f);
			}

			float CalculateAptitudeReliability()
			{
				var relevantRaces = horses.Count(r =>
					r.Race.Distance == Race.Distance ||
					r.Race.TrackType == Race.TrackType ||
					r.Race.TrackConditionType == Race.TrackConditionType);

				return Math.Min(relevantRaces * 0.2f, 1.0f);
			}

			var adjustedMetrics = AdjustedPerformanceCalculator.CalculateAdjustedPerformance(Race, this, horses);
			var connectionMetrics = ConnectionAnalyzer.AnalyzeConnections(Race, jockeys, trainers, breeders, sires, damsires, siredamsires, jockeytrainers);
			var conditionMetrics = ConditionAptitudeCalculator.CalculateConditionMetrics(horses, Race);
			var lastThreeFurlongsMetrics = LastThreeFurlongsAnalyzer.AnalyzeLastThreeFurlongs(this, horses);
			var jockeyWeightMetrics = JockeyWeightAnalyzer.AnalyzeJockeyWeight(this, horses, inRaces);
			var finishPositionMetrics = FinishPositionAnalyzer.AnalyzeFinishPosition(horses, Race);
			var tukaMetrics = TukaAnalyzer.AnalyzeTuka(horses);

			// 購入価格ランク（全レースで有効）
			var avgPurchasePriceInRace = inRaces.Select(r => r.PurchasePrice).DefaultIfEmpty(PurchasePrice).Average();

			var features = new OptimizedHorseFeaturesModel(this)
			{
				// 基本実績
				Recent3AdjustedAvg = adjustedMetrics.Recent3AdjustedAvg,
				Recent5AdjustedAvg = adjustedMetrics.Recent5AdjustedAvg,
				LastRaceAdjustedScore = adjustedMetrics.LastRaceAdjustedScore,
				BestAdjustedScore = adjustedMetrics.BestAdjustedScore,
				AdjustedConsistency = adjustedMetrics.AdjustedConsistency,
				OverallAdjustedAvg = adjustedMetrics.OverallAdjustedAvg,

				// 条件適性
				CurrentDistanceAptitude = conditionMetrics.CurrentDistanceAptitude,
				CurrentTrackTypeAptitude = conditionMetrics.CurrentTrackTypeAptitude,
				CurrentTrackConditionAptitude = conditionMetrics.CurrentTrackConditionAptitude,
				HeavyTrackAptitude = conditionMetrics.HeavyTrackAptitude,
				SpecificCourseAptitude = conditionMetrics.SpecificCourseAptitude,

				// 距離適性
				SprintAptitude = conditionMetrics.SprintAptitude,
				MileAptitude = conditionMetrics.MileAptitude,
				MiddleDistanceAptitude = conditionMetrics.MiddleDistanceAptitude,
				LongDistanceAptitude = conditionMetrics.LongDistanceAptitude,

				// 関係者実績
				JockeyRecentInverseAvg = connectionMetrics.JockeyRecentInverseAvg,
				JockeyCurrentConditionAvg = connectionMetrics.JockeyCurrentConditionAvg,
				TrainerRecentInverseAvg = connectionMetrics.TrainerRecentInverseAvg,
				TrainerCurrentConditionAvg = connectionMetrics.TrainerCurrentConditionAvg,
				BreederRecentInverseAvg = connectionMetrics.BreederRecentInverseAvg,
				BreederCurrentConditionAvg = connectionMetrics.BreederCurrentConditionAvg,
				SireRecentInverseAvg = connectionMetrics.SireRecentInverseAvg,
				SireCurrentConditionAvg = connectionMetrics.SireCurrentConditionAvg,
				DamSireRecentInverseAvg = connectionMetrics.DamSireRecentInverseAvg,
				DamSireCurrentConditionAvg = connectionMetrics.DamSireCurrentConditionAvg,
				SireDamSireRecentInverseAvg = connectionMetrics.SireDamSireRecentInverseAvg,
				SireDamSireCurrentConditionAvg = connectionMetrics.SireDamSireCurrentConditionAvg,
				JockeyTrainerRecentInverseAvg = connectionMetrics.JockeyTrainerRecentInverseAvg,
				JockeyTrainerCurrentConditionAvg = connectionMetrics.JockeyTrainerCurrentConditionAvg,

				// 状態・変化
				RestDays = (Race.RaceDate - LastRaceDate).Days,
				Age = Age,
				PerformanceTrend = adjustedMetrics.Recent3AdjustedAvg - adjustedMetrics.OverallAdjustedAvg,
				DistanceChangeAdaptation = CalculateDistanceChangeAdaptation(),
				ClassChangeAdaptation = CalculateClassChangeAdaptation(),
				JockeyWeightDiff = jockeyWeightMetrics.JockeyWeightDiff,
				JockeyWeightRankInRace = jockeyWeightMetrics.JockeyWeightRankInRace,
				JockeyWeightDiffFromAvgInRace = jockeyWeightMetrics.JockeyWeightDiffFromAvgInRace,
				AverageTuka = tukaMetrics.AverageTuka,
				LastRaceTuka = tukaMetrics.LastRaceTuka,
				TukaConsistency = tukaMetrics.TukaConsistency,
				AverageTukaInRace = tukaMetrics.AverageTukaInRace,
				LastRaceFinishPosition = finishPositionMetrics.LastRaceFinishPosition,
				Recent3AvgFinishPosition = finishPositionMetrics.Recent3AvgFinishPosition,
				FinishPositionImprovement = finishPositionMetrics.FinishPositionImprovement,
				LastRaceFinishPositionNormalized = finishPositionMetrics.LastRaceFinishPositionNormalized,
				PaceAdvantageScore = tukaMetrics.PaceAdvantageScore,

				// タイム関連
				SameDistanceTimeIndex = CalculateSameDistanceTimeIndex(Race.Distance),
				LastRaceTimeDeviation = CalculateLastRaceTimeDeviation(),
				TimeConsistencyScore = CalculateTimeConsistency(),
				AdjustedLastThreeFurlongsAvg = lastThreeFurlongsMetrics.AdjustedLastThreeFurlongsAvg,
				LastRaceAdjustedLastThreeFurlongs = lastThreeFurlongsMetrics.LastRaceAdjustedLastThreeFurlongs,
				AdjustedLastThreeFurlongsRankInRace = lastThreeFurlongsMetrics.AdjustedLastThreeFurlongsRankInRace,
				AdjustedLastThreeFurlongsDiffFromAvgInRace = lastThreeFurlongsMetrics.AdjustedLastThreeFurlongsDiffFromAvgInRace,

				// メタ情報
				IsNewHorse = RaceCount == 0,
				AptitudeReliability = CalculateAptitudeReliability(),

				// 購入価格ランク（全レースで有効）
				PurchasePriceRank = PurchasePrice / avgPurchasePriceInRace,

				// ラベル（トレーニング時に設定）
				Label = 0, // 実際の着順から計算
			};

			// 新馬の場合は特別処理
			if (RaceCount == 0)
			{
				var newHorseMetrics = MaidenRaceAnalyzer.AnalyzeNewHorse(this, inRaces, horses, jockeys, trainers, breeders, sires, damsires);
				features.TrainerNewHorseInverse = newHorseMetrics.TrainerNewHorseInverse;
				features.JockeyNewHorseInverse = newHorseMetrics.JockeyNewHorseInverse;
				features.SireNewHorseInverse = newHorseMetrics.SireNewHorseInverse;
				features.DamSireNewHorseInverse = newHorseMetrics.DamSireNewHorseInverse;
				features.BreederNewHorseInverse = newHorseMetrics.BreederNewHorseInverse;
			}
			else
			{
				// 新馬以外は通常成績を使用
				features.TrainerNewHorseInverse = connectionMetrics.TrainerRecentInverseAvg;
				features.JockeyNewHorseInverse = connectionMetrics.JockeyRecentInverseAvg;
				features.SireNewHorseInverse = connectionMetrics.SireRecentInverseAvg;
				features.DamSireNewHorseInverse = connectionMetrics.DamSireRecentInverseAvg;
				features.BreederNewHorseInverse = connectionMetrics.BreederRecentInverseAvg;
			}

			return features;
		}
	}

	public static class RaceDetailExtensions
	{
		public static T CalculateInRaces<T>(this T features, Race race) where T : IEnumerable<OptimizedHorseFeatures>
		{
			var inraceAdjustedLastThreeFurlongsAvgs = features.Select(r => r.AdjustedLastThreeFurlongsAvg).ToArray();
			var inraceAverageTuka = features.Select(r => r.AverageTuka).ToArray();
			var frontRunnerCount = inraceAverageTuka.Count(tuka => tuka < 0.3f);  // 逃げ・先行馬の数

			features.ForEach(x =>
			{
				// 同レース他馬との補正済み上がり比較
				x.AdjustedLastThreeFurlongsRankInRace = inraceAdjustedLastThreeFurlongsAvgs.Count(f => f > x.AdjustedLastThreeFurlongsAvg) + 1f;
				x.AdjustedLastThreeFurlongsDiffFromAvgInRace = inraceAdjustedLastThreeFurlongsAvgs.Average() / x.AdjustedLastThreeFurlongsAvg;

				// 通過順比較
				x.AverageTukaInRace = inraceAverageTuka.Average();
				x.PaceAdvantageScore = x.AverageTuka < 0.3F
					// 自分は逃げ→前に行く馬が少ないほど有利
					? (float)(race.NumberOfHorses - frontRunnerCount) / (float)race.NumberOfHorses
					: 0.6F < x.AverageTuka
					// 自分は追込→逃げ馬が多いほど有利(数値が大きくなる)
					? (float)frontRunnerCount / (float)race.NumberOfHorses
					: 0.5F;
			});

			return features;
		}
	}
}