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
using TBird.Core;
using TBird.Wpf;

namespace Netkeiba
{
	internal class STEP3Command : STEPBase
	{
		// モデル別ハイパーパラメータ（iter=2000, lr=0.02, es=150 は全モデル共通）
		// (leaves, depth, featFrac, subFrac, l2, minLeaf)
		private static readonly Dictionary<FeaturesType, (int leaves, int depth, double featFrac, double subFrac, double l2, int minLeaf)> ModelParams = new()
		{
			// Total: 全398特徴量 (0.5002)
			{ FeaturesType.Total, (20, 7, 0.25, 0.90, 7.0, 100) },
			// Horse: 191特徴量 (0.5041)
			{ FeaturesType.Horse, (15, 6, 0.30, 0.80, 12.0, 150) },
			// Connection: 116特徴量 (0.4277)
			{ FeaturesType.Connection, (20, 6, 0.35, 0.75, 15.0, 200) },
			// TotalMedium: importance>=0.12 190特徴量 (0.5076)
			{ FeaturesType.TotalMedium, (15, 6, 0.40, 0.85, 10.0, 120) },
			// TotalSmall: importance>=0.15 110特徴量 (0.5079)
			{ FeaturesType.TotalSmall, (15, 6, 0.55, 0.85, 10.0, 120) },
			// TotalRaw: 生値のみ 171特徴量 (0.5037)
			{ FeaturesType.TotalRaw, (15, 6, 0.40, 0.85, 10.0, 120) },
			// TotalRank: Rankのみ 162特徴量 (0.4907)
			{ FeaturesType.TotalRank, (15, 6, 0.40, 0.85, 10.0, 120) },
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
				MessageService.Debug($"********** 基準日：{basedate} **********");
				var data = await conn.GetModelAsync(basedate.AddYears(-6), basedate.AddMonths(-3));
				var test = await conn.GetModelAsync(basedate.AddMonths(-3).AddDays(1), basedate);

				AppSetting.Instance.RemoveAllRankingTrain();

				await ModelParams.Select(x => RankingAsync(x.Key.GetLabel(), data, test,
					OptimizedHorseFeatures.GetNormalizationNames(),
					OptimizedHorseFeatures.GetFeaturesTypeNames(x.Key),
					x.Value)).WhenAll();
			}
		}

