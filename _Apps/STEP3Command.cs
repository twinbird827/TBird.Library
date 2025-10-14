using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms;
using Netkeiba.Models;
using OpenQA.Selenium.DevTools.V130.WebAudio;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;

namespace Netkeiba
{
	internal class STEP3Command : STEPBase
	{
		public STEP3Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				if (!await conn.ExistsModelTableAsync())
				{
					AddLog("教育用データが作成されていません。処理を中断します。");
					return;
				}

				//foreach (var grade in EnumUtil.GetValues<GradeType>())
				//{
				//	if (grade == GradeType.勝2ク) continue;

				//	// モデルの評価
				//	MainViewModel.AddLog($"モデル学習開始：{grade}");

				//	RankingAsync(
				//		conn.GetModelAsync(grade.ToString(), DateTime.Now.AddYears(-7), DateTime.Now.AddMonths(-1)),
				//		conn.GetModelAsync(grade.ToString(), DateTime.Now.AddMonths(-1).AddDays(1), DateTime.Now)
				//	);
				//}

				//foreach (var grade in Arr("未勝利ク", "勝古", "新馬", "オク", "オ古", "勝ク", "オ障", "未勝利障"))
				//{
				//	// モデルの評価
				//	MainViewModel.AddLog($"モデル学習開始：{grade}");

				//	RankingAsync(
				//		grade,
				//		conn.GetModelAsync(grade, DateTime.Now.AddYears(-7), DateTime.Now.AddMonths(-1)),
				//		conn.GetModelAsync(grade, DateTime.Now.AddMonths(-1).AddDays(1), DateTime.Now)
				//	);
				//}

