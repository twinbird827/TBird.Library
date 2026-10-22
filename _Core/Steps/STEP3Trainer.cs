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
		// モデル別ハイパーパラメータ（iter=2000, lr=0.02, es=150 は全モデル共通）
		// (leaves, depth, featFrac, subFrac, l2, minLeaf)
		public static readonly Dictionary<FeaturesType, (int leaves, int depth, double featFrac, double subFrac, double l2, int minLeaf)> ModelParams = new()
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

		public static async Task<RankingTrain[]> TrainAsync()
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				if (!await conn.ExistsModelTableAsync())
				{
					throw new InvalidOperationException("教育用データが作成されていません。");
				}

				var basedate = DateTime.Now.AddDays(-2);
				MessageService.Debug($"********** 基準日：{basedate} **********");
				var data = await conn.GetModelAsync(basedate.AddYears(-6), basedate.AddMonths(-3));
				var test = await conn.GetModelAsync(basedate.AddMonths(-3).AddDays(1), basedate);

				AppSetting.Instance.RemoveAllRankingTrain();

				return await ModelParams.Select(x => RankingAsync(x.Key.GetLabel(), data, test,
					OptimizedHorseFeatures.GetNormalizationNames(),
					OptimizedHorseFeatures.GetFeaturesTypeNames(x.Key),
					x.Value)).WhenAll();
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

				var ndcg = featuresPredictions.GetNDCG();

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

				MessageService.Debug($"{grade}\tNDCG@1\t{ndcg.NDCG1:F4}\tNDCG@3\t{ndcg.NDCG3:F4}\tNDCG@5\t{ndcg.NDCG5:F4}\tTemperature\t{result.Temperature:F2}\tモデルを保存しました: {result.Path}");

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
	}
}
