using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Transforms;
using Netkeiba.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public static class STEP3Trainer
	{
		// 旧 7 モデルの "Total" と別名にし、旧 app-setting.json の Total モデルを読まないようにする
		public const string ModelName = "Single";

		// ハイパーパラメータ（iter=2000, lr=0.02, es=150）。値は研究の格子探索（netkeiba-feature-review.md 7.4 節）
		private static readonly (int leaves, int depth, double featFrac, double subFrac, double l2, int minLeaf) ModelParams = (31, 8, 0.4, 0.90, 7.0, 100);

		public static async Task<RankingTrain> TrainAsync()
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				// STEP2 を通らずに旧 BLOB を列ずれのまま学習しないよう、BLOB 長の不一致も未作成と同じに扱う
				if (!await conn.ExistsModelTableAsync() || !await conn.IsModelBlobLengthCurrentAsync())
				{
					throw new InvalidOperationException("教育用データが作成されていません。");
				}

				var basedate = DateTime.Now.AddDays(-2);
				MessageService.Debug($"********** 基準日：{basedate} **********");
				var data = await conn.GetModelAsync(basedate.AddYears(-6), basedate.AddMonths(-3));
				var test = await conn.GetModelAsync(basedate.AddMonths(-3).AddDays(1), basedate);

				AppSetting.Instance.RemoveAllRankingTrain();

				return await RankingAsync(ModelName, data, test,
					OptimizedHorseFeatures.GetNormalizationNames(),
					OptimizedHorseFeatures.GetFeaturesNames(),
					ModelParams);
			}
		}

		private static Task<RankingTrain> RankingAsync(string grade, OptimizedHorseFeatures[] data, OptimizedHorseFeatures[] test, string[] normalizations, string[] features,
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

			return Task.Run(() =>
			{
				var predictions = model.Transform(testdata);
				var allScores = predictions.GetColumn<float>("Score").ToArray();

				var featuresPredictions = allScores.SelectInParallel((score, i) => new FeaturesPrediction
				{
					RaceId = test[i].RaceId,
					ActualRank = (uint)(12 - test[i].Label),
					Score = score
				}).ToArray();

				var ndcg = featuresPredictions.GetNDCG();
				var temperature = GetCalibration(featuresPredictions);

				var result = new RankingTrain(
					DateTime.Now,
					grade,
					ndcg.NDCG1,
					ndcg.NDCG3,
					ndcg.NDCG5,
					temperature
				)
				{
					ConfidenceBounds = GetConfidenceBounds(featuresPredictions, temperature)
				};
				AppSetting.Instance.UpdateRankingTrains(result);

				DirectoryUtil.Create(Path.GetDirectoryName(result.Path));
				using (var fileStream = new FileStream(result.Path, FileMode.Create, FileAccess.Write, FileShare.Write))
				{
					_ml.Model.Save(model, null, fileStream);
				}

				MessageService.Debug($"{grade}\tNDCG@1\t{ndcg.NDCG1:F4}\tNDCG@3\t{ndcg.NDCG3:F4}\tNDCG@5\t{ndcg.NDCG5:F4}\tTemperature\t{result.Temperature:F2}\tConfidenceBounds\t{string.Join(",", result.ConfidenceBounds.Select(x => x.ToString("F4")))}\tモデルを保存しました: {result.Path}");

				return result;
			});
		}

		private static double GetCalibration(FeaturesPrediction[] tests)
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
					var probs = RacePredictionExtension.Softmax(horses.Select(h => h.Score).ToArray(), t);

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

		private static double[] GetConfidenceBounds(FeaturesPrediction[] tests, double temperature)
		{
			var maxP1 = tests.GroupBy(x => x.RaceId)
				.Where(g => g.Count() >= 3)
				.Select(g => RacePredictionExtension.Softmax(g.Select(x => x.Score).ToArray(), temperature).Max())
				.OrderBy(x => x)
				.ToArray();
			if (maxP1.Length == 0) return [];

			// numpy.percentile の既定（線形補間）と同じ
			return new[] { 0.2, 0.4, 0.6, 0.8 }.Select(q =>
			{
				var pos = q * (maxP1.Length - 1);
				var lo = (int)Math.Floor(pos);
				var hi = Math.Min(lo + 1, maxP1.Length - 1);
				return maxP1[lo] + (maxP1[hi] - maxP1[lo]) * (pos - lo);
			}).ToArray();
		}
	}
}
