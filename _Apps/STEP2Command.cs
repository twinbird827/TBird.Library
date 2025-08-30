using HorseRacingPrediction;
using System;
using System.Collections.Generic;
using System.Data;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;
using TBird.DB;
using System.DirectoryServices.ActiveDirectory;
using System.Text.Json;

namespace Netkeiba
{
	public class STEP2Command : STEPBase
	{
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
				}

				// ﾃｰﾌﾞﾙ作成
				await conn.CreateModel();

				using (var repo = new SQLiteRepository())
				{
					await repo.LoadDataAsync();

					var gene = new TrainingDataGenerator(repo);
					var data = gene.GenerateTrainingDataAsync(DateTime.Now.AddYears(-2), DateTime.Now.AddMonths(-1));

					var report = gene.ValidateTrainingData(data);
					report.PrintReport();
					if (!report.IsValid)
					{
						MainViewModel.AddLog("⚠️ データ品質に問題があります。修正してください。");
					}

					await conn.InsertModelAsync(data);
				}
			}
		}

		// ===== 訓練データ生成システム =====

		public class TrainingDataGenerator
		{
			private readonly IDataRepository _dataRepository;

			public TrainingDataGenerator(IDataRepository dataRepository)
			{
				_dataRepository = dataRepository ?? throw new ArgumentNullException(nameof(dataRepository));
			}

			/// <summary>
			/// 指定期間のレースデータから訓練データを生成
			/// </summary>
			public List<OptimizedHorseFeaturesModel> GenerateTrainingDataAsync(
				DateTime startDate,
				DateTime endDate,
				int minRaceCount = 2,
				bool includeNewHorses = true)
			{
				MainViewModel.AddLog($"訓練データ生成開始: {startDate:yyyy-MM-dd} ～ {endDate:yyyy-MM-dd}");

				var trainingData = new List<OptimizedHorseFeaturesModel>();
				var races = _dataRepository.GetRacesAsync(startDate, endDate);

				var processedCount = 0;
				var totalRaces = races.Count;

				foreach (var race in races.OrderBy(r => r.RaceDate))
				{
					try
					{
						var raceTrainingData = GenerateRaceTrainingDataAsync(race);
						trainingData.AddRange(raceTrainingData);

						processedCount++;
						if (processedCount % 100 == 0)
						{
							MainViewModel.AddLog($"進捗: {processedCount}/{totalRaces} レース処理完了");
						}
					}
					catch (Exception ex)
					{
						MainViewModel.AddLog($"レース {race.RaceId} の処理でエラー: {ex.Message}");
						continue;
					}
				}

				MainViewModel.AddLog($"訓練データ生成完了: {trainingData.Count} 件のデータを生成");
				return trainingData;
			}

			/// <summary>
			/// 単一レースから訓練データを生成
			/// </summary>
			private List<OptimizedHorseFeaturesModel> GenerateRaceTrainingDataAsync(Race race)
			{
				var raceTrainingData = new List<OptimizedHorseFeaturesModel>();
				var raceResults = _dataRepository.GetRaceResultsAsync(race.RaceId);

				// 各馬の特徴量を生成
				foreach (var result in raceResults)
				{
					// TODO osoi
					var raceHistory = _dataRepository.GetHorseHistoryBeforeAsync(
						result.HorseName, race.RaceDate
					);
					var horse = CreateHorseFromResultAsync(result, race.RaceDate, raceHistory);

					// 他の出走馬も設定（人気順計算等に必要）
					// TODO osoi
					var allHorsesInRace = CreateAllHorsesInRaceAsync(raceResults, race.RaceDate);

					// 関係者情報
					var jockeyRaces = _dataRepository.GetJockeyRecentRaces(result.JockeyName, race.RaceDate, 100);
					var trainerRaces = _dataRepository.GetTrainerRecentRaces(result.TrainerName, race.RaceDate, 100);

					// 特徴量抽出
					var features = FeatureExtractor.ExtractFeatures(horse, race, allHorsesInRace, _dataRepository);

					// ラベル生成（難易度調整済み着順スコア）
					features.Label = result.CalculateAdjustedInverseScore(race);
					features.RaceId = race.RaceId;

					raceTrainingData.Add(features);
				}

				return raceTrainingData;
			}

			/// <summary>
			/// レース結果から馬オブジェクトを作成
			/// </summary>
			private Horse CreateHorseFromResultAsync(RaceData result, DateTime raceDate, IEnumerable<RaceResult> raceHistory)
			{
				var horseDetails = _dataRepository.GetHorseDetailsAsync(result.HorseName, raceDate);

				return new Horse(result, horseDetails, raceHistory);
			}

			/// <summary>
			/// レース内の全馬を作成（相対指標計算用）
			/// </summary>
			private List<Horse> CreateAllHorsesInRaceAsync(List<RaceData> results, DateTime raceDate)
			{
				var horses = new List<Horse>();

				foreach (var result in results)
				{
					var horse = CreateHorseFromResultAsync(result, raceDate, Enumerable.Empty<RaceResult>());
					horses.Add(horse);
				}

				return horses;
			}

			/// <summary>
			/// 訓練データの品質チェック
			/// </summary>
			public TrainingDataQualityReport ValidateTrainingData(List<OptimizedHorseFeaturesModel> trainingData)
			{
				var report = new TrainingDataQualityReport();

				if (!trainingData.Any())
				{
					report.IsValid = false;
					report.Issues.Add("訓練データが空です");
					return report;
				}

				// 基本統計
				report.TotalRecords = trainingData.Count;
				report.UniqueRaces = trainingData.Select(d => d.RaceId).Distinct().Count();
				report.NewHorseRatio = trainingData.Count(d => d.IsNewHorse) / (float)trainingData.Count;

				// ラベル分布チェック
				var labels = trainingData.Select(d => d.Label).ToArray();
				report.LabelMin = labels.Min();
				report.LabelMax = labels.Max();
				report.LabelMean = labels.Average();
				report.LabelStdDev = CalculateStandardDeviation(labels);

				// 欠損値チェック
				CheckMissingValues(trainingData, report);

				// 異常値チェック
				CheckOutliers(trainingData, report);

				// 特徴量相関チェック
				CheckFeatureCorrelation(trainingData, report);

				report.IsValid = !report.Issues.Any();

				return report;
			}

			private void CheckMissingValues(List<OptimizedHorseFeaturesModel> data, TrainingDataQualityReport report)
			{
				var missingChecks = new Dictionary<string, Func<OptimizedHorseFeatures, bool>>
				{
					["Recent3AdjustedAvg"] = f => f.Recent3AdjustedAvg == 0,
					["CurrentDistanceAptitude"] = f => f.CurrentDistanceAptitude == 0,
					["JockeyCurrentConditionAvg"] = f => f.JockeyCurrentConditionAvg == 0,
					["TrainerCurrentConditionAvg"] = f => f.TrainerCurrentConditionAvg == 0
				};

				foreach (var check in missingChecks)
				{
					var missingCount = data.Count(check.Value);
					var missingRatio = missingCount / (float)data.Count;

					if (missingRatio > 0.3f) // 30%以上欠損
					{
						report.Issues.Add($"{check.Key}: {missingRatio:P1} のデータが欠損");
					}

					report.MissingValueRatios[check.Key] = missingRatio;
				}
			}

			private void CheckOutliers(List<OptimizedHorseFeaturesModel> data, TrainingDataQualityReport report)
			{
				// 異常に高いスコアをチェック
				var highScores = data.Where(d => d.Recent3AdjustedAvg > 2.0f).ToList();
				if (highScores.Count > data.Count * 0.05f) // 5%以上
				{
					report.Issues.Add($"異常に高いスコアが {highScores.Count} 件あります");
				}

				// 異常に古い馬をチェック
				var oldHorses = data.Where(d => d.Age > 8).ToList();
				if (oldHorses.Count > data.Count * 0.02f) // 2%以上
				{
					report.Issues.Add($"8歳以上の馬が {oldHorses.Count} 件あります");
				}

				//// 異常な体重変化をチェック
				//var extremeWeightChanges = data.Where(d => Math.Abs(d.WeightChange) > 20).ToList();
				//if (extremeWeightChanges.Count > data.Count * 0.02f) // 2%以上
				//{
				//	report.Issues.Add($"20kg以上の体重変化が {extremeWeightChanges.Count} 件あります");
				//}
			}

			private void CheckFeatureCorrelation(List<OptimizedHorseFeaturesModel> data, TrainingDataQualityReport report)
			{
				// 高相関の特徴量ペアをチェック
				var recent3Avg = data.Select(d => d.Recent3AdjustedAvg).ToArray();
				var recent5Avg = data.Select(d => d.Recent5AdjustedAvg).ToArray();

				var correlation = CalculateCorrelation(recent3Avg, recent5Avg);
				if (correlation > 0.95f)
				{
					report.Issues.Add($"Recent3AdjustedAvgとRecent5AdjustedAvgの相関が高すぎます: {correlation:F3}");
				}
			}

			private float CalculateStandardDeviation(float[] values)
			{
				var mean = values.Average();
				var variance = values.Select(v => (v - mean) * (v - mean)).Average();
				return (float)Math.Sqrt(variance);
			}

			private float CalculateCorrelation(float[] x, float[] y)
			{
				if (x.Length != y.Length) return 0;

				var meanX = x.Average();
				var meanY = y.Average();

				var numerator = x.Zip(y, (xi, yi) => (xi - meanX) * (yi - meanY)).Sum();
				var denomX = Math.Sqrt(x.Select(xi => (xi - meanX) * (xi - meanX)).Sum());
				var denomY = Math.Sqrt(y.Select(yi => (yi - meanY) * (yi - meanY)).Sum());

				return (float)(numerator / (denomX * denomY));
			}
		}

		// ===== 品質レポート =====

		public class TrainingDataQualityReport
		{
			public bool IsValid { get; set; } = true;
			public List<string> Issues { get; set; } = new();
			public int TotalRecords { get; set; }
			public int UniqueRaces { get; set; }
			public float NewHorseRatio { get; set; }
			public float LabelMin { get; set; }
			public float LabelMax { get; set; }
			public float LabelMean { get; set; }
			public float LabelStdDev { get; set; }
			public Dictionary<string, float> MissingValueRatios { get; set; } = new();

			public void PrintReport()
			{
				MainViewModel.AddLog("=== 訓練データ品質レポート ===");
				MainViewModel.AddLog($"総レコード数: {TotalRecords:N0}");
				MainViewModel.AddLog($"ユニークレース数: {UniqueRaces:N0}");
				MainViewModel.AddLog($"新馬比率: {NewHorseRatio:P1}");
				MainViewModel.AddLog($"ラベル統計: Min={LabelMin:F3}, Max={LabelMax:F3}, Mean={LabelMean:F3}, StdDev={LabelStdDev:F3}");

				if (MissingValueRatios.Any())
				{
					MainViewModel.AddLog("\n欠損値比率:");
					foreach (var missing in MissingValueRatios.Where(m => m.Value > 0))
					{
						MainViewModel.AddLog($"  {missing.Key}: {missing.Value:P1}");
					}
				}

				if (Issues.Any())
				{
					MainViewModel.AddLog("⚠️ 品質上の問題:");
					foreach (var issue in Issues)
					{
						MainViewModel.AddLog($"  - {issue}");
					}
				}
				else
				{
					MainViewModel.AddLog("✅ データ品質に問題はありません");
				}
			}
		}
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

		/// <summary>馬ﾍｯﾀﾞ</summary>
		private static readonly string[] col_model = Arr("ﾚｰｽID", "馬ID", "Features");

		public static async Task CreateModel(this SQLiteControl conn)
		{
			await conn.Create(
				"t_model",
				col_model,
				Arr("ﾚｰｽID", "馬ID")
			);

			// TODO indexの作成
		}

		public static async Task<bool> ExistsModelTableAsync(this SQLiteControl conn)
		{
			return await conn.ExistsColumn("t_model", "ﾚｰｽID");
		}

		public static async Task InsertModelAsync(this SQLiteControl conn, List<OptimizedHorseFeaturesModel> data)
		{
			if (!data.Any()) return;

			foreach (var chunk in data.GroupBy(x => x.RaceId))
			{
				await conn.BeginTransaction();
				foreach (var x in chunk)
				{
					var parameters = new[]
					{
						SQLiteUtil.CreateParameter(DbType.String, x.RaceId),
						SQLiteUtil.CreateParameter(DbType.String, x.HorseName),
						SQLiteUtil.CreateParameter(DbType.Object, x)
					};
					await conn.ExecuteNonQueryAsync("REPLACE INTO t_model (ﾚｰｽID, 馬ID, Features) VALUES (?, ?, ?)", parameters);
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
	}

}