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

		public OptimizedHorseFeaturesModel ExtractFeatures(List<RaceDetail> horses, RaceDetail[] inRaces, List<RaceDetail> jockeys, List<RaceDetail> trainers, List<RaceDetail> sires, List<RaceDetail> damsires, List<RaceDetail> siredamsires, List<RaceDetail> breeders)
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

			float CalculateStandardDeviation(float[] values)
			{
				if (values.Length < 2) return 1.0f;
				var mean = values.Average();
				var variance = values.Select(v => (v - mean) * (v - mean)).Average();
				return (float)Math.Sqrt(variance);
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
				var stdDev = CalculateStandardDeviation(timeDeviations.ToArray());
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

			float CalculateAdjustedLastThreeFurlongs(RaceDetail detail)
			{
				// 通過順補正: 前方(Tuka小)ほど脚を使っているので、より速く補正
				// Tuka=0.2(前方) → correction=0.8、Tuka=0.8(後方) → correction=0.2
				var positionCorrection = (1.0f - detail.Tuka); // 前方ほど大きく、後方ほど小さく

				// 距離補正: 短距離ほど上がりが重要
				var distanceFactor = detail.Race.Distance <= 1400 ? 1.2f :
									 detail.Race.Distance <= 1800 ? 1.0f : 0.8f;

				// 補正済み上がり = 実際の上がり - 補正値（マイナスすることで速くなる）
				// 前方にいた馬ほど補正値が大きく、上がりタイムが速く補正される
				// 後方にいた馬ほど補正値が小さく、実際の上がりに近い値になる
				return detail.LastThreeFurlongs - (positionCorrection * distanceFactor);
			}

			var adjustedMetrics = AdjustedPerformanceCalculator.CalculateAdjustedPerformance(Race, this, horses);
			var connectionMetrics = ConnectionAnalyzer.AnalyzeConnections(Race, jockeys, trainers, breeders, sires, damsires, siredamsires);
			var conditionMetrics = ConditionAptitudeCalculator.CalculateConditionMetrics(horses, Race);

			var jockeyWeightDiff = horses.Any() ? JockeyWeight - horses.First().JockeyWeight : 0f;

			// 通過順関連
			var averageTuka = horses.Any() ? horses.Take(5).Average(h => h.Tuka) : 0.5f;
			var lastRaceTuka = horses.Any() ? horses.First().Tuka : 0.5f;
			var tukaConsistency = horses.Count >= 2
				? 1.0f / (CalculateStandardDeviation(horses.Take(5).Select(h => h.Tuka).ToArray()) + 0.01f)
				: 1.0f;
			var averageTukaInRace = inRaces.Any() ? inRaces.Average(r => r.Tuka) : 0.5f;

			// 同レース他馬との斤量比較
			var inRaceWeights = inRaces.Select(r => r.JockeyWeight).ToArray();
			var avgWeightInRace = inRaceWeights.Any() ? inRaceWeights.Average() : JockeyWeight;
			var jockeyWeightRankInRace = inRaceWeights.Any()
				? inRaceWeights.Count(w => w < JockeyWeight) + 1f
				: Race.NumberOfHorses / 2f;
			var jockeyWeightDiffFromAvgInRace = JockeyWeight - avgWeightInRace;

			// 補正済み上がり3ハロン関連
			var adjustedLastThreeFurlongsAvg = horses.Any()
				? horses.Take(5).Select(h => CalculateAdjustedLastThreeFurlongs(h)).Average()
				: 0f;
			var lastRaceAdjustedLastThreeFurlongs = horses.Any()
				? CalculateAdjustedLastThreeFurlongs(horses.First())
				: 0f;

			// 同レース他馬との補正済み上がり比較
			var inRaceAdjustedLastThreeFurlongs = inRaces.Select(r => CalculateAdjustedLastThreeFurlongs(r)).ToArray();
			var avgAdjustedLastThreeFurlongsInRace = inRaceAdjustedLastThreeFurlongs.Any()
				? inRaceAdjustedLastThreeFurlongs.Average()
				: adjustedLastThreeFurlongsAvg;
			var currentAdjustedLastThreeFurlongs = adjustedLastThreeFurlongsAvg;
			var adjustedLastThreeFurlongsRankInRace = inRaceAdjustedLastThreeFurlongs.Any()
				? inRaceAdjustedLastThreeFurlongs.Count(f => f > currentAdjustedLastThreeFurlongs) + 1f
				: Race.NumberOfHorses / 2f;
			var adjustedLastThreeFurlongsDiffFromAvgInRace = currentAdjustedLastThreeFurlongs - avgAdjustedLastThreeFurlongsInRace;

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
				JockeyOverallInverseAvg = connectionMetrics.JockeyOverallInverseAvg,
				JockeyRecentInverseAvg = connectionMetrics.JockeyRecentInverseAvg,
				JockeyCurrentConditionAvg = connectionMetrics.JockeyCurrentConditionAvg,
				TrainerOverallInverseAvg = connectionMetrics.TrainerOverallInverseAvg,
				TrainerRecentInverseAvg = connectionMetrics.TrainerRecentInverseAvg,
				TrainerCurrentConditionAvg = connectionMetrics.TrainerCurrentConditionAvg,
				BreederOverallInverseAvg = connectionMetrics.BreederOverallInverseAvg,
				BreederRecentInverseAvg = connectionMetrics.BreederRecentInverseAvg,
				BreederCurrentConditionAvg = connectionMetrics.BreederCurrentConditionAvg,
				SireOverallInverseAvg = connectionMetrics.SireOverallInverseAvg,
				SireRecentInverseAvg = connectionMetrics.SireRecentInverseAvg,
				SireCurrentConditionAvg = connectionMetrics.SireCurrentConditionAvg,
				DamSireOverallInverseAvg = connectionMetrics.DamSireOverallInverseAvg,
				DamSireRecentInverseAvg = connectionMetrics.DamSireRecentInverseAvg,
				DamSireCurrentConditionAvg = connectionMetrics.DamSireCurrentConditionAvg,
				SireDamSireOverallInverseAvg = connectionMetrics.SireDamSireOverallInverseAvg,
				SireDamSireRecentInverseAvg = connectionMetrics.SireDamSireRecentInverseAvg,
				SireDamSireCurrentConditionAvg = connectionMetrics.SireDamSireCurrentConditionAvg,

				// 状態・変化
				RestDays = (float)(Race.RaceDate - LastRaceDate).Days,
				Age = Age,
				Popularity = 1F,
				PerformanceTrend = adjustedMetrics.Recent3AdjustedAvg - adjustedMetrics.OverallAdjustedAvg,
				DistanceChangeAdaptation = CalculateDistanceChangeAdaptation(),
				ClassChangeAdaptation = CalculateClassChangeAdaptation(),
				JockeyWeightDiff = jockeyWeightDiff,
				JockeyWeightRankInRace = jockeyWeightRankInRace,
				JockeyWeightDiffFromAvgInRace = jockeyWeightDiffFromAvgInRace,
				AverageTuka = averageTuka,
				LastRaceTuka = lastRaceTuka,
				TukaConsistency = tukaConsistency,
				AverageTukaInRace = averageTukaInRace,

				// タイム関連
				SameDistanceTimeIndex = CalculateSameDistanceTimeIndex(Race.Distance),
				LastRaceTimeDeviation = CalculateLastRaceTimeDeviation(),
				TimeConsistencyScore = CalculateTimeConsistency(),
				AdjustedLastThreeFurlongsAvg = adjustedLastThreeFurlongsAvg,
				LastRaceAdjustedLastThreeFurlongs = lastRaceAdjustedLastThreeFurlongs,
				AdjustedLastThreeFurlongsRankInRace = adjustedLastThreeFurlongsRankInRace,
				AdjustedLastThreeFurlongsDiffFromAvgInRace = adjustedLastThreeFurlongsDiffFromAvgInRace,

				// メタ情報
				IsNewHorse = RaceCount == 0,
				HasRaceExperience = RaceCount > 0,
				AptitudeReliability = CalculateAptitudeReliability(),

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
				features.PurchasePriceRank = newHorseMetrics.PurchasePriceRank;
			}

			return features;

		}
	}
}