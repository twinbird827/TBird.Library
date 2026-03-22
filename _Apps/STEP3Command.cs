using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Transforms;
using Netkeiba.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TBird.Core;
using TBird.Wpf;

namespace Netkeiba
{
	internal class STEP3Command : STEPBase
	{
		// ログ出力の並列数を制限するためのSemaphore（最大4並列）
		private static readonly SemaphoreSlim LogSemaphore = new(10, 10);

		private static readonly SemaphoreSlim LogSemaphore2 = new(1, 1);

		// アンサンブル評価用：各ウィンドウの訓練済みモデルとNDCG1を保持
		private readonly ConcurrentDictionary<string, (ITransformer model, double ndcg1)> _ensembleModels = new();

		// モデル別ハイパーパラメータ（label=""がモデル保存対象）
		// (label, iter, leaves, depth, featFrac, subFrac, l2, minLeaf, lr, es)
		private static readonly Dictionary<FeaturesType, (string label, int iter, int leaves, int depth, double featFrac, double subFrac, double l2, int minLeaf, double lr, int es)[]> ModelParams = new()
		{
			// Total: 全398特徴量 (0.5002)
			{ FeaturesType.Total, new[] {
				("", 2000, 20, 7, 0.25, 0.90, 7.0, 100, 0.02, 150),
			}},
			// Horse: 191特徴量 (0.5041)
			{ FeaturesType.Horse, new[] {
				("", 2000, 15, 6, 0.30, 0.80, 12.0, 150, 0.02, 150),
			}},
			// Jockey: 70特徴量 (0.3953)
			{ FeaturesType.Jockey, new[] {
				("", 2000, 12, 5, 0.50, 0.70, 20.0, 250, 0.02, 150),
			}},
			// Blood: 112特徴量 (0.3905)
			{ FeaturesType.Blood, new[] {
				("", 800, 15, 7, 0.30, 0.70, 15.0, 200, 0.02, 150),
			}},
			// Connection: 116特徴量 (0.4277)
			{ FeaturesType.Connection, new[] {
				("", 2000, 20, 6, 0.35, 0.75, 15.0, 200, 0.02, 150),
			}},
			// TotalLarge: importance>=0.10 311特徴量
			{ FeaturesType.TotalLarge, new[] {
				("", 2000, 15, 6, 0.40, 0.85, 10.0, 120, 0.02, 150),
			}},
			// TotalMedium: importance>=0.12 190特徴量 (0.5076)
			{ FeaturesType.TotalMedium, new[] {
				("", 2000, 15, 6, 0.40, 0.85, 10.0, 120, 0.02, 150),
			}},
			// TotalSmall: importance>=0.15 110特徴量 (0.5079)
			{ FeaturesType.TotalSmall, new[] {
				("", 2000, 15, 6, 0.55, 0.85, 10.0, 120, 0.02, 150),
			}},
			// TotalRaw: 生値のみ 171特徴量 (0.5037)
			{ FeaturesType.TotalRaw, new[] {
				("", 2000, 15, 6, 0.40, 0.85, 10.0, 120, 0.02, 150),
			}},
			// TotalRank: Rankのみ 162特徴量 (0.4907)
			{ FeaturesType.TotalRank, new[] {
				("", 2000, 15, 6, 0.40, 0.85, 10.0, 120, 0.02, 150),
			}},
		};

		public STEP3Command(MainViewModel vm) : base(vm)
		{

		}

		protected override async Task ActionAsync(object dummy)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				if (!await conn.ExistsModelTableAsync())
				{
					MessageService.Debug("教育用データが作成されていません。処理を中断します。");
					return;
				}