				RankingAsync(
					"ALL",
					conn.GetModelAsync(DateTime.Now.AddYears(-6), DateTime.Now.AddMonths(-12)),
					conn.GetModelAsync(DateTime.Now.AddMonths(-12).AddDays(1), DateTime.Now)
				);

			}
		}

		private void RankingAsync(string grade, IAsyncEnumerable<OptimizedHorseFeaturesModel> arr1, IAsyncEnumerable<OptimizedHorseFeaturesModel> arr2)
		{
			var _ml = new MLContext(seed: 1);
			var data = arr1.ToBlockingEnumerable().ToArray();
			var test = arr2.ToBlockingEnumerable().ToArray();

			var viewdata = _ml.Data.LoadFromEnumerable(data);
			var testdata = _ml.Data.LoadFromEnumerable(test);

			var pipeline = _ml.ConversionSingle(OptimizedHorseFeatures.GetFlagItemNames())
				.Append(_ml.Transforms.Conversion.MapValueToKey("RaceIdKey", "RaceId"))
				.Append(_ml.Transforms.Conversion.MapValueToKey("LabelKey", "Label"))
				.NormalizeMeanVarianceMultiple(_ml, OptimizedHorseFeatures.GetNormalizationItemNames())
				// 全特徴量結合
				.Append(_ml.Transforms.Concatenate("Features", OptimizedHorseFeatures.GetAllFeaturesNames()))
				// LightGBMランキング学習（Optionsクラスで設定）
				.Append(_ml.Ranking.Trainers.LightGbm(new Microsoft.ML.Trainers.LightGbm.LightGbmRankingTrainer.Options
				{
					LabelColumnName = "LabelKey",
					FeatureColumnName = "Features",
					RowGroupColumnName = "RaceIdKey",
					NumberOfIterations = 1200,     // やや減（過学習防止、案5）
					LearningRate = 0.025,          // やや増（学習速度向上、案5）
					NumberOfLeaves = 60,           // やや減（シンプル化、案5）
					MinimumExampleCountPerLeaf = 18, // 最小サンプル数（維持）
					MaximumBinCountPerFeature = 255, // ビン数を増やして精度向上（追加）
					UseCategoricalSplit = true,    // カテゴリ分割使用（Season, RaceDistance, CurrentGrade, CurrentTrackCondition用）
					HandleMissingValue = true,     // 欠損値処理（デフォルトtrue）
					UseZeroAsMissingValue = false, // 0を欠損値として扱う（デフォルトfalse）
					MinimumExampleCountPerGroup = 100, // グループの最小サンプル数（デフォルト100）
					MaximumCategoricalSplitPointCount = 32, // カテゴリ分割点の最大数（デフォルト32）
					CategoricalSmoothing = 10.0,   // カテゴリスムージング（デフォルト10.0）
					L2CategoricalRegularization = 10.0, // L2カテゴリ正則化（デフォルト10.0）
					Booster = new Microsoft.ML.Trainers.LightGbm.GradientBooster.Options
					{
						L2Regularization = 0.7,    // やや増（汎化性能向上、案5）
						L1Regularization = 0.03,   // L1正則化を微減（0.05→0.03）
						MinimumSplitGain = 0.005,  // 分割の最小ゲインを調整（0.01→0.005）
						MaximumTreeDepth = -1,      // 最大木の深さ（-1=制限なし、0→-1に変更）
					}
				}));

			try
			{
				var model = pipeline.Fit(viewdata);

				// 予測を実行
				var predictions = model.Transform(testdata);
				// 訓練データをそのまま使用して評価（本来は分割すべきだが、動作確認のため）
				MainViewModel.AddLog($"評価データ件数: {predictions.GetRowCount() ?? 0}");

				// スコアの統計情報を表示
				(float Length, float Min, float Max, float Average, double Standard) GetScores(float[] allScores)
				{
					return (
						Length: allScores.Length,
						Min: allScores.Min(),
						Max: allScores.Max(),
						Average: allScores.Average(),
						Standard: Math.Sqrt(allScores.Select(s => Math.Pow(s - allScores.Average(), 2)).Average())
					);
				}
				var allScores = predictions.GetColumn<float>("Score").ToArray();
				var mathScores = GetScores(allScores);

				MainViewModel.AddLog($"========== 予測スコア統計 ==========");
				MainViewModel.AddLog($"スコア数: {mathScores.Length} / 最小スコア: {mathScores.Min:F4} / 最大スコア: {mathScores.Max:F4} / 平均スコア: {mathScores.Average:F4} / 標準偏差: {mathScores.Standard:F4}");

				// 手動でNDCGを計算
				MainViewModel.AddLog("========== 手動NDCG計算 ==========");

				var ndcg = GetNDCG(test, allScores);

				MainViewModel.AddLog($"手動NDCG@1: {ndcg.NDCG1:F4} / 手動NDCG@3: {ndcg.NDCG3:F4} / 手動NDCG@5: {ndcg.NDCG5:F4} / 評価レース数: {ndcg.Count}");

				MainViewModel.AddLog("==============================");
				MainViewModel.AddLog("評価完了（簡易版 + 手動NDCG）");

				// データ件数も出力
				MainViewModel.AddLog($"学習グレード: {grade} / 学習データ件数: {data.Count()}件 / レース数: {data.Select(x => x.RaceId).Distinct().Count()}件");

				// 特徴量重要度を出力
				try
				{
					var lastTransformer = model.LastTransformer;
					if (lastTransformer is RankingPredictionTransformer<Microsoft.ML.Trainers.LightGbm.LightGbmRankingModelParameters> transformer)
					{
						MainViewModel.AddLog("========== 特徴量重要度 ==========");
						var featureNames = OptimizedHorseFeatures.GetAllFeaturesNames();

						Microsoft.ML.Data.VBuffer<float> weights = default;
						transformer.Model.GetFeatureWeights(ref weights);

						var importance = weights.GetValues()
							.ToArray()
							.Select((weight, index) => new { Name = featureNames[index], Weight = weight })
							.OrderByDescending(x => x.Weight);

						foreach (var item in importance)
						{
							MainViewModel.AddLog($"  {item.Name}: {item.Weight:F4}");
						}
						MainViewModel.AddLog("==========================================");
					}
				}
				catch (Exception ex)
				{
					MainViewModel.AddLog($"特徴量重要度の取得に失敗: {ex.Message}");
				}

				var result = new RankingTrain(
					DateTime.Now,
					grade,
					ndcg.NDCG1,
					ndcg.NDCG3,
					ndcg.NDCG5
				);
				AppSetting.Instance.UpdateRankingTrains(result);

				// ML.NET 4.0.2でのモデル保存方法
				using var fileStream = new FileStream(result.Path, FileMode.Create, FileAccess.Write, FileShare.Write);
				_ml.Model.Save(model, null, fileStream);
				MainViewModel.AddLog($"モデルを保存しました: {result.Path}");
			}
			catch (Exception ex)
			{
				MessageService.Debug(ex.ToString());
			}

		}

		private (int Count, double NDCG1, double NDCG3, double NDCG5) GetNDCG(OptimizedHorseFeatures[] test, float[] allScores)
		{
			// 最適化: インデックスの辞書を事前作成（O(n) → O(1)に改善）
			var indexMap = new Dictionary<OptimizedHorseFeatures, int>(test.Length);
			for (int i = 0; i < test.Length; i++)
			{
				indexMap[test[i]] = i;
			}

			var raceGroups = test.GroupBy(x => x.RaceId).ToArray();

			// 最適化: Parallel.ForEachで並列処理
			var results = new System.Collections.Concurrent.ConcurrentBag<(double ndcg1, double ndcg3, double ndcg5)>();

			Parallel.ForEach(raceGroups, raceGroup =>
			{
				var raceData = raceGroup.ToArray();
				if (raceData.Length < 3) return; // 3頭未満のレースは除外

				// 実際の着順（0ベース）と予測スコアを取得
				var raceResults = raceData.Select(horse => new
				{
					ActualRank = (int)horse.Label,
					PredictedScore = allScores[indexMap[horse]], // O(1)で取得
				}).ToArray();

				// 予測スコア順にソート（高いスコア = 良い予測順位）
				var sortedByPrediction = raceResults.OrderByDescending(x => x.PredictedScore).ToArray();

				// DCG@kを計算
				double CalculateDCG(int k)
				{
					double dcg = 0;
					for (int i = 0; i < Math.Min(k, sortedByPrediction.Length); i++)
					{
						var actualRank = sortedByPrediction[i].ActualRank;
						var relevance = 1.0 / (actualRank + 1);
						var discount = Math.Log2(i + 2);
						dcg += relevance / discount;
					}
					return dcg;
				}

				// Ideal DCG@k（完璧な順位予想）
				double CalculateIDCG(int k)
				{
					var idealOrder = raceResults.OrderBy(x => x.ActualRank).ToArray();
					double idcg = 0;
					for (int i = 0; i < Math.Min(k, idealOrder.Length); i++)
					{
						var actualRank = idealOrder[i].ActualRank;
						var relevance = 1.0 / (actualRank + 1);
						var discount = Math.Log2(i + 2);
						idcg += relevance / discount;
					}
					return idcg;
				}

				var dcg1 = CalculateDCG(1);
				var dcg3 = CalculateDCG(3);
				var dcg5 = CalculateDCG(5);

				var idcg1 = CalculateIDCG(1);
				var idcg3 = CalculateIDCG(3);
				var idcg5 = CalculateIDCG(5);

				double ndcg1 = idcg1 > 0 ? dcg1 / idcg1 : 0;
				double ndcg3 = idcg3 > 0 ? dcg3 / idcg3 : 0;
				double ndcg5 = idcg5 > 0 ? dcg5 / idcg5 : 0;

				results.Add((ndcg1, ndcg3, ndcg5));
			});

			// 集計
			var validRaces = results.Count;
			var ndcg1Sum = results.Sum(x => x.ndcg1);
			var ndcg3Sum = results.Sum(x => x.ndcg3);
			var ndcg5Sum = results.Sum(x => x.ndcg5);

			return (
				Count: validRaces,
				NDCG1: validRaces > 0 ? ndcg1Sum / validRaces : 0,
				NDCG3: validRaces > 0 ? ndcg3Sum / validRaces : 0,
				NDCG5: validRaces > 0 ? ndcg5Sum / validRaces : 0
			);
		}

		//private (int Count, double NDCG1, double NDCG3, double NDCG5) GetNDCG(OptimizedHorseFeatures[] test, float[] allScores)
		//{
		//	var raceGroups = test.GroupBy(x => x.RaceId).ToArray();
		//	double ndcg1Sum = 0, ndcg3Sum = 0, ndcg5Sum = 0;
		//	int validRaces = 0;

		//	foreach (var raceGroup in raceGroups)
		//	{
		//		var raceData = raceGroup.ToArray();
		//		if (raceData.Length < 3) continue; // 3頭未満のレースは除外

		//		// 実際の着順（0ベース）と予測スコアを取得
		//		var raceResults = raceData.Select((horse, index) => new
		//		{
		//			ActualRank = (int)horse.Label, // 0ベースの着順
		//			PredictedScore = allScores[Array.IndexOf(test, horse)],
		//			Index = index
		//		}).ToArray();

		//		// 予測スコア順にソート（高いスコア = 良い予測順位）
		//		var sortedByPrediction = raceResults.OrderByDescending(x => x.PredictedScore).ToArray();

		//		// DCG@kを計算
		//		double CalculateDCG(int k)
		//		{
		//			double dcg = 0;
		//			for (int i = 0; i < Math.Min(k, sortedByPrediction.Length); i++)
		//			{
		//				var actualRank = sortedByPrediction[i].ActualRank;
		//				var relevance = 1.0 / (actualRank + 1); // 着順の逆数が関連度
		//				var discount = Math.Log2(i + 2); // 位置による割引
		//				dcg += relevance / discount;
		//			}
		//			return dcg;
		//		}

		//		// Ideal DCG@k（完璧な順位予想）
		//		double CalculateIDCG(int k)
		//		{
		//			var idealOrder = raceResults.OrderBy(x => x.ActualRank).ToArray();
		//			double idcg = 0;
		//			for (int i = 0; i < Math.Min(k, idealOrder.Length); i++)
		//			{
		//				var actualRank = idealOrder[i].ActualRank;
		//				var relevance = 1.0 / (actualRank + 1);
		//				var discount = Math.Log2(i + 2);
		//				idcg += relevance / discount;
		//			}
		//			return idcg;
		//		}

		//		var dcg1 = CalculateDCG(1);
		//		var dcg3 = CalculateDCG(3);
		//		var dcg5 = CalculateDCG(5);

		//		var idcg1 = CalculateIDCG(1);
		//		var idcg3 = CalculateIDCG(3);
		//		var idcg5 = CalculateIDCG(5);

		//		if (idcg1 > 0) ndcg1Sum += dcg1 / idcg1;
		//		if (idcg3 > 0) ndcg3Sum += dcg3 / idcg3;
		//		if (idcg5 > 0) ndcg5Sum += dcg5 / idcg5;

		//		validRaces++;
		//	}

		//	return (
		//		Count: validRaces,
		//		NDCG1: validRaces > 0 ? ndcg1Sum / validRaces : 0,
		//		NDCG3: validRaces > 0 ? ndcg3Sum / validRaces : 0,
		//		NDCG5: validRaces > 0 ? ndcg5Sum / validRaces : 0
		//	);
		//}
	}

	public static partial class SQLite3Extensions
	{
		public static async IAsyncEnumerable<OptimizedHorseFeaturesModel> GetModelAsync(this SQLiteControl conn, DateTime start, DateTime end)
		{
			var sql = @"
SELECT m.*
FROM   t_orig_h h, t_model m
WHERE  CAST(h.開催日数 AS INTEGER) BETWEEN ? AND ?
AND    h.ﾚｰｽID                 = m.RaceId
AND    CAST(h.障害 AS INTEGER) = 0
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.Int32, AppUtil.ToTotalDays(start.AddYears(-7))),
				SQLiteUtil.CreateParameter(DbType.Int32, AppUtil.ToTotalDays(end.AddMonths(-1))),
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				OptimizedHorseFeaturesModel model;
				try
				{
					model = OptimizedHorseFeaturesModel.Deserialize(x).NotNull();
				}
				catch (Exception ex)
				{
					MainViewModel.AddLog(ex.ToString());
					throw;
				}
				yield return model;
			}
		}

		public static async IAsyncEnumerable<OptimizedHorseFeaturesModel> GetModelAsync(this SQLiteControl conn, string grade, DateTime start, DateTime end)
		{
			var sql = @"
SELECT m.*
FROM   t_orig_h h, t_model m
WHERE  CAST(h.開催日数 AS INTEGER) BETWEEN ? AND ?
AND    h.ﾗﾝｸ2                  = ?
AND    h.ﾚｰｽID                 = m.RaceId
AND    CAST(h.障害 AS INTEGER) = 0
";
			var parameters = new[]
			{
				SQLiteUtil.CreateParameter(DbType.Int32, AppUtil.ToTotalDays(start.AddYears(-7))),
				SQLiteUtil.CreateParameter(DbType.Int32, AppUtil.ToTotalDays(end.AddMonths(-1))),
				SQLiteUtil.CreateParameter(DbType.String, grade.ToString()),
			};

			foreach (var x in await conn.GetRows(sql, parameters))
			{
				OptimizedHorseFeaturesModel model;
				try
				{
					model = OptimizedHorseFeaturesModel.Deserialize(x).NotNull();
				}
				catch (Exception ex)
				{
					MainViewModel.AddLog(ex.ToString());
					throw;
				}
				yield return model;
			}
		}
	}

	public static partial class MLContextExtensions
	{
		public static IEstimator<ITransformer> NormalizeMeanVarianceMultiple(this IEstimator<ITransformer> pipeline, MLContext _ml, params string[] featureNames)
		{
			if (featureNames.Length == 0)
				throw new ArgumentException("At least one feature name is required");

			foreach (var feature in featureNames)
			{
				pipeline = pipeline.Append(_ml.Transforms.NormalizeMeanVariance(feature));
			}

			return pipeline;
		}

		//{
		//	if (featureNames.Length == 0)
		//		throw new ArgumentException("At least one feature name is required");

		//	foreach (var feature in featureNames)
		//	{
		//		pipeline = pipeline.Append(_ml.Transforms.Categorical.OneHotEncoding($"{feature}OneHot", feature));
		//	}

		//	return pipeline;
		//}

		public static TypeConvertingEstimator ConversionSingle(this MLContext _ml, params string[] featureNames)
		{
			if (featureNames.Length == 0)
				throw new ArgumentException("At least one feature name is required");

			var pipeline = _ml.Transforms.Conversion.ConvertType(
				featureNames.Select(feature => new InputOutputColumnPair(feature, feature)).ToArray(),
				DataKind.Single
			);

			return pipeline;
		}

	}
}