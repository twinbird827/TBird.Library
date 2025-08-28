using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using System.Formats.Asn1;
using Netkeiba;

namespace HorseRacingPrediction
{
	// ===== データリポジトリインターフェース =====

	// ===== CSV データリポジトリ実装 =====

	// ===== 使用例とワークフロー =====

	//public class TrainingDataCreationExample
	//{
	//	public static async Task<List<OptimizedHorseFeatures>> CreateTrainingDataExample()
	//	{
	//		// データリポジトリの初期化
	//		var dataRepository = new CsvDataRepository(@"C:\HorseRacingData");
	//		var generator = new TrainingDataGenerator(dataRepository);

	//		// 過去2年分の訓練データを生成
	//		var startDate = DateTime.Now.AddYears(-2);
	//		var endDate = DateTime.Now.AddMonths(-1); // 直近1ヶ月は除外（テスト用）

	//		var trainingData = await generator.GenerateTrainingDataAsync(
	//			startDate,
	//			endDate,
	//			minRaceCount: 2,      // 最低2戦以上の馬のみ
	//			includeNewHorses: true // 新馬も含める
	//		);

	//		// データ品質チェック
	//		var qualityReport = generator.ValidateTrainingData(trainingData);
	//		qualityReport.PrintReport();

	//		if (!qualityReport.IsValid)
	//		{
	//			MainViewModel.AddLog("⚠️ データ品質に問題があります。修正してください。");
	//			return null;
	//		}

	//		// データをファイルに保存（オプション）
	//		await SaveTrainingDataAsync(trainingData, @"C:\Models\training_data.json");

	//		return trainingData;
	//	}

	//	private static async Task SaveTrainingDataAsync(List<OptimizedHorseFeatures> data, string filePath)
	//	{
	//		var directory = Path.GetDirectoryName(filePath);
	//		if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
	//		{
	//			Directory.CreateDirectory(directory);
	//		}

	//		var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
	//		{
	//			WriteIndented = true
	//		});
	//		await File.WriteAllTextAsync(filePath, json);
	//		MainViewModel.AddLog($"訓練データを保存しました: {filePath}");
	//	}

	//	public static async Task<List<OptimizedHorseFeatures>> LoadTrainingDataAsync(string filePath)
	//	{
	//		if (!File.Exists(filePath))
	//			return null;

	//		var json = await File.ReadAllTextAsync(filePath);
	//		return JsonSerializer.Deserialize<List<OptimizedHorseFeatures>>(json);
	//	}
	//}

	// ===== 完全なワークフロー =====

	//public class CompletePredictionWorkflow
	//{
	//	public static async Task RunCompleteWorkflowAsync()
	//	{
	//		try
	//		{
	//			MainViewModel.AddLog("=== 競馬予想システム 完全ワークフロー ===");

	//			// Step 0: サンプルデータ生成（初回のみ）
	//			var dataDirectory = @"C:\HorseRacingData";
	//			if (!Directory.Exists(dataDirectory) || !File.Exists(Path.Combine(dataDirectory, "races.csv")))
	//			{
	//				MainViewModel.AddLog("\n0. サンプルデータ生成中...");
	//				CsvSampleDataGenerator.GenerateSampleCsvFiles(dataDirectory);
	//			}

	//			// Step 1: 訓練データ生成
	//			MainViewModel.AddLog("\n1. 訓練データ生成中...");
	//			var trainingData = await TrainingDataCreationExample.CreateTrainingDataExample();

	//			if (trainingData == null || !trainingData.Any())
	//			{
	//				MainViewModel.AddLog("❌ 訓練データの生成に失敗しました");
	//				return;
	//			}

	//			// Step 2: モデル訓練
	//			MainViewModel.AddLog("\n2. モデル訓練中...");
	//			var model = new HorseRacingPredictionModel();
	//			var modelPath = @"C:\Models\horse_racing_model.zip";

	//			model.TrainAndSaveModel(trainingData, modelPath);

	//			// Step 3: テストデータで評価
	//			MainViewModel.AddLog("\n3. モデル評価中...");
	//			var testData = await GenerateTestDataAsync();
	//			var evaluation = await EvaluateModelAsync(model, testData);

	//			MainViewModel.AddLog($"モデル精度: {evaluation.Accuracy:P1}");
	//			MainViewModel.AddLog($"Top3的中率: {evaluation.Top3HitRate:P1}");

	//			// Step 4: 実際の予想
	//			MainViewModel.AddLog("\n4. 実際の予想実行...");
	//			var todayRaces = await GetTodayRacesAsync();

	//			foreach (var race in todayRaces.Take(3)) // 最初の3レースのみ
	//			{
	//				var predictions = model.PredictRace(race.Horses, race);

	//				MainViewModel.AddLog($"\n📍 {race.CourseName} {race.Distance}m {race.Grade}");
	//				foreach (var pred in predictions.Take(5))
	//				{
	//					MainViewModel.AddLog($"  {pred.PredictedRank}位: {pred.Horse.Name} " +
	//						$"(スコア: {pred.Score:F3}, 信頼度: {pred.Confidence:P0})");
	//				}
	//			}

	//			MainViewModel.AddLog("\n✅ ワークフロー完了");
	//		}
	//		catch (Exception ex)
	//		{
	//			MainViewModel.AddLog($"❌ エラーが発生しました: {ex.Message}");
	//			MainViewModel.AddLog($"スタックトレース: {ex.StackTrace}");
	//		}
	//	}

