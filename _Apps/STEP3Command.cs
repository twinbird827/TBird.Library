using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.Transforms;
using Netkeiba.Models;
using System;
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
				var basedate = DateTime.Parse("2025/12/02");
				await RankingAsync(
					await conn.GetModelAsync(basedate.AddYears(-6), basedate.AddMonths(-12)),
					await conn.GetModelAsync(basedate.AddMonths(-12).AddDays(1), basedate)
				);

			}
		}

		private async Task RankingAsync(OptimizedHorseFeatures[] arr1, OptimizedHorseFeatures[] arr2)
		{
			// これまで作成した教育ﾃﾞｰﾀの削除
			AppSetting.Instance.RemoveAllRankingTrain();

			var task = new List<Task>();

			//task.Add(RankingAsync(FeaturesType.All.GetLabel(), arr1, arr2, OptimizedHorseFeatures.GetNormalizationNames(), OptimizedHorseFeatures.GetFeaturesTypeNames(FeaturesType.All)));

			//foreach (var type in FeaturesAttribute.GetTargetTypes())
			//{
			//	task.Add(RankingAsync(type.GetLabel(), arr1, arr2, OptimizedHorseFeatures.GetNormalizationNames(type), OptimizedHorseFeatures.GetFeaturesTypeNames(type)));
			//}

			//task.AddRange(RankingAsync2(FeaturesType.All.GetLabel(), arr1, arr2, OptimizedHorseFeatures.GetNormalizationNames(), OptimizedHorseFeatures.GetFeaturesTypeNames(FeaturesType.All)));

			var targets = new Dictionary<FeaturesType, string[]>()
			{
				{ FeaturesType.Jockey, OptimizedHorseFeatures.GetFeaturesTypeNames(FeaturesType.JockeyOther) },
				{ FeaturesType.Connection, OptimizedHorseFeatures.GetFeaturesTypeNames(FeaturesType.ConnectionOther) },
				{ FeaturesType.Blood, OptimizedHorseFeatures.GetFeaturesTypeNames(FeaturesType.BloodOther) },
			};
			var normalizations = new Dictionary<FeaturesType, string[]>()
			{
				{ FeaturesType.Jockey, OptimizedHorseFeatures.GetNormalizationNames(FeaturesType.JockeyOther).Concat(OptimizedHorseFeatures.GetNormalizationNames(FeaturesType.Jockey)).ToArray() },
				{ FeaturesType.Connection, OptimizedHorseFeatures.GetNormalizationNames(FeaturesType.ConnectionOther).Concat(OptimizedHorseFeatures.GetNormalizationNames(FeaturesType.Connection)).ToArray() },
				{ FeaturesType.Blood, OptimizedHorseFeatures.GetNormalizationNames(FeaturesType.BloodOther).Concat(OptimizedHorseFeatures.GetNormalizationNames(FeaturesType.Blood)).ToArray() },
			};

			foreach (var type in new[] { FeaturesType.Jockey, FeaturesType.Connection, FeaturesType.Blood })
			{
				task.AddRange(RankingAsync2(type.GetLabel(), arr1, arr2, normalizations[type], OptimizedHorseFeatures.GetFeaturesTypeNames(type), targets[type]));
			}

			await task.WhenAll();
		}

		private IEnumerable<Task> RankingAsync2(string grade, OptimizedHorseFeatures[] arr1, OptimizedHorseFeatures[] arr2, string[] normalizations, string[] features, string[] targets)
		{
			yield return RankingAsync(grade, arr1, arr2, normalizations, features, "all");

			foreach (var target in targets)
			{
				yield return RankingAsync(grade, arr1, arr2, normalizations, features, target);
			}
		}

		private EstimatorChain<RankingPredictionTransformer<LightGbmRankingModelParameters>> GetPipeline(MLContext _ml, string[] normalizations, string[] features)
		{
			var pipeline = _ml.Transforms.Conversion.MapValueToKey("RaceIdKey", "RaceId")
				.Append(_ml.Transforms.Conversion.MapValueToKey("LabelKey", "Label"))
				.NormalizeMeanVarianceMultiple(_ml, normalizations)
				.Append(_ml.Transforms.Concatenate("Features", features))
				.Append(_ml.Ranking.Trainers.LightGbm(new LightGbmRankingTrainer.Options
				{
					LabelColumnName = "LabelKey",
					FeatureColumnName = "Features",
					RowGroupColumnName = "RaceIdKey",
					NumberOfIterations = 200,     // やや減（過学習防止、案5）
					LearningRate = 0.5,          // やや増（学習速度向上、案5）
					NumberOfLeaves = 20,           // やや減（シンプル化、案5）
					MinimumExampleCountPerLeaf = 100, // 最小サンプル数（維持）
					MaximumBinCountPerFeature = 255, // ビン数を増やして精度向上（追加）
					UseCategoricalSplit = true,    // カテゴリ分割使用（Season, RaceDistance, CurrentGrade, CurrentTrackCondition用）
					HandleMissingValue = true,     // 欠損値処理（デフォルトtrue）
					UseZeroAsMissingValue = false, // 0を欠損値として扱う（デフォルトfalse）
					MinimumExampleCountPerGroup = 100, // グループの最小サンプル数（デフォルト100）
					MaximumCategoricalSplitPointCount = 32, // カテゴリ分割点の最大数（デフォルト32）
					CategoricalSmoothing = 10.0,   // カテゴリスムージング（デフォルト10.0）
					L2CategoricalRegularization = 10.0, // L2カテゴリ正則化（デフォルト10.0）

					Booster = new GradientBooster.Options
					{
						L2Regularization = 0.75,    // やや増（汎化性能向上、案5）
						L1Regularization = 0.25,   // L1正則化を微減（0.05→0.03）
						MinimumSplitGain = 0.005,  // 分割の最小ゲインを調整（0.01→0.005）
						MaximumTreeDepth = -1,      // 最大木の深さ（-1=制限なし、0→-1に変更）
					},

					// NDCG@1を重視
					EvaluationMetric = LightGbmRankingTrainer.Options.EvaluateMetricType.NormalizedDiscountedCumulativeGain,
					// 1着に最大の重みを付ける
					//CustomGains = new int[] { 0, 3, 7, 15, 31, 63, 127, 255, 511, 1023, 2047, 4095 }.Reverse().ToArray()
				}));

			return pipeline;
		}

		private Task RankingAsync(string grade, OptimizedHorseFeatures[] data, OptimizedHorseFeatures[] test, string[] normalizations, string[] features, string target = "")
		{
			var _ml = new MLContext(seed: 1);

			var viewdata = _ml.Data.LoadFromEnumerable(data);
			var testdata = _ml.Data.LoadFromEnumerable(test);

			if (target != "all" && target != "")
			{
				features = features.Concat(Arr(target)).ToArray();
				//normalizations = normalizations.Where(x => features.Contains(x)).ToArray();
			}

			var pipeline = GetPipeline(_ml, normalizations, features);

			var model = pipeline.Fit(viewdata);

			// 並列数を制限してバックグラウンドで実行
			return Task.Run(async () =>
			{
				await LogSemaphore.WaitAsync();
				try
				{
					var message1 = new List<string>();

					if (target != "")
						message1.Add($"{grade}\t{target}");
					else
						message1.Add($"========== 予測スコア統計: {grade} ==========");

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
						ActualRank = test[i].Label + 1,
						Score = score
					}).ToArray();

					var ndcg = GetNDCG(featuresPredictions);

					// 手動でNDCGを計算
					message1.Add($"手動NDCG@1\t{ndcg.NDCG1:F4}\t手動NDCG@3\t{ndcg.NDCG3:F4}\t手動NDCG@5\t{ndcg.NDCG5:F4}\t評価レース数\t{ndcg.Count}");
					message1.Add(Enumerable.Range(0, 7).Select(i => $"手動RATE1@{i + 1}\t{ndcg.RATE1[i]:F4}").GetString("\t"));
					message1.Add(Enumerable.Range(0, 7).Select(i => $"手動RATE2@{i + 1}\t{ndcg.RATE2[i]:F4}").GetString("\t"));
					message1.Add(Enumerable.Range(0, 7).Select(i => $"手動RATE3@{i + 1}\t{ndcg.RATE3[i]:F4}").GetString("\t"));

					var message2 = new List<string>();

					message2.Add(message1.GetString("\t"));

					if (target == "")
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
								AnalyzeFeatureCorrelations(test, features, weights, message2);
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

					WpfUtil.ExecuteOnUI(() => message2.ForEach(s => MessageService.Debug(s)));

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