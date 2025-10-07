using Jint.Parser.Ast;
using Microsoft.ML.Data;
using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba
{
	public class STEP2Command : STEPBase
	{
		private Dictionary<string, List<RaceDetail>> _Horses = new();
		private Dictionary<string, List<RaceDetail>> _Jockeys = new();
		private Dictionary<string, List<RaceDetail>> _Trainers = new();
		private Dictionary<string, List<RaceDetail>> _Sires = new();
		private Dictionary<string, List<RaceDetail>> _DamSires = new();
		private Dictionary<string, List<RaceDetail>> _SireDamSires = new();
		private Dictionary<string, List<RaceDetail>> _Breeders = new();
		private Dictionary<string, List<RaceDetail>> _JockeyTrainers = new();

		public STEP2Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var create = VM.S2Overwrite.IsChecked || !await conn.ExistsModelTableAsync();

				if (create)
				{
					// 作成し直すために全ﾃｰﾌﾞﾙDROP
					await conn.DropSTEP2();

					// これまで作成した教育ﾃﾞｰﾀの削除
					AppSetting.Instance.RemoveAllRankingTrain();
				}

				// ﾃｰﾌﾞﾙ作成
				await conn.CreateModel();

				// バッチ処理で訓練データを生成・保存
				await GenerateAndSaveTrainingDataAsync(conn);

				_Horses.Clear();
				_Jockeys.Clear();
				_Trainers.Clear();
				_Sires.Clear();
				_DamSires.Clear();
				_SireDamSires.Clear();
				_Breeders.Clear();
				_JockeyTrainers.Clear();
			}
		}

		/// <summary>
		/// 指定期間のレースデータから訓練データを段階的に生成・保存
		/// </summary>
		public async Task GenerateAndSaveTrainingDataAsync(SQLiteControl conn)
		{
			MainViewModel.AddLog($"訓練データ生成開始");

			var already = conn.GetAlreadyCreatedRacesAsync().ToBlockingEnumerable().ToArray();

			await foreach (var race in conn.GetRaceAsync())
			{
				try
				{
					// 今ﾚｰｽの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

					// 過去ﾚｰｽの結果をｾｯﾄする
					details.ForEach(x => x.Initialize(_Horses.Get(x.Horse, new List<RaceDetail>())));

					// 今ﾚｰｽのﾚｰﾃｨﾝｸﾞ情報をｾｯﾄする
					race.AverageRating = details.Average(x => x.AverageRating);

					if (!already.Contains(race.RaceId))
					{
						// 特徴量を生成
						var results = details.Select(x =>
						{
							var features = x.ExtractFeatures(
								_Horses.Get(x.Horse, new List<RaceDetail>()),
								details,
								_Jockeys.Get(x.Jockey, new List<RaceDetail>()),
								_Trainers.Get(x.Trainer, new List<RaceDetail>()),
								_Breeders.Get(x.Breeder, new List<RaceDetail>()),
								_Sires.Get(x.Sire, new List<RaceDetail>()),
								_DamSires.Get(x.DamSire, new List<RaceDetail>()),
								_SireDamSires.Get(x.SireDamSire, new List<RaceDetail>()),
								_JockeyTrainers.Get(x.JockeyTrainer, new List<RaceDetail>())
							);

							// ラベル生成（難易度調整済み着順スコア）
							features.Label = (x.FinishPosition - 1).Run(x => x < 12 ? x : 11);

							return features;
						}).ToArray().CalculateInRaces(race);

						// ﾃﾞｰﾀﾍﾞｰｽに格納
						await conn.InsertModelAsync(results);
					}

					// 今ﾚｰｽの情報をﾒﾓﾘに格納
					details.ForEach(x =>
					{
						void AddHistory(Dictionary<string, List<RaceDetail>> dic, RaceDetail tgt, string key)
						{
							if (!dic.ContainsKey(key))
							{
								dic.Add(key, new List<RaceDetail>());
							}
							dic[key].Insert(0, tgt);
						}

						AddHistory(_Horses, x, x.Horse);
						AddHistory(_Jockeys, x, x.Jockey);
						AddHistory(_Trainers, x, x.Trainer);
						AddHistory(_Sires, x, x.Sire);
						AddHistory(_DamSires, x, x.DamSire);
						AddHistory(_SireDamSires, x, x.SireDamSire);
						AddHistory(_Breeders, x, x.Breeder);
						AddHistory(_JockeyTrainers, x, x.JockeyTrainer);
					});

					MainViewModel.AddLog($"訓練データ生成完了：{race.RaceId} {race.RaceDate}");
				}
				catch (Exception ex)
				{
					MessageService.Debug(ex.ToString());
				}
			}

			MainViewModel.AddLog($"訓練データ生成完了");
		}
	}

	// ===== 難易度調整済みパフォーマンス計算 =====

	public class AdjustedPerformanceMetrics
	{
		public float Recent3AdjustedAvg { get; set; }
		public float Recent5AdjustedAvg { get; set; }
		public float OverallAdjustedAvg { get; set; }

		//public float BestAdjustedScore { get; set; }
		public float LastRaceAdjustedScore { get; set; }
		public float AdjustedConsistency { get; set; }
		//public float G1AdjustedAvg { get; set; }
		//public float G2G3AdjustedAvg { get; set; }
		//public float OpenAdjustedAvg { get; set; }
	}

	public static class AdjustedPerformanceCalculator
	{
		public static float CalculateAdjustedInverseScore(uint finishPosition, Race race)
		{
			// 基本の逆数スコア
			float baseScore = 1.0f / finishPosition;

			// レース難易度による重み付け
			float difficultyMultiplier = RaceDifficultyAnalyzer.CalculateDifficultyMultiplier(race);

			return baseScore * difficultyMultiplier;
		}

		public static AdjustedPerformanceMetrics CalculateAdjustedPerformance(Race race, RaceDetail detail, List<RaceDetail> results)
		{
			var adjustedScores = results
				.Select(result => result.CalculateAdjustedInverseScore())
				.ToArray();

			return new AdjustedPerformanceMetrics
			{
				Recent3AdjustedAvg = adjustedScores.Take(3).DefaultIfEmpty(0.1f).Average(),
				Recent5AdjustedAvg = adjustedScores.Take(5).DefaultIfEmpty(0.1f).Average(),
				OverallAdjustedAvg = adjustedScores.DefaultIfEmpty(0.1f).Average(),
				//BestAdjustedScore = adjustedScores.DefaultIfEmpty(0.1f).Max(),
				LastRaceAdjustedScore = adjustedScores.FirstOrDefault(0.1f),
				AdjustedConsistency = CalculateConsistency(adjustedScores),
				//G1AdjustedAvg = CalculateGradeSpecificAverage(results, x => x.IsG1()),
				//G2G3AdjustedAvg = CalculateGradeSpecificAverage(results, x => x.IsG2() || x.IsG3()),
				//OpenAdjustedAvg = CalculateGradeSpecificAverage(results, x => x.IsOPEN())
			};
		}

		private static float CalculateConsistency(float[] scores)
		{
			if (scores.Length < 2) return 1.0f;

			var mean = scores.Average();
			if (mean < 0.01f) return 0.1f; // ゼロ除算回避（極端に低い成績）

			var variance = scores.Select(s => (s - mean) * (s - mean)).Average();
			var stdDev = (float)Math.Sqrt(variance);

			// 変動係数(CV)の逆数を0-1範囲に正規化
			// CV = stdDev / mean（相対的なばらつき）
			// 安定している（CVが小さい）ほど1に近く、不安定なほど0に近い
			var cv = stdDev / mean;
			return 1.0f / (1.0f + cv);
		}

		private static float CalculateGradeSpecificAverage(List<RaceDetail> races, Func<GradeType, bool> is_target)
		{
			var targetGrades = EnumUtil.GetValues<GradeType>().Where(is_target);

			return races
				.Where(r => targetGrades.Contains(r.Race.Grade))
				.AdjustedInverseScoreAverage(0.0F);
		}

		// ===== レース難易度分析 =====

		public static class RaceDifficultyAnalyzer
		{
			public static float CalculateDifficultyMultiplier(Race race)
			{
				float multiplier = 1.0f;

				// グレード別基本倍率
				multiplier *= GetGradeMultiplier(race.Grade);

				// 賞金による補正
				multiplier *= CalculatePrizeMultiplier(race.FirstPrizeMoney);

				// 頭数による競争激化度
				multiplier *= CalculateFieldSizeMultiplier(race.NumberOfHorses);

				// 出走馬の平均レーティング
				if (race.AverageRating > 0) multiplier *= CalculateQualityMultiplier(race.AverageRating);

				// 特別条件による補正
				multiplier *= GetSpecialConditionMultiplier(race);

				return multiplier;
			}

			private static float GetGradeMultiplier(GradeType grade)
			{
				return (grade.Single() + 5F) / 10F;
			}

			private static float CalculatePrizeMultiplier(long prizeMoney)
			{
				const long basePrize = 700; // 700万円を基準
				return (float)(Math.Log((double)prizeMoney / basePrize) * 0.2 + 1.0);
			}

			private static float CalculateFieldSizeMultiplier(int numberOfHorses)
			{
				const int baseField = 12; // 12頭を基準
				return (float)(Math.Log((double)numberOfHorses / baseField) * 0.15 + 1.0);
			}

			private static float CalculateQualityMultiplier(float averageRating)
			{
				// 60を基準に、40-100の範囲で補正
				const float baseRating = 60f;
				const float range = 40f; // ±40ポイントの範囲

				// 正規化（-0.5 ～ +1.0程度）
				float normalized = (averageRating - baseRating) / range;

				// 0.9 ～ 1.2の範囲で補正（±20%まで）
				return 1.0f + Math.Max(-0.1f, Math.Min(normalized * 0.2f, 0.2f));
			}

			private static float GetSpecialConditionMultiplier(Race race)
			{
				float multiplier = 1.0f;

				if (race.IsInternational)
					multiplier *= 1.2f;

				if (race.IsAgedHorseRace)
					multiplier *= 1.1f;

				return multiplier;
			}
		}
	}

	// ===== 条件適性計算 =====
	public class ConditionMetrics
	{
		public float CurrentDistanceAptitude { get; set; }
		public float CurrentTrackTypeAptitude { get; set; }
		public float CurrentTrackConditionAptitude { get; set; }
	}

	public static class ConditionAptitudeCalculator
	{
		public static ConditionMetrics CalculateConditionMetrics(List<RaceDetail> horses, Race race)
		{
			return new ConditionMetrics()
			{
				CurrentDistanceAptitude = ConditionAptitudeCalculator.CalculateCurrentConditionAptitude(horses, race),
				CurrentTrackTypeAptitude = ConditionAptitudeCalculator.CalculateTrackTypeAptitude(horses, race.TrackType),
				CurrentTrackConditionAptitude = ConditionAptitudeCalculator.CalculateTrackConditionAptitude(horses, race.TrackConditionType),
			};
		}

		private static float CalculateCurrentConditionAptitude(List<RaceDetail> horses, Race race)
		{
			// 1. 同距離カテゴリ+同芝ダート+同馬場状態
			var level1 = horses.Where(r =>
				r.Race.DistanceCategory == race.DistanceCategory &&
				r.Race.TrackType == race.TrackType &&
				r.Race.TrackConditionType == race.TrackConditionType).ToList();

			// レベル1が十分あれば（5戦以上）それを優先
			if (level1.Count >= 5)
				return level1.AdjustedInverseScoreAverage();

			// 2. 同距離カテゴリ+同芝ダート（馬場状態問わず）
			var level2 = horses.Where(r =>
				r.Race.DistanceCategory == race.DistanceCategory &&
				r.Race.TrackType == race.TrackType).ToList();

			// レベル1が少数ある場合は次レベルと組み合わせ
			if (level1.Any() && level2.Any())
				return level1.AdjustedInverseScoreAverage() * 0.7f
					 + level2.AdjustedInverseScoreAverage() * 0.3f;

			// レベル2のみ
			if (level2.Count >= 5)
				return level2.AdjustedInverseScoreAverage();

			// 3. 同芝ダート（距離問わず）
			var level3 = horses.Where(r =>
				r.Race.TrackType == race.TrackType).ToList();

			// レベル2が少数ある場合は次レベルと組み合わせ
			if (level2.Any() && level3.Any())
				return level2.AdjustedInverseScoreAverage() * 0.7f
					 + level3.AdjustedInverseScoreAverage() * 0.3f;

			// レベル3のみ、または全体平均
			if (level3.Any())
				return level3.AdjustedInverseScoreAverage();

			// 4. 全体平均
			return horses.AdjustedInverseScoreAverage();
		}

		//private static float CalculateDistanceCategoryAptitude(List<RaceDetail> races, DistanceCategory category)
		//{
		//	var categoryRaces = races
		//		.Where(r => r.Race.DistanceCategory == category)
		//		.ToList();

		//	return CalculateAdjustedAverageScore(categoryRaces);
		//}

		private static float CalculateTrackTypeAptitude(List<RaceDetail> races, TrackType type)
		{
			return races.Where(r => r.Race.TrackType == type).AdjustedInverseScoreAverage();
		}

		private static float CalculateTrackConditionAptitude(List<RaceDetail> races, TrackConditionType condition)
		{
			// 1. 完全一致（同じ馬場状態）
			var exactMatches = races.Where(r => r.Race.TrackConditionType == condition).ToList();

			// 完全一致が十分あれば（5戦以上）それを優先
			if (exactMatches.Count >= 5)
				return exactMatches.AdjustedInverseScoreAverage();

			// 2. 類似馬場状態（良+稍重 or 重+不良）
			var similarMatches = races.Where(r =>
			{
				var current = (int)condition;
				var past = (int)r.Race.TrackConditionType;
				// 良(0)と稍重(1)、または重(2)と不良(3)をグループ化
				return (current <= 1 && past <= 1) || (current >= 2 && past >= 2);
			}).ToList();

			// 完全一致が少数ある場合は類似と組み合わせ
			if (exactMatches.Any() && similarMatches.Any())
				return exactMatches.AdjustedInverseScoreAverage() * 0.7f
					 + similarMatches.AdjustedInverseScoreAverage() * 0.3f;

			// 類似のみ、または全体平均
			if (similarMatches.Any())
				return similarMatches.AdjustedInverseScoreAverage();

			// 3. 全体平均
			return races.AdjustedInverseScoreAverage();
		}

		private static float CalculateRelaxedConditionAptitude(List<RaceDetail> raceHistory, Race currentRace)
		{
			// 1. 同距離のみ
			var sameDistance = raceHistory.Where(r => r.Race.Distance == currentRace.Distance);
			if (sameDistance.Any())
				return sameDistance.AdjustedInverseScoreAverage();

			// 2. 同距離カテゴリ
			var sameCategory = raceHistory.Where(r => r.Race.DistanceCategory == currentRace.DistanceCategory);
			if (sameCategory.Any())
				return sameCategory.AdjustedInverseScoreAverage();

			// 3. 全体平均
			return raceHistory.AdjustedInverseScoreAverage();
		}

		private static float CalculateSpecificCourseAptitude(List<RaceDetail> races, string courseName)
		{
			return races.Where(r => r.Race.CourseName == courseName).AdjustedInverseScoreAverage();
		}

	}

	// ===== 関係者実績分析 =====

	public static class ConnectionAnalyzer
	{
		public static ConnectionMetrics AnalyzeConnections(Race upcomingRace, List<RaceDetail> jockeys, List<RaceDetail> trainers, List<RaceDetail> breeders, List<RaceDetail> sires, List<RaceDetail> damsires, List<RaceDetail> siredamsires, List<RaceDetail> jockeytrainers)
		{
			return new ConnectionMetrics
			{
				JockeyRecentInverseAvg = jockeys.Take(30).AdjustedInverseScoreAverage(),
				JockeyCurrentConditionAvg = CalculateConditionSpecific(jockeys, upcomingRace),

				TrainerRecentInverseAvg = trainers.Take(30).AdjustedInverseScoreAverage(),
				TrainerCurrentConditionAvg = CalculateConditionSpecific(trainers, upcomingRace),

				BreederRecentInverseAvg = breeders.Take(30).AdjustedInverseScoreAverage(),
				BreederCurrentConditionAvg = CalculateConditionSpecific(breeders, upcomingRace),

				SireRecentInverseAvg = sires.Take(30).AdjustedInverseScoreAverage(),
				SireCurrentConditionAvg = CalculateConditionSpecific(sires, upcomingRace),
				SireDistanceAptitude = CalculateDistanceSpecific(sires, upcomingRace),

				DamSireRecentInverseAvg = damsires.Take(30).AdjustedInverseScoreAverage(),
				DamSireCurrentConditionAvg = CalculateConditionSpecific(damsires, upcomingRace),
				DamSireDistanceAptitude = CalculateDistanceSpecific(damsires, upcomingRace),

				SireDamSireRecentInverseAvg = siredamsires.Take(30).AdjustedInverseScoreAverage(),
				SireDamSireCurrentConditionAvg = CalculateConditionSpecific(siredamsires, upcomingRace),
				SireDamSireDistanceAptitude = CalculateDistanceSpecific(siredamsires, upcomingRace),

				JockeyTrainerRecentInverseAvg = jockeytrainers.Take(30).AdjustedInverseScoreAverage(),
				JockeyTrainerCurrentConditionAvg = CalculateConditionSpecific(jockeytrainers, upcomingRace),
			};
		}

		private static float CalculateConditionSpecific(IEnumerable<RaceDetail> races, Race upcomingRace)
		{
			return races
				.Where(r => r.Race.DistanceCategory == upcomingRace.DistanceCategory && r.Race.TrackType == upcomingRace.TrackType)
				.AdjustedInverseScoreAverage();
		}

		private static float CalculateDistanceSpecific(IEnumerable<RaceDetail> races, Race upcomingRace)
		{
			return races
				.Where(r => r.Race.DistanceCategory == upcomingRace.DistanceCategory)
				.AdjustedInverseScoreAverage();
		}
	}

	public class ConnectionMetrics
	{
		public float JockeyRecentInverseAvg { get; set; }
		public float JockeyCurrentConditionAvg { get; set; }

		public float TrainerRecentInverseAvg { get; set; }
		public float TrainerCurrentConditionAvg { get; set; }

		public float BreederRecentInverseAvg { get; set; }
		public float BreederCurrentConditionAvg { get; set; }

		public float SireRecentInverseAvg { get; set; }
		public float SireCurrentConditionAvg { get; set; }
		public float SireDistanceAptitude { get; set; }

		public float DamSireRecentInverseAvg { get; set; }
		public float DamSireCurrentConditionAvg { get; set; }
		public float DamSireDistanceAptitude { get; set; }

		public float SireDamSireRecentInverseAvg { get; set; }
		public float SireDamSireCurrentConditionAvg { get; set; }
		public float SireDamSireDistanceAptitude { get; set; }

		public float JockeyTrainerRecentInverseAvg { get; set; }
		public float JockeyTrainerCurrentConditionAvg { get; set; }
	}

	// ===== 新馬・未勝利戦対応 =====

	public static class MaidenRaceAnalyzer
	{
		public static NewHorseMetrics AnalyzeNewHorse(RaceDetail detail, RaceDetail[] inraces, List<RaceDetail> horses, List<RaceDetail> jockeys, List<RaceDetail> trainers, List<RaceDetail> breeders, List<RaceDetail> sires, List<RaceDetail> damsires)
		{
			float CalculateNewHorseInverse(List<RaceDetail> arr) => arr
				.Where(r => r.RaceCount == 0)
				.Take(30)
				.AdjustedInverseScoreAverage();

			return new NewHorseMetrics
			{
				TrainerNewHorseInverse = CalculateNewHorseInverse(trainers),
				JockeyNewHorseInverse = CalculateNewHorseInverse(jockeys),
				SireNewHorseInverse = CalculateNewHorseInverse(sires),
				DamSireNewHorseInverse = CalculateNewHorseInverse(damsires),
				BreederNewHorseInverse = CalculateNewHorseInverse(breeders),
			};
		}
	}

	public class NewHorseMetrics
	{
		public float TrainerNewHorseInverse { get; set; }
		public float JockeyNewHorseInverse { get; set; }
		public float SireNewHorseInverse { get; set; }
		public float DamSireNewHorseInverse { get; set; }
		public float BreederNewHorseInverse { get; set; }
	}

	public static class LastThreeFurlongsAnalyzer
	{
		public static LastThreeFurlongsMetrics AnalyzeLastThreeFurlongs(RaceDetail detail, List<RaceDetail> horses)
		{
			float GetAverage(int take) => horses.Take(take).Select(CalculateAdjustedLastThreeFurlongs).DefaultIfEmpty(35F).Average();

			var result = new LastThreeFurlongsMetrics()
			{
				AdjustedLastThreeFurlongsAvg = GetAverage(5),
				LastRaceAdjustedLastThreeFurlongs = GetAverage(1),
				AdjustedLastThreeFurlongsDiffFromAvgInRace = 0,
			};
			return result;
		}

		private static float CalculateAdjustedLastThreeFurlongs(RaceDetail detail)
		{
			// 37秒を超える上がりは異常値（騎手が諦めて流した等）としてキャップ
			var cappedTime = Math.Min(detail.LastThreeFurlongs, 37.0f);

			// 通過順補正: 前方(Tuka小)ほど脚を使っているので、より速く補正
			// Tuka=0.2(前方) → correction=0.8、Tuka=0.8(後方) → correction=0.2
			var positionCorrection = (1.0f - detail.Tuka); // 前方ほど大きく、後方ほど小さく

			// 距離補正: 短距離ほど上がりが重要
			var distanceFactor = detail.Race.Distance <= 1400 ? 1.2f :
								 detail.Race.Distance <= 1800 ? 1.0f : 0.8f;

			// 補正済み上がり = 実際の上がり - 補正値（マイナスすることで速くなる）
			// 前方にいた馬ほど補正値が大きく、上がりタイムが速く補正される
			// 後方にいた馬ほど補正値が小さく、実際の上がりに近い値になる
			return cappedTime - (positionCorrection * distanceFactor);
		}

	}

	public class LastThreeFurlongsMetrics
	{
		public float AdjustedLastThreeFurlongsAvg { get; set; }
		public float LastRaceAdjustedLastThreeFurlongs { get; set; }
		public float AdjustedLastThreeFurlongsDiffFromAvgInRace { get; set; }
	}

	public static class JockeyWeightAnalyzer
	{
		public static JockeyWeightMetrics AnalyzeJockeyWeight(RaceDetail detail, List<RaceDetail> horses, RaceDetail[] inraces)
		{
			var inraceweights = inraces.Select(r => r.JockeyWeight).ToArray();

			return new JockeyWeightMetrics()
			{
				JockeyWeightDiff = horses.Any() ? detail.JockeyWeight - horses[0].JockeyWeight : 0f,
				JockeyWeightRankInRace = inraceweights.DefaultIfEmpty(detail.Race.NumberOfHorses / 2).Count(w => w < detail.JockeyWeight) + 1f,
				JockeyWeightDiffFromAvgInRace = detail.JockeyWeight / inraceweights.DefaultIfEmpty(detail.JockeyWeight).Average()
			};
		}
	}

	public class JockeyWeightMetrics
	{
		public float JockeyWeightDiff { get; set; }
		public float JockeyWeightRankInRace { get; set; }
		public float JockeyWeightDiffFromAvgInRace { get; set; }
	}

	public static class FinishPositionAnalyzer
	{
		public static FinishPositionMetrics AnalyzeFinishPosition(List<RaceDetail> horses, Race race)
		{
			var positions = horses.Select(x => (float)x.FinishPosition).ToArray();
			var def = race.NumberOfHorses / 2f;

			return new FinishPositionMetrics()
			{
				LastRaceFinishPosition = horses.Any() ? positions[0] : def,
				Recent3AvgFinishPosition = positions.Take(3).DefaultIfEmpty(def).Average(),
				FinishPositionImprovement = positions.Length >= 2
					? positions[1] - positions[0]
					: 0f,
				LastRaceFinishPositionNormalized = horses.Any()
					? positions[0] / (float)horses[0].Race.NumberOfHorses
					: 0.5f,
			};
		}
	}

	public class FinishPositionMetrics
	{
		public float LastRaceFinishPosition { get; set; }
		public float Recent3AvgFinishPosition { get; set; }
		public float FinishPositionImprovement { get; set; }
		public float LastRaceFinishPositionNormalized { get; set; }
	}

	public static class TukaAnalyzer
	{
		public static TukaMetrics AnalyzeTuka(List<RaceDetail> horses, List<RaceDetail> sires, List<RaceDetail> damsires, List<RaceDetail> siredamsires)
		{
			var tmp = horses.Any()
				? horses
				: siredamsires.Any()
				? siredamsires
				: sires.Any()
				? sires
				: damsires;
			var tukas = tmp.Select(x => x.Tuka).Take(5).ToArray();

			return new TukaMetrics()
			{
				AverageTuka = tukas.DefaultIfEmpty(0.5F).Average(),
				LastRaceTuka = tukas.Take(1).DefaultIfEmpty(0.5F).Average(),
				TukaConsistency = CalculateTukaConsistency(tukas),
				// 以下2項目は後で設定する
				AverageTukaInRace = 0F,
				PaceAdvantageScore = 0F,
			};
		}

		private static float CalculateTukaConsistency(float[] tukas)
		{
			if (tukas.Length < 2) return 1.0f;

			var stdDev = AppUtil.CalculateStandardDeviation(tukas);

			// 標準偏差を0～1の範囲に正規化
			// 標準偏差が小さい（一貫性が高い）ほど1に近く、大きい（バラバラ）ほど0に近い
			return 1.0f - Math.Min(stdDev, 1.0f);
		}
	}

	public class TukaMetrics
	{
		public float AverageTuka { get; set; }
		public float LastRaceTuka { get; set; }
		public float TukaConsistency { get; set; }
		public float AverageTukaInRace { get; set; }
		public float PaceAdvantageScore { get; set; }
	}

	public static partial class SQLite3Extensions
	{
		/// <summary>
		/// 教育ﾃﾞｰﾀ作成用ﾃｰﾌﾞﾙを削除します。
		/// </summary>
		/// <param name="conn"></param>
		/// <returns></returns>
		public static async Task DropSTEP2(this SQLiteControl conn)
		{
			await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
		}

		public static async Task CreateModel(this SQLiteControl conn)
		{
			await conn.Create(
				"t_model",
				typeof(OptimizedHorseFeaturesModel).GetPropertiesEX().Select(x => new Column()
				{
					Name = x.Name,
					Type = x.GetTypeString(),
					IsKey = new[] { "RaceId", "Horse" }.Contains(x.Name)
				}).ToArray()
			);

			// TODO indexの作成
		}

		public static async Task<bool> ExistsModelTableAsync(this SQLiteControl conn)
		{
			return await conn.ExistsColumn("t_model", "RaceId");
		}

		public static async Task InsertModelAsync(this SQLiteControl conn, IEnumerable<OptimizedHorseFeaturesModel> data)
		{
			if (!data.Any()) return;

			foreach (var chunk in data.GroupBy(x => x.RaceId))
			{
				await conn.BeginTransaction();
				foreach (var x in chunk)
				{
					var properties = x.GetPropertiesEX();
					var parameters = properties
						.Select(p => SQLiteUtil.CreateParameter(p.GetDBType(), p.Property.GetValue(x)))
						.ToArray();
					var items = properties.Select(p => p.Name).GetString(",");
					var values = properties.Select(p => "?").GetString(",");
					await conn.ExecuteNonQueryAsync($"REPLACE INTO t_model ({items}) VALUES ({values})", parameters);
				}
				conn.Commit();
			}
		}

		public static async Task<bool> ExistsModelAsync(this SQLiteControl conn, string raceid)
		{
			var cnt = await conn.ExecuteScalarAsync(
				"SELECT COUNT(*) FROM t_orig_h WHERE ﾚｰｽID = ?",
				SQLiteUtil.CreateParameter(DbType.String, raceid)
			).RunAsync(x => x.GetInt32());
			return 0 < cnt;
		}

		public static async IAsyncEnumerable<Race> GetRaceAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT h.ﾚｰｽID, h.ﾚｰｽ名, h.開催場所, h.距離, h.馬場, h.馬場状態, h.ﾗﾝｸ1, h.優勝賞金, h.開催日, h.頭数
FROM   t_orig_h h
WHERE  CAST(h.障害 AS INTEGER) = 0
ORDER BY h.開催日, h.ﾚｰｽID
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return new Race(x);
			}
		}

		public static async IAsyncEnumerable<RaceDetail> GetRaceDetailsAsync(this SQLiteControl conn, Race race)
		{
			var sql = @"
SELECT d.ﾚｰｽID, d.馬番, d.馬ID, d.騎手ID, d.調教師ID, u.父ID, u.母父ID, u.生産者ID, d.着順, d.ﾀｲﾑ変換, d.賞金, u.評価額, u.生年月日, d.斤量, d.通過, d.上り, d.馬性, d.ﾀｲﾑ指数, d.着差
FROM v_orig_d d, t_uma u
WHERE d.ﾚｰｽID = ? AND d.馬ID = u.馬ID
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.String, race.RaceId),
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				yield return new RaceDetail(x, race);
			}
		}

		public static async IAsyncEnumerable<string> GetAlreadyCreatedRacesAsync(this SQLiteControl conn)
		{
			var sql = @"
SELECT DISTINCT RaceId
FROM   t_model h
";

			foreach (var x in await conn.GetRows(sql))
			{
				yield return x["RaceId"].Str();
			}
		}

		public static IEnumerable<CustomProperty> GetPropertiesEX(this object value)
		{
			return value.GetType().GetPropertiesEX();
		}

		public static IEnumerable<CustomProperty> GetPropertiesEX(this Type type)
		{
			return type.GetProperties()
				.Select(p => new CustomProperty(
					p,
					p.Name,
					p.PropertyType,
					p.GetCustomAttribute<LoadColumnAttribute>()
				));
		}

		public class CustomProperty
		{
			public CustomProperty(PropertyInfo property, string name, Type type, LoadColumnAttribute? attribute)
			{
				Property = property;
				Name = name;
				Type = type;
				Attribute = attribute;
			}

			public PropertyInfo Property { get; set; }
			public string Name { get; set; }
			public Type Type { get; set; }
			public LoadColumnAttribute? Attribute { get; set; }

			public string GetTypeString() => Type.Name switch
			{
				"Single" => "REAL",
				"UInt32" => "INTEGER",
				"Int32" => "INTEGER",
				"Boolean" => "INTEGER",
				_ => "TEXT"
			};

			public DbType GetDBType() => Type.Name switch
			{
				"Single" => DbType.Single,
				"UInt32" => DbType.Int32,
				"Int32" => DbType.Int32,
				"Boolean" => DbType.Int32,
				_ => DbType.String
			};

			public void SetProperty(OptimizedHorseFeaturesModel instance, Dictionary<string, object> x)
			{
				switch (Type.Name)
				{
					case "Single":
						Property.SetValue(instance, (float)x[Name]);
						break;
					case "UInt32":
						Property.SetValue(instance, (uint)x[Name]);
						break;
					case "Int32":
						Property.SetValue(instance, (int)x[Name]);
						break;
					case "Boolean":
						Property.SetValue(instance, (int)x[Name] > 0);
						break;
					default:
						Property.SetValue(instance, (string)x[Name]);
						break;
				}
			}
		}
	}

}