		private Task RankingAsync(string grade, OptimizedHorseFeatures[] data, OptimizedHorseFeatures[] test, string[] normalizations, string[] features,
			(int leaves, int depth, double featFrac, double subFrac, double l2, int minLeaf) p)
		{
			var _ml = new MLContext(seed: 1);
			var viewdata = _ml.Data.LoadFromEnumerable(data);
			var testdata = _ml.Data.LoadFromEnumerable(test);

			// 前処理パイプライン（RaceIdはHashで語彙不要・検証データでも安全に変換）
			var preprocessPipeline = _ml.Transforms.Conversion.Hash("RaceIdKey", "RaceId")
				.Append(_ml.Transforms.Conversion.MapValueToKey("LabelKey", "Label", keyOrdinality: ValueToKeyMappingEstimator.KeyOrdinality.ByValue))
				.NormalizeMeanVarianceMultiple(_ml, normalizations)
				.Append(_ml.Transforms.Concatenate("Features", features));
			var preprocessModel = preprocessPipeline.Fit(viewdata);
			var transformedTrain = preprocessModel.Transform(viewdata);

			// LightGBMトレーナー
			var trainer = _ml.Ranking.Trainers.LightGbm(new LightGbmRankingTrainer.Options
			{
				LabelColumnName = "LabelKey",
				FeatureColumnName = "Features",
				RowGroupColumnName = "RaceIdKey",
				NumberOfIterations = 2000,
				LearningRate = 0.02,
				NumberOfLeaves = p.leaves,
				MinimumExampleCountPerLeaf = p.minLeaf,
				MaximumBinCountPerFeature = 255,
				UseCategoricalSplit = true,
				HandleMissingValue = true,
				UseZeroAsMissingValue = false,
				MinimumExampleCountPerGroup = 100,
				MaximumCategoricalSplitPointCount = 32,
				CategoricalSmoothing = 10.0,
				L2CategoricalRegularization = 10.0,
				EarlyStoppingRound = 150,

				Booster = new GradientBooster.Options
				{
					L2Regularization = p.l2,
					L1Regularization = 0.5,
					MinimumSplitGain = 0.01,
					MaximumTreeDepth = p.depth,
					FeatureFraction = p.featFrac,
					SubsampleFraction = p.subFrac,
					SubsampleFrequency = 1,
				},

				EvaluationMetric = LightGbmRankingTrainer.Options.EvaluateMetricType.NormalizedDiscountedCumulativeGain,
			});
			var trainedModel = trainer.Fit(transformedTrain);

			var model = preprocessModel.Append(trainedModel);

			// バックグラウンドで評価・保存
			return Task.Run(() =>
			{
				// 予測を実行
				var predictions = model.Transform(testdata);
				var allScores = predictions.GetColumn<float>("Score").ToArray();

				var featuresPredictions = allScores.SelectInParallel((score, i) => new FeaturesPrediction
				{
					RaceId = test[i].RaceId,
					ActualRank = (uint)(12 - test[i].Label),
					Score = score
				}).ToArray();

				var ndcg = GetNDCG(featuresPredictions);

				var result = new RankingTrain(
					DateTime.Now,
					grade,
					ndcg.NDCG1,
					ndcg.NDCG3,
					ndcg.NDCG5,
					GetCalibration(featuresPredictions)
				);
				AppSetting.Instance.UpdateRankingTrains(result);

				// ML.NET 4.0.2でのモデル保存方法
				DirectoryUtil.Create(Path.GetDirectoryName(result.Path));
				using (var fileStream = new FileStream(result.Path, FileMode.Create, FileAccess.Write, FileShare.Write))
				{
					_ml.Model.Save(model, null, fileStream);
				}

				var message = $"{grade}\tNDCG@1\t{ndcg.NDCG1:F4}\tNDCG@3\t{ndcg.NDCG3:F4}\tNDCG@5\t{ndcg.NDCG5:F4}\tTemperature\t{result.Temperature:F2}\tモデルを保存しました: {result.Path}";
				WpfUtil.ExecuteOnUI(() => MessageService.Debug(message));
			});
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
					NDCG1 = ndcg1,
					NDCG3 = ndcg3,
					NDCG5 = ndcg5,
				});
			});

			// 集計
			var validRaces = results.Count;
			var ndcg1Sum = results.Sum(x => x.NDCG1);
			var ndcg3Sum = results.Sum(x => x.NDCG3);
			var ndcg5Sum = results.Sum(x => x.NDCG5);

			return new AggregateNDCG()
			{
				NDCG1 = validRaces > 0 ? ndcg1Sum / validRaces : 0,
				NDCG3 = validRaces > 0 ? ndcg3Sum / validRaces : 0,
				NDCG5 = validRaces > 0 ? ndcg5Sum / validRaces : 0,
			};
		}

		private double GetCalibration(FeaturesPrediction[] tests)
		{
			var temperatures = new[] { 0.25f, 0.5f, 0.75f, 1.0f, 1.5f, 2.0f, 3.0f, 4.0f };
			var raceGroups = tests.GroupBy(x => x.RaceId).Where(g => g.Count() >= 3).ToArray();

			if (raceGroups.Length == 0) return 1.0;

			var bestT = 1.0;
			var bestBrier = double.MaxValue;

			foreach (var t in temperatures)
			{
				double brierSum = 0;
				int count = 0;

				foreach (var race in raceGroups)
				{
					var horses = race.ToArray();

					// softmax: P(i) = exp(Score_i / T) / Σ exp(Score_j / T)
					var maxScore = horses.Max(h => h.Score);
					var exps = horses.Select(h => Math.Exp((h.Score - maxScore) / t)).ToArray();
					var sumExp = exps.Sum();
					var probs = exps.Select(e => e / sumExp).ToArray();

					for (int i = 0; i < horses.Length; i++)
					{
						var actual = horses[i].ActualRank == 1 ? 1.0 : 0.0;
						brierSum += Math.Pow(probs[i] - actual, 2);
						count++;
					}
				}

				var brier = brierSum / count;

				if (brier < bestBrier)
				{
					bestBrier = brier;
					bestT = t;
				}
			}

			return bestT;
		}

		private class FeaturesPrediction
		{
			public string RaceId { get; set; }

			public uint ActualRank { get; set; }

			public float Score { get; set; }
		}

		private class AggregateNDCG
		{
			public double NDCG1 { get; set; }
			public double NDCG3 { get; set; }
			public double NDCG5 { get; set; }

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

	}
}
