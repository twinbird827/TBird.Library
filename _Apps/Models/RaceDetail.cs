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
				Tuka = Math.Min(Tuka, 1.0F);
				LastThreeFurlongs = x.Get("上り").Single();
				Gender = x.Get("馬性").Str();
				Age = (Race.RaceDate - BirthDate).TotalDays.Single() / 365F;
				TimeIndex = x.Get("ﾀｲﾑ指数").Single();
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
		public string Gender { get; set; }
		public float TimeIndex { get; }

		private float AverageTimeIndex(IEnumerable<RaceDetail> horses, int take) => horses
				.Select(x => x.TimeIndex < 40 ? 60F : x.TimeIndex) // 40未満は異常値として除外（全体の5%）
				.Take(take)
				.DefaultIfEmpty(60f) // デフォルトは新馬・未勝利平均の60
				.Average();

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

			// タイム指数でAverageRatingを計算（より正確な能力評価）
			AverageRating = AverageTimeIndex(horses, 5);

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
				var lastGrade = horses[0].Race.Grade;
				var gradeChange = Race.Grade - lastGrade;
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
			var tukaMetrics = TukaAnalyzer.AnalyzeTuka(horses, sires, damsires, siredamsires);

			// 購入価格ランク（全レースで有効）
			var avgPurchasePriceInRace = inRaces.Select(r => r.PurchasePrice).DefaultIfEmpty(PurchasePrice).Average();

			// クラス昇級判定
			float CalculateClassUpChallenge()
			{
				if (!horses.Any()) return 0f;
				var lastGrade = horses[0].Race.Grade;
				return (int)Race.Grade > (int)lastGrade ? 1f : 0f;
			}

			// グレード変化（過去3レース平均 - 現在グレード）
			float CalculateGradeChange(List<RaceDetail> horses)
			{
				if (!horses.Any()) return 0f;
				var past3AvgGrade = horses.Take(3)
					.Select(h => h.Race.Grade.GetGradeFeatures())
					.DefaultIfEmpty(Race.Grade.GetGradeFeatures())
					.Average();
				var currentGrade = Race.Grade.GetGradeFeatures();
				return past3AvgGrade - currentGrade;
			}

			// 馬場状態変化
			float CalculateTrackConditionChangeFromLast()
			{
				if (!horses.Any()) return 0f;
				var lastCondition = horses[0].Race.TrackConditionType;
				return Race.TrackConditionType - lastCondition;
			}

			// 性別を数値に変換
			float ConvertGenderToFloat(string gender)
			{
				if (string.IsNullOrEmpty(gender)) return 0f;
				if (gender.Contains("牝")) return 0.5f;  // 牝馬
				if (gender.Contains("セ")) return 1.0f;  // セン馬
				return 0f;  // 牡馬（デフォルト）
			}

			// 経験回数計算
			var sameCourseExperience = horses.Count(h => h.Race.CourseName == Race.CourseName);
			var sameDistanceCategoryExperience = horses.Count(h => h.Race.DistanceCategory == Race.DistanceCategory);
			var sameTrackTypeExperience = horses.Count(h => h.Race.TrackType == Race.TrackType);

			var features = new OptimizedHorseFeaturesModel(this)
			{
				// 基本実績
				Recent3AdjustedAvg = adjustedMetrics.Recent3AdjustedAvg,
				Recent5AdjustedAvg = adjustedMetrics.Recent5AdjustedAvg,
				LastRaceAdjustedScore = adjustedMetrics.LastRaceAdjustedScore,
				AdjustedConsistency = adjustedMetrics.AdjustedConsistency,

				// 条件適性
				CurrentDistanceAptitude = conditionMetrics.CurrentDistanceAptitude,
				CurrentTrackTypeAptitude = conditionMetrics.CurrentTrackTypeAptitude,
				CurrentTrackConditionAptitude = conditionMetrics.CurrentTrackConditionAptitude,

				// 関係者実績
				JockeyRecentInverseAvg = connectionMetrics.JockeyRecentInverseAvg,
				JockeyCurrentConditionAvg = connectionMetrics.JockeyCurrentConditionAvg,
				JockeyDistanceAptitude = connectionMetrics.JockeyDistanceAptitude,
				JockeyTrackConditionAptitude = connectionMetrics.JockeyTrackConditionAptitude,
				JockeyPlaceAptitude = connectionMetrics.JockeyPlaceAptitude,
				TrainerRecentInverseAvg = connectionMetrics.TrainerRecentInverseAvg,
				TrainerCurrentConditionAvg = connectionMetrics.TrainerCurrentConditionAvg,
				TrainerDistanceAptitude = connectionMetrics.TrainerDistanceAptitude,
				TrainerTrackConditionAptitude = connectionMetrics.TrainerTrackConditionAptitude,
				TrainerPlaceAptitude = connectionMetrics.TrainerPlaceAptitude,
				BreederRecentInverseAvg = connectionMetrics.BreederRecentInverseAvg,
				BreederCurrentConditionAvg = connectionMetrics.BreederCurrentConditionAvg,
				SireRecentInverseAvg = connectionMetrics.SireRecentInverseAvg,
				SireCurrentConditionAvg = connectionMetrics.SireCurrentConditionAvg,
				SireDistanceAptitude = connectionMetrics.SireDistanceAptitude,
				SireTrackConditionAptitude = connectionMetrics.SireTrackConditionAptitude,
				SirePlaceAptitude = connectionMetrics.SirePlaceAptitude,
				DamSireRecentInverseAvg = connectionMetrics.DamSireRecentInverseAvg,
				DamSireCurrentConditionAvg = connectionMetrics.DamSireCurrentConditionAvg,
				DamSireDistanceAptitude = connectionMetrics.DamSireDistanceAptitude,
				DamSireTrackConditionAptitude = connectionMetrics.DamSireTrackConditionAptitude,
				DamSirePlaceAptitude = connectionMetrics.DamSirePlaceAptitude,
				SireDamSireRecentInverseAvg = connectionMetrics.SireDamSireRecentInverseAvg,
				SireDamSireCurrentConditionAvg = connectionMetrics.SireDamSireCurrentConditionAvg,
				SireDamSireDistanceAptitude = connectionMetrics.SireDamSireDistanceAptitude,
				SireDamSireTrackConditionAptitude = connectionMetrics.SireDamSireTrackConditionAptitude,
				SireDamSirePlaceAptitude = connectionMetrics.SireDamSirePlaceAptitude,
				JockeyTrainerRecentInverseAvg = connectionMetrics.JockeyTrainerRecentInverseAvg,
				JockeyTrainerCurrentConditionAvg = connectionMetrics.JockeyTrainerCurrentConditionAvg,
				JockeyTrainerDistanceAptitude = connectionMetrics.JockeyTrainerDistanceAptitude,
				JockeyTrainerTrackConditionAptitude = connectionMetrics.JockeyTrainerTrackConditionAptitude,
				JockeyTrainerPlaceAptitude = connectionMetrics.JockeyTrainerPlaceAptitude,

				// 状態・変化
				RestDays = Math.Min((Race.RaceDate - LastRaceDate).Days, 365F),
				IsRentoFlag = (Race.RaceDate - LastRaceDate).Days < 14,  // 中1週以下
				Age = Age,
				Gender = ConvertGenderToFloat(Gender),  // 牡=0, 牝=0.5, セン=1
				Season = (float)((Race.RaceDate.Month - 1) / 3),  // 0=1-3月, 1=4-6月, 2=7-9月, 3=10-12月
				RaceDistance = (float)Race.DistanceCategory,
				PerformanceTrend = adjustedMetrics.Recent3AdjustedAvg / adjustedMetrics.OverallAdjustedAvg,
				DistanceChangeAdaptation = CalculateDistanceChangeAdaptation(),
				ClassChangeAdaptation = CalculateClassChangeAdaptation(),
				JockeyWeightDiff = jockeyWeightMetrics.JockeyWeightDiff,
				JockeyWeightDiffFromAvgInRace = jockeyWeightMetrics.JockeyWeightDiffFromAvgInRace,
				AverageTuka = tukaMetrics.AverageTuka,
				LastRaceTuka = tukaMetrics.LastRaceTuka,
				TukaConsistency = tukaMetrics.TukaConsistency,
				AverageTukaInRace = tukaMetrics.AverageTukaInRace,
				LastRaceFinishPosition = finishPositionMetrics.LastRaceFinishPosition,
				Recent3AvgFinishPosition = finishPositionMetrics.Recent3AvgFinishPosition,
				FinishPositionImprovement = finishPositionMetrics.FinishPositionImprovement,
				PaceAdvantageScore = tukaMetrics.PaceAdvantageScore,
				CurrentGrade = Race.Grade.GetGradeFeatures(),
				ClassUpChallenge = CalculateClassUpChallenge(),
				GradeChange = CalculateGradeChange(horses),
				CurrentTrackCondition = (int)Race.TrackConditionType,
				TrackConditionChangeFromLast = CalculateTrackConditionChangeFromLast(),
				SameCourseExperience = sameCourseExperience,
				SameDistanceCategoryExperience = sameDistanceCategoryExperience,
				SameTrackTypeExperience = sameTrackTypeExperience,

				// タイム関連
				SameDistanceTimeIndex = CalculateSameDistanceTimeIndex(Race.Distance),
				LastRaceTimeDeviation = CalculateLastRaceTimeDeviation(),
				TimeConsistencyScore = CalculateTimeConsistency(),
				AdjustedLastThreeFurlongsAvg = lastThreeFurlongsMetrics.AdjustedLastThreeFurlongsAvg,
				LastRaceAdjustedLastThreeFurlongs = lastThreeFurlongsMetrics.LastRaceAdjustedLastThreeFurlongs,
				AdjustedLastThreeFurlongsDiffFromAvgInRace = lastThreeFurlongsMetrics.AdjustedLastThreeFurlongsDiffFromAvgInRace,
				AverageTimeIndex = AverageTimeIndex(horses, 5),
				LastRaceTimeIndex = AverageTimeIndex(horses, 1),

				// レース内位置情報
				Umaban = Umaban,
				UmabanAdvantage = 1f - ((float)Umaban / (float)Race.NumberOfHorses),

				// メタ情報
				IsNewHorse = RaceCount == 0,
				AptitudeReliability = CalculateAptitudeReliability(),

				// 購入価格ランク（全レースで有効）
				PurchasePriceRank = PurchasePrice / avgPurchasePriceInRace,

				// ラベル（トレーニング時に設定）
				Label = 0, // 実際の着順から計算
			};

			// 新馬専用成績と通常成績を加重平均で組み合わせ (新馬専用70% + 通常30%)
			float GravityCalculate(float newhorse, float regular) => newhorse * 0.7F + regular * 0.3F;

			var newHorseMetrics = MaidenRaceAnalyzer.AnalyzeNewHorse(this, inRaces, horses, jockeys, trainers, breeders, sires, damsires);

			features.TrainerNewHorseInverse = GravityCalculate(newHorseMetrics.TrainerNewHorseInverse, connectionMetrics.TrainerRecentInverseAvg);
			features.JockeyNewHorseInverse = GravityCalculate(newHorseMetrics.JockeyNewHorseInverse, connectionMetrics.JockeyRecentInverseAvg);
			features.SireNewHorseInverse = GravityCalculate(newHorseMetrics.SireNewHorseInverse, connectionMetrics.SireRecentInverseAvg);
			features.DamSireNewHorseInverse = GravityCalculate(newHorseMetrics.DamSireNewHorseInverse, connectionMetrics.DamSireRecentInverseAvg);
			features.BreederNewHorseInverse = GravityCalculate(newHorseMetrics.BreederNewHorseInverse, connectionMetrics.BreederRecentInverseAvg);

			// === 新規追加特徴量の計算 ===

			// 1. 交互作用項
			features.LastRaceScore_X_TimeRank = features.LastRaceAdjustedScore * features.AverageTimeIndexRankInRace;
			features.JockeyPlace_X_TrainerPlace = features.JockeyPlaceAptitude * features.TrainerPlaceAptitude;
			features.JockeyPlace_X_DistanceApt = features.JockeyPlaceAptitude * features.CurrentDistanceAptitude;
			features.LastRaceScore_X_JockeyPlace = features.LastRaceAdjustedScore * features.JockeyPlaceAptitude;
			features.Recent3Avg_X_JockeyRecent = features.Recent3AdjustedAvg * features.JockeyRecentInverseAvg;

			// 2. レース内ランク特徴量（CalculateInRacesメソッドで計算）
			// 初期値は不要（CalculateInRacesで上書きされる）

			// 3. RecentUpwardTrend (着順が改善傾向か)
			if (horses.Count >= 3)
			{
				var recent3Positions = horses.Take(3).Select(h => h.FinishPosition).ToList();
				// 着順は小さい方が良いので、連続して小さくなっている場合に1.0
				features.RecentUpwardTrend = (recent3Positions[0] < recent3Positions[1] && recent3Positions[1] < recent3Positions[2]) ? 1.0f : 0.0f;
			}
			else
			{
				features.RecentUpwardTrend = 0.0f;
			}

			// 4. 騎手×調教師の強化（信頼度重み付け）
			int jockeyTrainerSampleCount = jockeytrainers.Count();
			float confidence = Math.Min(jockeyTrainerSampleCount / 30.0f, 1.0f); // 30レース以上で信頼度MAX
			features.JockeyTrainerDistanceAptitude_Robust =
				(features.JockeyTrainerDistanceAptitude * confidence) +
				((features.JockeyDistanceAptitude + features.TrainerDistanceAptitude) / 2 * (1 - confidence));
			features.JockeyTrainerTrackConditionAptitude_Robust =
				(features.JockeyTrainerTrackConditionAptitude * confidence) +
				((features.JockeyTrackConditionAptitude + features.TrainerTrackConditionAptitude) / 2 * (1 - confidence));
			features.JockeyTrainerPlaceAptitude_Robust =
				(features.JockeyTrainerPlaceAptitude * confidence) +
				((features.JockeyPlaceAptitude + features.TrainerPlaceAptitude) / 2 * (1 - confidence));

			// 5. ターゲットエンコーディング（STEP2で計算）
			features.SeasonTargetEncoded = features.Season; // 初期値
			features.CurrentGradeTargetEncoded = features.CurrentGrade; // 初期値
			features.CurrentTrackConditionTargetEncoded = features.CurrentTrackCondition; // 初期値

			// 6. アンサンブル特徴量
			features.OverallHorseQuality =
				(features.Recent3AdjustedAvg * 0.3f) +
				(features.LastRaceAdjustedScore * 0.3f) +
				(features.AverageTimeIndexRankInRace * 0.4f);
			features.OverallConnectionQuality =
				(features.JockeyPlaceAptitude * 0.4f) +
				(features.TrainerPlaceAptitude * 0.3f) +
				(features.JockeyTrainerPlaceAptitude * 0.3f);

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

			// タイム指数のレース内ランクを計算
			var inraceTimeIndexes = features.Select(r => r.AverageTimeIndex).ToArray();

			features.ForEach(x =>
			{
				// 同レース他馬との補正済み上がり比較
				x.AdjustedLastThreeFurlongsDiffFromAvgInRace = inraceAdjustedLastThreeFurlongsAvgs.Average() / x.AdjustedLastThreeFurlongsAvg;

				// 通過順比較
				x.AverageTukaInRace = inraceAverageTuka.Average();
				x.TukaAdvantage = x.AverageTuka / Math.Max(x.AverageTukaInRace, 0.01f);
				x.PaceAdvantageScore = x.AverageTuka < 0.3F
					// 自分は逃げ→前に行く馬が少ないほど有利(数値が大きくなる)
					? (float)(race.NumberOfHorses - frontRunnerCount) / (float)race.NumberOfHorses
					: 0.6F < x.AverageTuka
					// 自分は追込→逃げ馬が多いほど有利(数値が大きくなる)
					? (float)frontRunnerCount / (float)race.NumberOfHorses
					: 0.5F;

				// ペース×脚質の相性：逃げ馬が多い場合の逃げ馬デメリット、追込馬が多い場合の追込馬デメリットを考慮
				var closerCount = inraceAverageTuka.Count(tuka => tuka > 0.6f);  // 追込馬の数
				x.PaceStyleCompatibility = x.AverageTuka < 0.3F
					// 逃げ: 逃げ馬が多いとペースが速くなりデメリット
					? 1.0f - ((float)frontRunnerCount / (float)race.NumberOfHorses)
					: 0.6F < x.AverageTuka
					// 追込: 逃げ馬が少ないとペースが遅く追込不利、追込馬が多いと展開不利
					? ((float)frontRunnerCount / (float)race.NumberOfHorses) * (1.0f - (float)closerCount / (float)race.NumberOfHorses)
					// 先行・差し: 中間的な脚質は安定
					: 0.7F;

				// タイム指数のレース内ランク（降順、高いほど上位）を正規化（1.0=最良、0.0=最悪）
				var timeIndexRank = inraceTimeIndexes.Count(t => t > x.AverageTimeIndex) + 1;
				var horseCount = features.Count();
				x.AverageTimeIndexRankInRace = horseCount > 1 ? 1.0f - ((timeIndexRank - 1) / (float)(horseCount - 1)) : 0.5f;

				// === 新規ランク特徴量の計算 ===
				// レース内ランク計算ヘルパー（降順: 大きいほど良い）
				float CalculateRankDesc(float value, float[] values)
				{
					if (horseCount <= 1) return 0.5f;
					var rank = values.Count(v => v > value) + 1;
					return 1.0f - ((rank - 1) / (float)(horseCount - 1));
				}

				// レース内ランク計算ヘルパー（昇順: 小さいほど良い）
				float CalculateRankAsc(float value, float[] values)
				{
					if (horseCount <= 1) return 0.5f;
					var rank = values.Count(v => v < value) + 1;
					return 1.0f - ((rank - 1) / (float)(horseCount - 1));
				}

				// 各ランク特徴量を計算
				var jockeyRecentValues = features.Select(f => f.JockeyRecentInverseAvg).ToArray();
				x.JockeyRecentRankInRace = CalculateRankDesc(x.JockeyRecentInverseAvg, jockeyRecentValues);

				var lastRaceScoreValues = features.Select(f => f.LastRaceAdjustedScore).ToArray();
				x.LastRaceScoreRankInRace = CalculateRankDesc(x.LastRaceAdjustedScore, lastRaceScoreValues);

				var ageValues = features.Select(f => f.Age).ToArray();
				x.AgeRankInRace = CalculateRankAsc(x.Age, ageValues); // 若い方が良い

				var restDaysValues = features.Select(f => f.RestDays).ToArray();
				x.RestDaysRankInRace = CalculateRankAsc(x.RestDays, restDaysValues); // 短い方が良い（中間が最適だが簡略化）

				var recent3AvgValues = features.Select(f => f.Recent3AdjustedAvg).ToArray();
				x.Recent3AvgRankInRace = CalculateRankDesc(x.Recent3AdjustedAvg, recent3AvgValues);
			});

			return features;
		}
	}
}