				var basedate = DateTime.Now.AddDays(-2);
				for (var i = 0; i < 4; i++)
				{
					MessageService.Debug($"********** 基準日：{basedate} **********");
					await RankingAsync(
						await conn.GetModelAsync(basedate.AddYears(-6), basedate.AddMonths(-3)),
						Array.Empty<OptimizedHorseFeatures>(),
						await conn.GetModelAsync(basedate.AddMonths(-3).AddDays(1), basedate),
						shouldSaveModels: (i == 0)
					);
					basedate = basedate.AddMonths(-1);
				}
			}
		}

		private async Task RankingAsync(OptimizedHorseFeatures[] arr1, OptimizedHorseFeatures[] arrValid, OptimizedHorseFeatures[] arr2, bool shouldSaveModels = true)
		{
			// 初回のみ既存モデルを削除
			if (shouldSaveModels)
			{
				AppSetting.Instance.RemoveAllRankingTrain();
			}

			// アンサンブル用モデル辞書をクリア
			_ensembleModels.Clear();

			var task = new List<Task>();

			// 全モデルを統一ループで訓練
			foreach (var (type, configs) in ModelParams)
			{
				var grade = type.GetLabel();
				foreach (var p in configs)
				{
					task.Add(RankingAsync(grade, arr1, arrValid, arr2,
						OptimizedHorseFeatures.GetNormalizationNames(),
						OptimizedHorseFeatures.GetFeaturesTypeNames(type),
						p, shouldSaveModels));
				}
			}

			await task.WhenAll();

			// 全ウィンドウでアンサンブル評価（メモリ上のモデルを使用）
			EvaluateEnsemble(arr2);
		}

		private void EvaluateEnsemble(OptimizedHorseFeatures[] test)
		{
			var ml = new MLContext(seed: 1);

			// メモリ上の訓練済みモデルを使用
			// 0=Total, 1=Horse, 2=Jockey, 3=Blood, 4=Connection,
			// 5=TotalLarge, 6=TotalMedium, 7=TotalSmall, 8=TotalRaw, 9=TotalRank
			var types = new[]
			{
				FeaturesType.Total, FeaturesType.Horse, FeaturesType.Jockey, FeaturesType.Blood, FeaturesType.Connection,
				FeaturesType.TotalLarge, FeaturesType.TotalMedium, FeaturesType.TotalSmall, FeaturesType.TotalRaw, FeaturesType.TotalRank,
			};
			var grades = types.Select(t => t.GetLabel()).ToArray();

			// 全モデルが揃っているか確認
			if (grades.Any(g => !_ensembleModels.ContainsKey(g))) return;

			var entries = grades.Select(g => _ensembleModels[g]).ToArray();

			// テストデータの各モデルスコアを取得
			var view = ml.Data.LoadFromEnumerable(test);
			var scores = entries.Select(e =>
			{
				var predictions = e.model.Transform(view);
				return predictions.GetColumn<float>("Score").ToArray();
			}).ToArray();

			// NDCG1²重み（事前計算）
			var ndcg1Sq = entries.Select(e => e.ndcg1 * e.ndcg1).ToArray();

			// NDCG1²加重平均スコアラーを生成するヘルパー
			Func<int, float> MakeScorer(int[] idx)
			{
				var s = idx.Select(j => ndcg1Sq[j]).Sum();
				return i => (float)(idx.Select(j => scores[j][i] * ndcg1Sq[j]).Sum() / s);
			}

			// アンサンブル方式定義（12パターン）
			var methods = new (string name, Func<int, float> scorer)[]
			{
				//// (a) top3_current: Total + Horse + Connection
				//("(a) top3_current", MakeScorer(new[] { 0, 1, 4 })),
				//// (b) top3_large: TotalLarge + Horse + Connection
				//("(b) top3_large", MakeScorer(new[] { 5, 1, 4 })),
				//// (c) top3_medium: TotalMedium + Horse + Connection
				//("(c) top3_medium", MakeScorer(new[] { 6, 1, 4 })),
				//// (d) top3_small: TotalSmall + Horse + Connection
				//("(d) top3_small", MakeScorer(new[] { 7, 1, 4 })),
				//// (e) top3_raw: TotalRaw + Horse + Connection
				//("(e) top3_raw", MakeScorer(new[] { 8, 1, 4 })),
				//// (f) top3_rank: TotalRank + Horse + Connection
				//("(f) top3_rank", MakeScorer(new[] { 9, 1, 4 })),
				//// (g) top4_add_medium: Total + TotalMedium + Horse + Connection
				//("(g) top4_add_med", MakeScorer(new[] { 0, 6, 1, 4 })),
				//// (h) top4_add_small: Total + TotalSmall + Horse + Connection
				//("(h) top4_add_sml", MakeScorer(new[] { 0, 7, 1, 4 })),
				//// (i) diverse4: TotalMedium + TotalRaw + Horse + Connection
				//("(i) diverse4", MakeScorer(new[] { 6, 8, 1, 4 })),
				//// (j) all5: Total + Horse + Jockey + Blood + Connection
				//("(j) all5", MakeScorer(new[] { 0, 1, 2, 3, 4 })),
				//// (k) med_all5: TotalMedium + Horse + Jockey + Blood + Connection
				//("(k) med_all5", MakeScorer(new[] { 6, 1, 2, 3, 4 })),
				// (l) total_variants: Total + TotalMedium + TotalSmall + Horse + Connection
				("(l) total_vars", MakeScorer(new[] { 0, 6, 7, 1, 4 })),
				// (l) total_variants: Total + TotalMedium + TotalSmall + Horse + Connection
				("(j) total_vars2", MakeScorer(new[] { 1, 6, 7, 8, 9 })),
			};

			var message = new List<string> { "========== アンサンブル評価 ==========" };

			// 各方式のNDCGを計算
			foreach (var (name, scorer) in methods)
			{
				var preds = test.Select((t, i) => new FeaturesPrediction
				{
					RaceId = t.RaceId,
					ActualRank = (uint)(12 - t.Label),
					Score = scorer(i)
				}).ToArray();

				var ndcg = GetNDCG(preds);
				message.Add($"{name}\tNDCG@1\t{ndcg.NDCG1:F4}\tNDCG@3\t{ndcg.NDCG3:F4}\tNDCG@5\t{ndcg.NDCG5:F4}");
			}

			message.Add("==========================================");
			message.ForEach(s => WpfUtil.ExecuteOnUI(() => MessageService.Debug(s)));
		}

		private LightGbmRankingTrainer GetTrainer(MLContext _ml,
			int numberOfIterations = 2000,
			int numberOfLeaves = 20,
			int maximumTreeDepth = 7,
			double featureFraction = 0.25,
			double subsampleFraction = 0.9,
			double l2Regularization = 7.0,
			double learningRate = 0.02,
			int minimumExampleCountPerLeaf = 100,
			int earlyStoppingRound = 150)
		{
			return _ml.Ranking.Trainers.LightGbm(new LightGbmRankingTrainer.Options
			{
				LabelColumnName = "LabelKey",
				FeatureColumnName = "Features",
				RowGroupColumnName = "RaceIdKey",
				NumberOfIterations = numberOfIterations,
				LearningRate = learningRate,
				NumberOfLeaves = numberOfLeaves,
				MinimumExampleCountPerLeaf = minimumExampleCountPerLeaf,
				MaximumBinCountPerFeature = 255,
				UseCategoricalSplit = true,
				HandleMissingValue = true,
				UseZeroAsMissingValue = false,
				MinimumExampleCountPerGroup = 100,
				MaximumCategoricalSplitPointCount = 32,
				CategoricalSmoothing = 10.0,
				L2CategoricalRegularization = 10.0,
				EarlyStoppingRound = earlyStoppingRound,

				Booster = new GradientBooster.Options
				{
					L2Regularization = l2Regularization,
					L1Regularization = 0.5,
					MinimumSplitGain = 0.01,
					MaximumTreeDepth = maximumTreeDepth,
					FeatureFraction = featureFraction,
					SubsampleFraction = subsampleFraction,
					SubsampleFrequency = 1,
				},

				EvaluationMetric = LightGbmRankingTrainer.Options.EvaluateMetricType.NormalizedDiscountedCumulativeGain,
			});
		}

		private Task RankingAsync(string grade, OptimizedHorseFeatures[] data, OptimizedHorseFeatures[] valid, OptimizedHorseFeatures[] test, string[] normalizations, string[] features,
			(string label, int iter, int leaves, int depth, double featFrac, double subFrac, double l2, int minLeaf, double lr, int es) p, bool shouldSaveModels = true)
		{
			var _ml = new MLContext(seed: 1);
			var viewdata = _ml.Data.LoadFromEnumerable(data);
			var validdata = valid.Length > 0 ? _ml.Data.LoadFromEnumerable(valid) : null;
			var testdata = _ml.Data.LoadFromEnumerable(test);

			// 前処理パイプライン（RaceIdはHashで語彙不要・検証データでも安全に変換）
			var preprocessPipeline = _ml.Transforms.Conversion.Hash("RaceIdKey", "RaceId")
				.Append(_ml.Transforms.Conversion.MapValueToKey("LabelKey", "Label", keyOrdinality: ValueToKeyMappingEstimator.KeyOrdinality.ByValue))
				.NormalizeMeanVarianceMultiple(_ml, normalizations)
				.Append(_ml.Transforms.Concatenate("Features", features));
			var preprocessModel = preprocessPipeline.Fit(viewdata);
			var transformedTrain = preprocessModel.Transform(viewdata);
			var transformedValid = validdata != null ? preprocessModel.Transform(validdata) : null;

			// LightGBMトレーナー
			var trainer = GetTrainer(_ml,
				numberOfIterations: p.iter,
				numberOfLeaves: p.leaves,
				maximumTreeDepth: p.depth,
				featureFraction: p.featFrac,
				subsampleFraction: p.subFrac,
				l2Regularization: p.l2,
				minimumExampleCountPerLeaf: p.minLeaf,
				learningRate: p.lr,
				earlyStoppingRound: p.es);
			var trainedModel = transformedValid != null
				? trainer.Fit(transformedTrain, transformedValid)
				: trainer.Fit(transformedTrain);

			var model = preprocessModel.Append(trainedModel);

			// 並列数を制限してバックグラウンドで実行
			return Task.Run(async () =>
			{
				await LogSemaphore.WaitAsync();
				try
				{
					var message1 = new List<string>();

					if (p.label != "")
						message1.Add($"{grade}\t[{p.label}]");
					else
						message1.Add($"========== 予測スコア統計: {grade} ==========");
					message1.Add($"params\titer={p.iter}\tleaves={p.leaves}\tdepth={p.depth}\tfeatFrac={p.featFrac}\tsubFrac={p.subFrac}\tl2={p.l2}\tminLeaf={p.minLeaf}\tlr={p.lr}\tes={p.es}");

					// 予測を実行
					var predictions = model.Transform(testdata);
					var allScores = predictions.GetColumn<float>("Score").ToArray();

					//// スコアの統計情報を表示
					//(float Length, float Min, float Max, float Average, double Standard) GetScores(float[] allScores)
					//{
					//	return (
					//		Length: allScores.Length,
					//		Min: allScores.Min(),
					//		Max: allScores.Max(),
					//		Average: allScores.Average(),
					//		Standard: Math.Sqrt(allScores.Select(s => Math.Pow(s - allScores.Average(), 2)).Average())
					//	);
					//}
					//var mathScores = GetScores(allScores);
					//message.Add($"スコア数\t{mathScores.Length}\t最小スコア\t{mathScores.Min:F4}\t最大スコア\t{mathScores.Max:F4}\t平均スコア\t{mathScores.Average:F4}\t標準偏差\t{mathScores.Standard:F4}");

					var featuresPredictions = allScores.SelectInParallel((score, i) => new FeaturesPrediction
					{
						RaceId = test[i].RaceId,
						ActualRank = (uint)(12 - test[i].Label),
						Score = score
					}).ToArray();

					var ndcg = GetNDCG(featuresPredictions);

					// 訓練データのNDCGを計算
					var trainPredictions = model.Transform(viewdata);
					var trainScores = trainPredictions.GetColumn<float>("Score").ToArray();
					var trainFeaturesPredictions = trainScores.SelectInParallel((score, i) => new FeaturesPrediction
					{
						RaceId = data[i].RaceId,
						ActualRank = (uint)(12 - data[i].Label),
						Score = score
					}).ToArray();
					var trainNdcg = GetNDCG(trainFeaturesPredictions);

					// 手動でNDCGを計算
					message1.Add($"訓練NDCG@1\t{trainNdcg.NDCG1:F4}\t訓練NDCG@3\t{trainNdcg.NDCG3:F4}\t訓練NDCG@5\t{trainNdcg.NDCG5:F4}");
					message1.Add($"手動NDCG@1\t{ndcg.NDCG1:F4}\t手動NDCG@3\t{ndcg.NDCG3:F4}\t手動NDCG@5\t{ndcg.NDCG5:F4}\t評価レース数\t{ndcg.Count}");
					message1.Add(Enumerable.Range(0, 7).Select(i => $"手動RATE1@{i + 1}\t{ndcg.RATE1[i]:F4}").GetString("\t"));
					message1.Add(Enumerable.Range(0, 7).Select(i => $"手動RATE2@{i + 1}\t{ndcg.RATE2[i]:F4}").GetString("\t"));
					message1.Add(Enumerable.Range(0, 7).Select(i => $"手動RATE3@{i + 1}\t{ndcg.RATE3[i]:F4}").GetString("\t"));

					var message2 = new List<string>();

					message2.Add(message1.GetString("\t"));

					// アンサンブル評価用にモデルとNDCG1を保持（全ウィンドウ）
					if (p.label == "")
					{
						_ensembleModels[grade] = (model, ndcg.NDCG1);
					}

					if (p.label == "" && shouldSaveModels)
					{

						message2.Add("==============================");

						// データ件数も出力
						message2.Add($"学習グレード: {grade}\t学習データ件数: {data.Count()}件\tレース数: {data.Select(x => x.RaceId).Distinct().Count()}件");

						// 特徴量重要度を出力
						try
						{
							var lastTransformer = model.LastTransformer;
							if (lastTransformer is RankingPredictionTransformer<Microsoft.ML.Trainers.LightGbm.LightGbmRankingModelParameters> transformer)
							{
								message2.Add("========== 特徴量重要度 ==========");

								Microsoft.ML.Data.VBuffer<float> weights = default;
								transformer.Model.GetFeatureWeights(ref weights);

								var importance = weights.GetValues()
									.ToArray()
									.Select((weight, index) => new { Name = features[index], Weight = weight })
									.OrderByDescending(x => x.Weight);

								foreach (var item in importance)
								{
									message2.Add($"  {item.Name}: {item.Weight:F4}");
								}
								message2.Add("==========================================");

								// 特徴量相関分析
								//AnalyzeFeatureCorrelations(test, features, weights, message2);
							}
						}
						catch (Exception ex)
						{
							message2.Add($"特徴量重要度の取得に失敗: {ex.Message}");
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
						message2.Add($"モデルを保存しました: {result.Path}");
					}

					using (await Locker.LockAsync(Lock))
					{
						message2.ForEach(s => WpfUtil.ExecuteOnUI(() => MessageService.Debug(s)));
					}

				}
				finally
				{
					LogSemaphore.Release();
				}
			});
		}

		/// <summary>
		/// 特徴量の相関分析
		/// </summary>
		private void AnalyzeFeatureCorrelations(
			OptimizedHorseFeatures[] data,
			string[] features,
			VBuffer<float> weights,
			List<string> message)
		{
			message.Add("========== 特徴量相関分析 ==========");

			try
			{
				// サンプリング（最大100000件）
				var samples = data.Take(100000).ToArray();
				if (samples.Length == 0) return;

				// 全特徴量を対象
				var topFeatures = weights.GetValues()
					.ToArray()
					.Select((weight, index) => new { Name = features[index], Weight = weight })
					.OrderByDescending(x => x.Weight)
					.ToArray();

				message.Add($"分析対象: 全{topFeatures.Length}特徴量 × {samples.Length}サンプル");

				// プロパティ情報をキャッシュ
				var properties = topFeatures
					.Select(f => new
					{
						f.Name,
						f.Weight,
						Property = typeof(OptimizedHorseFeatures).GetProperty(f.Name)
					})
					.Where(x => x.Property != null)
					.ToArray();

				// 特徴量の値を事前に配列化（高速化）
				var values = properties
					.Select(p => samples.Select(s => Convert.ToSingle(p.Property!.GetValue(s))).ToArray())
					.ToArray();

				// 相関係数を計算
				var correlations = new List<(string F1, string F2, float Corr)>();

				for (int i = 0; i < properties.Length; i++)
				{
					for (int j = i + 1; j < properties.Length; j++)
					{
						var corr = PearsonCorrelation(values[i], values[j]);
						correlations.Add((properties[i].Name, properties[j].Name, corr));
					}
				}

				// 高相関ペア（重複の可能性）
				var highCorr = correlations
					.Where(x => Math.Abs(x.Corr) > 0.7f)
					.OrderByDescending(x => Math.Abs(x.Corr));

				message.Add("高相関ペア（|相関|>0.7、片方を削除候補）:");
				foreach (var (f1, f2, corr) in highCorr)
				{
					message.Add($"  {f1} - {f2}: {corr:F3}");
				}

				// 中程度の相関（交互作用の可能性）
				var medCorr = correlations
					.Where(x => Math.Abs(x.Corr) > 0.3f && Math.Abs(x.Corr) <= 0.7f)
					.OrderByDescending(x => Math.Abs(x.Corr));

				message.Add("中程度の相関（0.3<|相関|<0.7、交互作用の可能性）:");
				foreach (var (f1, f2, corr) in medCorr)
				{
					message.Add($"  {f1} - {f2}: {corr:F3}");
				}
			}
			catch (Exception ex)
			{
				message.Add($"相関分析に失敗: {ex.Message}");
			}

			message.Add("==========================================");
		}

		/// <summary>
		/// ピアソン相関係数
		/// </summary>
		private static float PearsonCorrelation(float[] x, float[] y)
		{
			var n = x.Length;
			var meanX = x.Average();
			var meanY = y.Average();

			var numerator = 0.0;
			var sumSqX = 0.0;
			var sumSqY = 0.0;

			for (int i = 0; i < n; i++)
			{
				var dx = x[i] - meanX;
				var dy = y[i] - meanY;
				numerator += dx * dy;
				sumSqX += dx * dx;
				sumSqY += dy * dy;
			}

			var denominator = Math.Sqrt(sumSqX * sumSqY);
			return denominator == 0 ? 0f : (float)(numerator / denominator);
		}

		private AggregateNDCG GetNDCG(FeaturesPrediction[] tests)
		{
			var raceGroups = tests.GroupBy(x => x.RaceId).ToArray();

			// 最適化: Parallel.ForEachで並列処理
			var results = new System.Collections.Concurrent.ConcurrentBag<AggregateNDCG>();

			Parallel.ForEach(raceGroups, raceGroup =>
			{
				if (raceGroup.Count() < 3) return; // 3頭未満のレースは除外

				// 予測スコア順にソート（高いスコア = 良い予測順位）
				var sortedByPrediction = raceGroup.OrderByDescending(x => x.Score).ToArray();

				// DCG@kを計算
				double CalculateDCG(int k)
				{
					double dcg = 0;
					for (int i = 0; i < Math.Min(k, sortedByPrediction.Length); i++)
					{
						var actualRank = sortedByPrediction[i].ActualRank;
						var relevance = 1.0 / actualRank;
						var discount = Math.Log2(i + 2);
						dcg += relevance / discount;
					}
					return dcg;
				}

				// Ideal DCG@k（完璧な順位予想）
				double CalculateIDCG(int k)
				{
					var idealOrder = raceGroup.OrderBy(x => x.ActualRank).ToArray();
					double idcg = 0;
					for (int i = 0; i < Math.Min(k, idealOrder.Length); i++)
					{
						var actualRank = idealOrder[i].ActualRank;
						var relevance = 1.0 / actualRank;
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

				results.Add(new AggregateNDCG()
				{
					NDCG1 = idcg1 > 0 ? dcg1 / idcg1 : 0,
					NDCG3 = idcg3 > 0 ? dcg3 / idcg3 : 0,
					NDCG5 = idcg5 > 0 ? dcg5 / idcg5 : 0,
					RATE1 = Enumerable.Range(0, 7).Select(i => i < sortedByPrediction.Length && sortedByPrediction[i].ActualRank <= 1 ? 1D : 0D).ToArray(),
					RATE2 = Enumerable.Range(0, 7).Select(i => i < sortedByPrediction.Length && sortedByPrediction[i].ActualRank <= 2 ? 1D : 0D).ToArray(),
					RATE3 = Enumerable.Range(0, 7).Select(i => i < sortedByPrediction.Length && sortedByPrediction[i].ActualRank <= 3 ? 1D : 0D).ToArray(),
				});
			});

			// 集計
			var validRaces = results.Count;
			var ndcg1Sum = results.Sum(x => x.NDCG1);
			var ndcg3Sum = results.Sum(x => x.NDCG3);
			var ndcg5Sum = results.Sum(x => x.NDCG5);

			return new AggregateNDCG()
			{
				Count = validRaces,
				NDCG1 = validRaces > 0 ? ndcg1Sum / validRaces : 0,
				NDCG3 = validRaces > 0 ? ndcg3Sum / validRaces : 0,
				NDCG5 = validRaces > 0 ? ndcg5Sum / validRaces : 0,
				RATE1 = Enumerable.Range(0, 7).Select(i => results.Sum(x => validRaces > 0 ? x.RATE1[i] / validRaces : 0)).ToArray(),
				RATE2 = Enumerable.Range(0, 7).Select(i => results.Sum(x => validRaces > 0 ? x.RATE2[i] / validRaces : 0)).ToArray(),
				RATE3 = Enumerable.Range(0, 7).Select(i => results.Sum(x => validRaces > 0 ? x.RATE3[i] / validRaces : 0)).ToArray(),
			};
		}

		private AggregateRATE[] GetRATE(FeaturesPrediction[] tests)
		{
			var min = -4;
			var max = 5;

			var rets = Enumerable.Range(min, max - min).Select(i => new AggregateRATE()
			{
				Target = i,
				Count = tests.Count(x => i <= x.Score && x.Score < i + 1),
				RATE1 = tests.Count(x => i <= x.Score && x.Score < i + 1 && x.ActualRank == 1),
				RATE3 = tests.Count(x => i <= x.Score && x.Score < i + 1 && x.ActualRank <= 3),
				RATE5 = tests.Count(x => i <= x.Score && x.Score < i + 1 && x.ActualRank <= 5),
			}).Concat(
			[
				new AggregateRATE()
				{
					Target = min - 1,
					Count = tests.Count(x => min > x.Score),
					RATE1 = tests.Count(x => min > x.Score && x.ActualRank == 1),
					RATE3 = tests.Count(x => min > x.Score && x.ActualRank <= 3),
					RATE5 = tests.Count(x => min > x.Score && x.ActualRank <= 5),
				},
				new AggregateRATE()
				{
					Target = max,
					Count = tests.Count(x => max <= x.Score),
					RATE1 = tests.Count(x => max <= x.Score && x.ActualRank == 1),
					RATE3 = tests.Count(x => max <= x.Score && x.ActualRank <= 3),
					RATE5 = tests.Count(x => max <= x.Score && x.ActualRank <= 5),
				}
			]);

			return rets.Select(x => new AggregateRATE()
			{
				Target = x.Target,
				Count = x.Count,
				RATE1 = x.RATE1 / x.Count,
				RATE3 = x.RATE3 / x.Count,
				RATE5 = x.RATE5 / x.Count,
			}).OrderBy(x => x.Target).ToArray();
		}

		//private AggregateNDCG GetNDCG(OptimizedHorseFeatures[] test, float[] allScores)
		//{
		//	// 最適化: インデックスの辞書を事前作成（O(n) → O(1)に改善）
		//	var indexMap = new Dictionary<OptimizedHorseFeatures, int>(test.Length);
		//	for (int i = 0; i < test.Length; i++)
		//	{
		//		indexMap[test[i]] = i;
		//	}

		//	var raceGroups = test.GroupBy(x => x.RaceId).ToArray();

		//	// 最適化: Parallel.ForEachで並列処理
		//	var results = new System.Collections.Concurrent.ConcurrentBag<AggregateNDCG>();

		//	Parallel.ForEach(raceGroups, raceGroup =>
		//	{
		//		var raceData = raceGroup.ToArray();
		//		if (raceData.Length < 3) return; // 3頭未満のレースは除外

		//		// 実際の着順（0ベース）と予測スコアを取得
		//		var raceResults = raceData.Select(horse => new
		//		{
		//			ActualRank = (int)horse.Label,
		//			PredictedScore = allScores[indexMap[horse]], // O(1)で取得
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
		//				var relevance = 1.0 / (actualRank + 1);
		//				var discount = Math.Log2(i + 2);
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

		//		double ndcg1 = idcg1 > 0 ? dcg1 / idcg1 : 0;
		//		double ndcg3 = idcg3 > 0 ? dcg3 / idcg3 : 0;
		//		double ndcg5 = idcg5 > 0 ? dcg5 / idcg5 : 0;

		//		results.Add(new AggregateNDCG()
		//		{
		//			NDCG1 = idcg1 > 0 ? dcg1 / idcg1 : 0,
		//			NDCG3 = idcg3 > 0 ? dcg3 / idcg3 : 0,
		//			NDCG5 = idcg5 > 0 ? dcg5 / idcg5 : 0
		//		});
		//	});

		//	// 集計
		//	var validRaces = results.Count;
		//	var ndcg1Sum = results.Sum(x => x.NDCG1);
		//	var ndcg3Sum = results.Sum(x => x.NDCG3);
		//	var ndcg5Sum = results.Sum(x => x.NDCG5);

		//	return new AggregateNDCG()
		//	{
		//		Count = validRaces,
		//		NDCG1 = validRaces > 0 ? ndcg1Sum / validRaces : 0,
		//		NDCG3 = validRaces > 0 ? ndcg3Sum / validRaces : 0,
		//		NDCG5 = validRaces > 0 ? ndcg5Sum / validRaces : 0
		//	};
		//}

		private class FeaturesPrediction
		{
			public string RaceId { get; set; }

			public uint ActualRank { get; set; }

			public float Score { get; set; }
		}

		private class AggregateNDCG
		{
			public int Count { get; set; }
			public double NDCG1 { get; set; }
			public double NDCG3 { get; set; }
			public double NDCG5 { get; set; }
			public double[] RATE1 { get; set; } = new double[7];
			public double[] RATE2 { get; set; } = new double[7];
			public double[] RATE3 { get; set; } = new double[7];

		}

		private class AggregateRATE
		{
			public int Target { get; set; }
			public int Count { get; set; }
			public double RATE1 { get; set; }
			public double RATE3 { get; set; }
			public double RATE5 { get; set; }
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