	//	private static async Task<List<OptimizedHorseFeatures>> GenerateTestDataAsync()
	//	{
	//		// 直近1ヶ月のデータをテスト用に使用
	//		var dataRepository = new CsvDataRepository(@"C:\HorseRacingData");
	//		var generator = new TrainingDataGenerator(dataRepository);

	//		var startDate = DateTime.Now.AddMonths(-1);
	//		var endDate = DateTime.Now;

	//		return await generator.GenerateTrainingDataAsync(startDate, endDate);
	//	}

	//	private static async Task<ModelEvaluationResult> EvaluateModelAsync(
	//		HorseRacingPredictionModel model,
	//		List<OptimizedHorseFeatures> testData)
	//	{
	//		// 簡易評価（実際の実装では詳細な評価を行う）
	//		var correctPredictions = 0;
	//		var top3Hits = 0;
	//		var totalRaces = testData.Select(d => d.RaceId).Distinct().Count();

	//		// 評価ロジックの実装...

	//		return new ModelEvaluationResult
	//		{
	//			Accuracy = 0.68f, // 仮の値
	//			Top3HitRate = 0.85f, // 仮の値
	//			Precision = 0.72f,
	//			Recall = 0.65f
	//		};
	//	}

	//	private static async Task<List<Race>> GetTodayRacesAsync()
	//	{
	//		// 今日のレースデータを取得（サンプル実装）
	//		var dataRepository = new CsvDataRepository(@"C:\HorseRacingData");
	//		var races = await dataRepository.GetRacesAsync(DateTime.Today, DateTime.Today.AddDays(1));

	//		// サンプルレースを生成（実際のデータがない場合）
	//		if (!races.Any())
	//		{
	//			races = GenerateSampleRaces();
	//		}

	//		return races;
	//	}

	//	private static List<Race> GenerateSampleRaces()
	//	{
	//		var sampleRaces = new List<Race>
	//		{
	//			new Race
	//			{
	//				RaceId = "20250130_R01",
	//				CourseName = "東京",
	//				Distance = 1600,
	//				TrackType = "芝",
	//				TrackCondition = "良",
	//				Grade = "G1古",
	//				FirstPrizeMoney = 200000000,
	//				NumberOfHorses = 16,
	//				RaceDate = DateTime.Today,
	//				Horses = GenerateSampleHorses(16)
	//			}
	//		};

	//		return sampleRaces;
	//	}

	//	private static List<Horse> GenerateSampleHorses(int count)
	//	{
	//		var horses = new List<Horse>();
	//		var horseNames = new[] { "ディープインパクト", "ゴールドシップ", "キングカメハメハ", "ダイワメジャー", "ハーツクライ", "ルーラーシップ", "オルフェーヴル", "ロードカナロア", "モーリス", "エピファネイア", "キズナ", "ドリームジャーニー", "ヴィクトワールピサ", "エイシンフラッシュ", "ステイゴールド", "サートゥルナーリア" };
	//		var jockeyNames = new[] { "武豊", "川田将雅", "福永祐一", "戸崎圭太", "ルメール", "デムーロ", "岩田康誠", "池添謙一" };
	//		var trainerNames = new[] { "友道康夫", "藤沢和雄", "音無秀孝", "池江泰寿", "堀宣行", "国枝栄" };

	//		var random = new Random();

	//		for (int i = 0; i < count; i++)
	//		{
	//			horses.Add(new Horse
	//			{
	//				Name = horseNames[i % horseNames.Length] + $"_{i + 1}",
	//				Age = random.Next(3, 7),
	//				Weight = random.Next(450, 480),
	//				PreviousWeight = random.Next(445, 485),
	//				Jockey = jockeyNames[random.Next(jockeyNames.Length)],
	//				Trainer = trainerNames[random.Next(trainerNames.Length)],
	//				Sire = "ディープインパクト",
	//				DamSire = "キングカメハメハ",
	//				Breeder = "ノーザンファーム",
	//				LastRaceDate = DateTime.Now.AddDays(-random.Next(30, 90)),
	//				PurchasePrice = random.Next(20000000, 80000000),
	//				Odds = 2.0f + random.NextSingle() * 18.0f,
	//				RaceCount = random.Next(5, 20),
	//				RaceHistory = new List<RaceResult>()
	//			});
	//		}

	//		return horses;
	//	}
	//}

	//public class ModelEvaluationResult
	//{
	//	public float Accuracy { get; set; }
	//	public float Top3HitRate { get; set; }
	//	public float Precision { get; set; }
	//	public float Recall { get; set; }
	//}

	// ===== メインエントリーポイント =====

	//public class Program
	//{
	//	public static async Task Main(string[] args)
	//	{
	//		MainViewModel.AddLog("競馬予想システムを開始します...");

	//		try
	//		{
	//			await CompletePredictionWorkflow.RunCompleteWorkflowAsync();
	//		}
	//		catch (Exception ex)
	//		{
	//			MainViewModel.AddLog($"システムエラー: {ex.Message}");
	//		}

	//		MainViewModel.AddLog("\nEnterキーを押して終了してください...");
	//		Console.ReadLine();
	//	}
	//}
}