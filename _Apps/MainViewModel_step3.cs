using AngleSharp.Common;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Web;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		private const string Label = "着順";
		private const string Group = "ﾚｰｽID";

		public BindableCollection<TreeCheckboxViewModel> CreateModelSources { get; } = new BindableCollection<TreeCheckboxViewModel>();

		public BindableContextCollection<TreeCheckboxViewModel> CreateModels { get; }

		private IEnumerable<CheckboxItemModel> GetCheckes() => CreateModels.SelectMany(x => x.Children).Select(x => x.Value);

		private long tgtdate;

		public IRelayCommand S3EXECCHECK => RelayCommand.Create(_ =>
		{
			CreateModelSources.ForEach(x => x.Value.IsChecked = _check = !_check);
		});
		private bool _check = false;

		public IRelayCommand S3EXECPREDICT => RelayCommand.Create(async _ =>
		{
			using var selenium = TBirdSeleniumFactory.GetDisposer();
			var pays = new (int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[]
			{
                    // 単4の予想結果
                    (400, "単4", (arr, payoutDetail, j) => Get三連単(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複2の予想結果
                    (200, "複2", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複3の予想結果
                    (300, "複3", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 1),
						arr.Where(x => x[j].GetInt32() <= 4),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複4aの予想結果
                    (400, "複4", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 4),
						arr.Where(x => x[j].GetInt32() <= 4),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // ワ1の予想結果
                    (100, "ワ1-2", (arr, payoutDetail, j) => Getワイド(payoutDetail,
						arr.Where(x => Arr(1, 2).Contains(x[j].GetInt32())))
					),
					(100, "ワ1-3", (arr, payoutDetail, j) => Getワイド(payoutDetail,
						arr.Where(x => Arr(1, 3).Contains(x[j].GetInt32())))
					),
					(100, "ワ2-3", (arr, payoutDetail, j) => Getワイド(payoutDetail,
						arr.Where(x => Arr(2, 3).Contains(x[j].GetInt32())))
					),
                    // ワ3の予想結果
                    (300, "ワ3", (arr, payoutDetail, j) => Getワイド(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 3))
					),
                    // 連1の予想結果
                    (100, "連1-2", (arr, payoutDetail, j) => Get馬連(payoutDetail,
						arr.Where(x => Arr(1, 2).Contains(x[j].GetInt32())))
					),
					(100, "連1-3", (arr, payoutDetail, j) => Get馬連(payoutDetail,
						arr.Where(x => Arr(1, 3).Contains(x[j].GetInt32())))
					),
					(100, "連2-3", (arr, payoutDetail, j) => Get馬連(payoutDetail,
						arr.Where(x => Arr(2, 3).Contains(x[j].GetInt32())))
					),
                    // 単勝1の予想結果
                    (100, "勝1", (arr, payoutDetail, j) => Get単勝(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 1))
					),
                    // 単勝1の予想結果
                    (100, "勝2", (arr, payoutDetail, j) => Get単勝(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 2))
					),
                    // 単勝1の予想結果
                    (100, "勝3", (arr, payoutDetail, j) => Get単勝(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 3))
					),
			};

			// Initialize MLContext
			MLContext mlContext = new MLContext();

			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var maxdate = await conn.ExecuteScalarAsync<long>("SELECT MAX(開催日数) FROM t_model");
				var mindate = await conn.ExecuteScalarAsync<long>("SELECT MIN(開催日数) FROM t_model");
				tgtdate = Calc(maxdate, (maxdate - mindate) * 0.1, (x, y) => x - y).GetInt64();
			}

			var ranks = AppUtil.ﾗﾝｸ2.Keys;

			var bbbb = PredictionModel(pays, Arr(
				ranks.Select(rank => new BinaryClassificationPredictionFactory(mlContext, rank, 1)),
				ranks.Select(rank => new BinaryClassificationPredictionFactory(mlContext, rank, 2)),
				ranks.Select(rank => new BinaryClassificationPredictionFactory(mlContext, rank, 3)),
				ranks.Select(rank => new BinaryClassificationPredictionFactory(mlContext, rank, 6)),
				ranks.Select(rank => new BinaryClassificationPredictionFactory(mlContext, rank, 7)),
				ranks.Select(rank => new BinaryClassificationPredictionFactory(mlContext, rank, 8))
			).SelectMany(tmp => tmp).ToArray());
			var cccc = PredictionModel(pays, Arr(
				ranks.Select(rank => new RegressionPredictionFactory(mlContext, rank, 1))
			).SelectMany(tmp => tmp).ToArray());

			var path = Path.Combine("result", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-Prediction.csv");

			FileUtil.BeforeCreate(path);

			AddLog($"S3EXECPREDICT: {path}");

			using (var file = new FileAppendWriter(path))
			{
				// ﾍｯﾀﾞの書き込み
				await file.WriteLineAsync(Arr(
					"Rank",
					"Index",
					"Score",
					"Rate"
				).Concat(
					pays.SelectMany(x => Arr(x.head + "+S", x.head + "+R"))
				).GetString(","));

				foreach (var dic in bbbb)
				{
					var val = await dic.Value;

					await file.WriteLineAsync(
						dic.Key.Run(x => Arr((object)x.Rank, x.Index, x.Score, x.Rate)).Concat(
							val.SelectMany(x => Arr((object)x.score, x.rate))
						).Select(x => x.ToString()).GetString(",")
					);
				}

				foreach (var dic in cccc)
				{
					var val = await dic.Value;

					await file.WriteLineAsync(
						dic.Key.Run(x => Arr((object)x.Rank, x.Index, x.Score, x.Rate)).Concat(
							val.SelectMany(x => Arr((object)x.score, x.rate))
						).Select(x => x.ToString()).GetString(",")
					);
				}
			}

			System.Diagnostics.Process.Start("EXPLORER.EXE", Path.GetFullPath("result"));
		});

		public IRelayCommand S3EXEC => RelayCommand.Create(async _ =>
		{
			using var selenium = TBirdSeleniumFactory.GetDisposer();
			var seconds = AppSetting.Instance.TrainingCount;
			var metrics = Arr(BinaryClassificationMetric.AreaUnderRocCurve);

			DirectoryUtil.DeleteInFiles("model", x => Path.GetExtension(x.FullName) == ".csv");

			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var maxdate = await conn.ExecuteScalarAsync<long>("SELECT MAX(開催日数) FROM t_model");
				var mindate = await conn.ExecuteScalarAsync<long>("SELECT MIN(開催日数) FROM t_model");
				tgtdate = Calc(maxdate, (maxdate - mindate) * 0.1, (x, y) => x - y).GetInt64();
			}
			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum =
				seconds * GetCheckes().Count(x => x.IsChecked && x.Value.StartsWith("B-")) * metrics.Length +
				seconds * GetCheckes().Count(x => x.IsChecked && x.Value.StartsWith("R-"));

			AppSetting.Instance.Save();

			Func<DbDataReader, (float 着順, float 単勝)> 着勝 = r => (r.GetValue("着順").GetSingle(), r.GetValue("単勝").GetSingle());

			Func<int, int, int, int, int, int, int, int, Dictionary<int, Func<DbDataReader, object>>> RANK別2 = (i1, i2, i3, i4, j1, j2, j3, j4) => new Dictionary<int, Func<DbDataReader, object>>()
			{
				{ 1, r => 着勝(r).Run(x => x.着順 <= i1) },
				{ 2, r => 着勝(r).Run(x => x.着順 <= i2) },
				{ 3, r => 着勝(r).Run(x => x.着順 <= i3) },
				{ 4, r => 着勝(r).Run(x => x.着順 <= i4) },
				{ 6, r => 着勝(r).Run(x => x.着順 > j1) },
				{ 7, r => 着勝(r).Run(x => x.着順 > j2) },
				{ 8, r => 着勝(r).Run(x => x.着順 > j3) },
				{ 9, r => 着勝(r).Run(x => x.着順 > j4) },
			};

			var dic = AppUtil.ﾗﾝｸ2.Keys.ToDictionary(x => x, _ => RANK別2(5, 6, 7, 8, 3, 4, 5, 6));

			//try
			//{
			//	await MultiClassClassification(1, "RANK1", 100);
			//}
			//catch (Exception ex)
			//{
			//	MessageService.Info(ex.ToString());
			//	return;
			//}

			var random = new Random();
			for (var tmp = 0; tmp < seconds; tmp++)
			{
				var second = (uint)random.Next((int)AppSetting.Instance.MinimumTrainingTimeSecond, (int)AppSetting.Instance.MaximumTrainingTimeSecond);

				foreach (var metric in metrics)
				{
					foreach (var x in GetCheckes().Where(x => x.IsChecked && x.Value.StartsWith("B-")))
					{
						var args = x.Value.Split("-");
						var index = args[2].GetInt32();
						var rank = args[1];

						await BinaryClassification(index, rank, second, metric, dic[rank][index]).TryCatch();
					}
				}

				foreach (var x in GetCheckes().Where(x => x.IsChecked && x.Value.StartsWith("R-")))
				{
					var args = x.Value.Split("-");
					await Regression(args[1], second).TryCatch();
				};

				foreach (var x in GetCheckes().Where(x => x.IsChecked && x.Value.StartsWith("M-")))
				{
					var args = x.Value.Split("-");
					await MultiClassClassification(1, args[1], second).TryCatch();
				};

			}
		});

		private async Task BinaryClassification(int index, string rank, uint second, BinaryClassificationMetric metric, Func<DbDataReader, object> func_yoso)
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath, rank, func_yoso);

			AddLog($"=============== Begin Update of BinaryClassification evaluation {rank} {index} {second} ===============");

			// Infer column information
			var columnInference = mlContext.Auto().InferColumns(dataPath, new ColumnInformation().Run(x =>
			{
				x.LabelColumnName = Label;
				x.SamplingKeyColumnName = Group;
				x.IgnoredColumnNames.AddRange(AppSetting.Instance.DicCor.First(c => c.Key == rank).Value);
			}), groupColumns: false);
			columnInference.TextLoaderOptions.Run(x =>
			{
				x.Columns[1].DataKind = DataKind.Int64;
			});

			// Create text loader
			TextLoader loader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);

			// Load data into IDataView
			IDataView data = loader.Load(dataPath);

			// Split into train (80%), validation (20%) sets
			var trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

			//Define pipeline
			SweepablePipeline pipeline = mlContext
					.Auto()
					.Featurizer(data, columnInformation: columnInference.ColumnInformation)
					.Append(mlContext.Auto().BinaryClassification(
						labelColumnName: columnInference.ColumnInformation.LabelColumnName,
						useFastForest: AppSetting.Instance.UseFastForest,
						useFastTree: AppSetting.Instance.UseFastTree,
						useLbfgsLogisticRegression: AppSetting.Instance.UseLbfgsLogisticRegression,
						useLgbm: AppSetting.Instance.UseLgbm,
						useSdcaLogisticRegression: AppSetting.Instance.UseSdcaLogisticRegression
					));

			// Log experiment trials
			var monitor = new AutoMLMonitor(pipeline, this);

			// Create AutoML experiment
			var experiment = mlContext.Auto().CreateExperiment()
				.SetPipeline(pipeline)
				.SetBinaryClassificationMetric(metric, labelColumn: Label)
				.SetTrainingTimeInSeconds(second)
				.SetEciCostFrugalTuner()
				.SetDataset(trainValidationData)
				.SetMonitor(monitor);

			// Run experiment
			var cts = new CancellationTokenSource();
			TrialResult experimentResults = await experiment.RunAsync(cts.Token);

			// Get best model
			var model = experimentResults.Model;

			// Get all completed trials
			var completedTrials = monitor.GetCompletedTrials();

			// Measure trained model performance
			// Apply data prep transformer to test data
			// Use trained model to make inferences on test data
			IDataView testDataPredictions = model.Transform(trainValidationData.TestSet);

			// Save model
			var savepath = $@"model\BinaryClassification_{rank}_{index.ToString(2)}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

			var trained = mlContext.BinaryClassification.Evaluate(testDataPredictions, labelColumnName: Label);
			var now = await PredictionModel(rank, new BinaryClassificationPredictionFactory(mlContext, rank, index, model)).RunAsync(x =>
				new BinaryClassificationResult(savepath, rank, index, second, trained, x.score, x.rate)
			);
			var old = AppSetting.Instance.GetBinaryClassificationResult(index, rank);
			var bst = old == BinaryClassificationResult.Default || old.GetScore() < now.GetScore() ? now : old;

			AddLog($"=============== Result of BinaryClassification Model Data {rank} {index} {second} ===============");
			AddLog($"Accuracy: {trained.Accuracy}");
			AddLog($"AreaUnderPrecisionRecallCurve: {trained.AreaUnderPrecisionRecallCurve}");
			AddLog($"Entropy: {trained.Entropy}");
			AddLog($"F1Score: {trained.F1Score}");
			AddLog($"LogLoss: {trained.LogLoss}");
			AddLog($"LogLossReduction: {trained.LogLossReduction}");
			AddLog($"NegativePrecision: {trained.NegativePrecision}");
			AddLog($"NegativeRecall: {trained.NegativeRecall}");
			AddLog($"PositivePrecision: {trained.PositivePrecision}");
			AddLog($"PositiveRecall: {trained.PositiveRecall}");
			AddLog($"{trained.ConfusionMatrix.GetFormattedConfusionTable()}");
			AddLog($"AreaUnderRocCurve: {trained.AreaUnderRocCurve}");
			AddLog($"Rate: {now.Rate:N4}     Score: {now.Score:N4}     S^2*R: {now.GetScore():N4}");
			AddLog($"=============== End Update of BinaryClassification evaluation {rank} {index} {second} ===============");

			mlContext.Model.Save(model, data.Schema, savepath);

			AppSetting.Instance.UpdateBinaryClassificationResults(bst, old);

			if (old != null && !bst.Equals(old) && await FileUtil.Exists(old.Path))
			{
				FileUtil.Delete(old.Path);
			}

			Progress.Value += 1;

			AppUtil.DeleteEndress(dataPath);
		}

		private async Task Regression(string rank, uint second)
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath, rank, (int 着順) => 着順);

			AddLog($"=============== Begin of Regression evaluation {rank} {second} ===============");

			var columnInference = mlContext.Auto().InferColumns(dataPath, new ColumnInformation().Run(x =>
			{
				x.LabelColumnName = Label;
				x.SamplingKeyColumnName = Group;
				x.IgnoredColumnNames.AddRange(AppSetting.Instance.DicCor.First(c => c.Key == rank).Value);
			}), groupColumns: false);
			columnInference.TextLoaderOptions.Run(x =>
			{
				x.Columns[1].DataKind = DataKind.Int64;
			});

			// Create text loader
			TextLoader loader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);

			// Load data into IDataView
			IDataView data = loader.Load(dataPath);

			// Split into train (80%), validation (20%) sets
			var trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

			//Define pipeline
			SweepablePipeline pipeline = mlContext
					.Auto()
					.Featurizer(data, columnInformation: columnInference.ColumnInformation)
					.Append(mlContext.Auto().Regression(
						labelColumnName: columnInference.ColumnInformation.LabelColumnName,
						useFastForest: AppSetting.Instance.UseFastForest,
						useFastTree: AppSetting.Instance.UseFastTree,
						useLbfgsPoissonRegression: AppSetting.Instance.UseLbfgsPoissonRegression,
						useLgbm: AppSetting.Instance.UseLgbm,
						useSdca: AppSetting.Instance.UseSdca
					));

			// Log experiment trials
			var monitor = new AutoMLMonitor(pipeline, this);

			// Create AutoML experiment
			var experiment = mlContext.Auto().CreateExperiment()
				.SetPipeline(pipeline)
				.SetRegressionMetric(AppSetting.Instance.RegressionMetric, Label)
				.SetTrainingTimeInSeconds((uint)second)
				.SetEciCostFrugalTuner()
				.SetDataset(trainValidationData)
				.SetMonitor(monitor);

			// Run experiment
			var cts = new CancellationTokenSource();
			TrialResult experimentResults = await experiment.RunAsync(cts.Token);

			// Get best model
			var model = experimentResults.Model;

			// Get all completed trials
			var completedTrials = monitor.GetCompletedTrials();

			// Measure trained model performance
			// Apply data prep transformer to test data
			// Use trained model to make inferences on test data
			IDataView testDataPredictions = model.Transform(trainValidationData.TestSet);

			// Save model
			var savepath = $@"model\Regression_{rank}_{1.ToString(2)}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

			var trained = mlContext.Regression.Evaluate(testDataPredictions, labelColumnName: Label);
			var now = await PredictionModel(rank, new RegressionPredictionFactory(mlContext, rank, 1, model)).RunAsync(x =>
				new RegressionResult(savepath, rank, 1, second, trained, x.score, x.rate)
			);
			var old = AppSetting.Instance.GetRegressionResult(1, rank);
			var bst = old == RegressionResult.Default || old.GetScore() < now.GetScore() ? now : old;

			AddLog($"=============== Result of Regression Model Data {rank} {second} ===============");
			AddLog($"MeanSquaredError: {trained.MeanSquaredError}");
			AddLog($"RootMeanSquaredError: {trained.RootMeanSquaredError}");
			AddLog($"LossFunction: {trained.LossFunction}");
			AddLog($"MeanAbsoluteError: {trained.MeanAbsoluteError}");
			AddLog($"RSquared: {trained.RSquared}");
			AddLog($"Rate: {now.Rate:N4}     Score: {now.Score:N4}     S^2*R: {now.GetScore():N4}");
			AddLog($"=============== End of Regression evaluation {rank} {second} ===============");

			mlContext.Model.Save(model, data.Schema, savepath);

			AppSetting.Instance.UpdateRegressionResults(bst, old);

			if (old != null && !bst.Equals(old) && await FileUtil.Exists(old.Path))
			{
				FileUtil.Delete(old.Path);
			}

			Progress.Value += 1;

			AppUtil.DeleteEndress(dataPath);
		}

		private async Task MultiClassClassification(int index, string rank, uint second)
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath, rank, (int 着順) => (uint)Math.Min(着順, 6));

			AddLog($"=============== Begin Update of MultiClassClassification evaluation {rank} {index} {second} ===============");

			// Infer column information
			var columnInference = mlContext.Auto().InferColumns(dataPath, new ColumnInformation().Run(x =>
			{
				x.LabelColumnName = Label;
				x.SamplingKeyColumnName = Group;
				x.IgnoredColumnNames.AddRange(AppSetting.Instance.DicCor.First(c => c.Key == rank).Value);
			}), groupColumns: false);
			columnInference.TextLoaderOptions.Run(x =>
			{
				x.Columns[0].DataKind = DataKind.UInt32;
				x.Columns[1].DataKind = DataKind.Int64;
			});
			// Create text loader
			TextLoader loader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);

			// Load data into IDataView
			IDataView data = loader.Load(dataPath);

			// Split into train (80%), validation (20%) sets
			var trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);
			////Define pipeline
			//SweepablePipeline pipeline = mlContext
			//		.Auto()
			//		.Featurizer(data)
			//		.Append(mlContext.Transforms.Conversion.MapValueToKey("Label", Label))
			//		.Append(mlContext.Transforms.Concatenate("Features", Enumerable.Range(0, 235).Select(i => $"C{i.ToString(4)}").ToArray()))
			//		.Append(mlContext.Auto().MultiClassification(
			//			labelColumnName: Label,
			//			useFastForest: AppSetting.Instance.UseFastForest,
			//			useFastTree: AppSetting.Instance.UseFastTree,
			//			useLbfgsLogisticRegression: AppSetting.Instance.UseLbfgsLogisticRegression,
			//			useLgbm: AppSetting.Instance.UseLgbm,
			//			useSdcaLogisticRegression: AppSetting.Instance.UseSdcaLogisticRegression
			//		).Append(mlContext.Transforms.Conversion.MapKeyToValue(Label, "Label")));

			//// Log experiment trials
			//var monitor = new AutoMLMonitor(pipeline, this);

			//// Create AutoML experiment
			//var experiment = mlContext.Auto().CreateExperiment()
			//	.SetPipeline(pipeline)
			//	.SetMulticlassClassificationMetric(MulticlassClassificationMetric.MacroAccuracy, "Label")
			//	.SetTrainingTimeInSeconds(second)
			//	.SetEciCostFrugalTuner()
			//	.SetDataset(trainValidationData)
			//	.SetMonitor(monitor);

			//// Run experiment
			//var cts = new CancellationTokenSource();
			//TrialResult experimentResults = await experiment.RunAsync(cts.Token);
			//TrainTestData valid = mlContext.Data.TrainTestSplit(trainValidationData.TrainSet, testFraction: 0.2);

			//// Get best model
			//var model = experimentResults.Model;

			var pipeline = mlContext.Transforms.Conversion.MapValueToKey("Label", Label)
				.Append(mlContext.Transforms.Concatenate("Features", Enumerable.Range(0, 235).Select(i => $"C{i.ToString(4)}").ToArray()))
				.Append(mlContext.MulticlassClassification.Trainers.LightGbm()
					.Append(mlContext.Transforms.Conversion.MapKeyToValue(Label, "Label")));

			var model = pipeline.Fit(trainValidationData.TrainSet);

			// Get all completed trials
			//var completedTrials = monitor.GetCompletedTrials();

			// Measure trained model performance
			// Apply data prep transformer to test data
			// Use trained model to make inferences on test data
			IDataView testDataPredictions = model.Transform(trainValidationData.TestSet);

			// Save model
			var savepath = $@"model\MultiClassification_{rank}_{index.ToString(2)}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

			var trained = mlContext.MulticlassClassification.Evaluate(testDataPredictions, labelColumnName: "Label");
			var now = await PredictionModel(rank, new MultiClassificationPredictionFactory(mlContext, rank, index, model)).RunAsync(x =>
				new MultiClassificationResult(savepath, rank, index, second, trained, x.score, x.rate)
			);
			var old = AppSetting.Instance.GetMultiClassificationResult(index, rank);
			var bst = old == MultiClassificationResult.Default || old.GetScore() < now.GetScore() ? now : old;

			AddLog($"=============== Result of MultiClassification Model Data {rank} {index} {second} ===============");
			AddLog($"LogLoss: {trained.LogLoss}");
			AddLog($"LogLossReduction: {trained.LogLossReduction}");
			AddLog($"MacroAccuracy: {trained.MacroAccuracy}");
			AddLog($"MicroAccuracy: {trained.MicroAccuracy}");
			AddLog($"TopKAccuracy: {trained.TopKAccuracy}");
			AddLog($"TopKPredictionCount: {trained.TopKPredictionCount}");
			AddLog($"Rate: {now.Rate:N4}     Score: {now.Score:N4}     S^2*R: {now.GetScore():N4}");
			AddLog($"=============== End Update of MultiClassification evaluation {rank} {index} {second} ===============");

			mlContext.Model.Save(model, data.Schema, savepath);

			AppSetting.Instance.UpdateMultiClassificationResults(bst, old);

			if (old != null && !bst.Equals(old) && await FileUtil.Exists(old.Path))
			{
				FileUtil.Delete(old.Path);
			}

			Progress.Value += 1;

			AppUtil.DeleteEndress(dataPath);
		}

		private Task CreateModelInputData(string path, string rank, Func<int, object> func_target)
		{
			return CreateModelInputData(path, rank, r => func_target(r.GetValue("着順").GetInt32()));
		}

		private async Task CreateModelInputData(string path, string rank, Func<DbDataReader, object> func_target)
		{
			FileUtil.BeforeCreate(path);

			AddLog($"Before Create: {path}");

			using (var conn = AppUtil.CreateSQLiteControl())
			using (var file = new FileAppendWriter(path))
			{
				using var reader = await conn.ExecuteReaderAsync("SELECT * FROM t_model WHERE 開催日数 <= ? AND ﾗﾝｸ1 = ? ORDER BY ﾚｰｽID, 馬番",
					SQLiteUtil.CreateParameter(DbType.Int64, tgtdate),
					SQLiteUtil.CreateParameter(DbType.Int64, AppUtil.ﾗﾝｸ2.Keys.IndexOf(rank))
				);

				var next = await reader.ReadAsync();

				if (!next) return;

				var first = AppUtil.ToSingles((byte[])reader.GetValue("Features"));
				var headers = Enumerable.Repeat("C", first.Length).Select((c, i) => $"{c}{i.ToString(4)}");

				await file.WriteLineAsync(Arr(Label, Group)
					.Concat(headers)
					.GetString(",")
				);
				await file.WriteLineAsync(Arr(func_target(reader), reader.GetValue("ﾚｰｽID").GetInt64())
					.Concat(first.Select(x => (object)x))
					.GetString(",")
				);

				while (next)
				{
					var features = AppUtil.ToSingles((byte[])reader.GetValue("Features"));
					await file.WriteLineAsync(Arr(func_target(reader), reader.GetValue("ﾚｰｽID").GetInt64())
						.Concat(features.Select(x => (object)x))
						.GetString(",")
					);
					next = await reader.ReadAsync();
				}
			}

			AddLog($"After Create: {path}");
		}

		private async Task<(float score, float rate)> PredictionModel<TSrc, TDst>(string rank, PredictionFactory<TSrc, TDst> factory) where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var pays = new (int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[]
				{
                    // 複2の予想結果
                    (200, "複2", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複3の予想結果
                    (300, "複3", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 1),
						arr.Where(x => x[j].GetInt32() <= 4),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複4aの予想結果
                    (400, "複4", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 4),
						arr.Where(x => x[j].GetInt32() <= 4),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // ワ1の予想結果
                    (100, "ワ1", (arr, payoutDetail, j) => Getワイド(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2))
					),
                    // ワ3の予想結果
                    (300, "ワ3", (arr, payoutDetail, j) => Getワイド(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 3))
					),
                    // 連1の予想結果
                    (100, "連1", (arr, payoutDetail, j) => Get馬連(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2))
					),
                    // 単勝1の予想結果
                    (100, "勝1", (arr, payoutDetail, j) => Get単勝(payoutDetail,
						arr.Where(x => x[j].GetInt32() == 1))
					),
				};

				var rets = new List<float>();

				foreach (var raceid in await conn.GetRows(r => r.Get<long>(0), "SELECT DISTINCT ﾚｰｽID FROM t_model WHERE 開催日数 > ? AND ﾗﾝｸ1 = ?",
						SQLiteUtil.CreateParameter(DbType.Int64, tgtdate),
						SQLiteUtil.CreateParameter(DbType.Int64, AppUtil.ﾗﾝｸ2.Keys.IndexOf(rank))
					))
				{
					var racs = new List<List<object>>();

					foreach (var m in await conn.GetRows("SELECT 馬番, ﾚｰｽID, Features FROM t_model WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.Int64, raceid)))
					{
						var tmp = new List<object>();

						// ｽｺｱ算出
						var scr = factory.Predict((byte[])m["Features"], m["ﾚｰｽID"].GetInt64());
						tmp.Add(scr);

						// 共通ﾍｯﾀﾞ
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(m["馬番"]);

						racs.Add(tmp);
					}

					// ｽｺｱで順位付けをする
					if (racs.Any())
					{
						var n = 1;
						racs.OrderByDescending(x => x[0].GetDouble()).ForEach(x => x.Add(n++));

						await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_payout (ﾚｰｽID,key,val, PRIMARY KEY (ﾚｰｽID,key))");

						// 支払情報を出力
						var payoutDetail = await conn.GetRows("SELECT * FROM t_payout WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid.ToString())).RunAsync(async rows =>
						{
							if (rows.Any())
							{
								return rows.ToDictionary(x => $"{x["key"]}", x => $"{x["val"]}");
							}
							else
							{
								return await GetPayout(raceid.ToString());
							}
						});

						// 結果の平均を結果に詰める
						rets.Add(pays.Select(x => x.func(racs, payoutDetail, 7).GetSingle()).Sum());

						await conn.BeginTransaction();
						foreach (var x in payoutDetail)
						{
							await conn.ExecuteNonQueryAsync("REPLACE INTO t_payout (ﾚｰｽID,key,val) VALUES (?,?,?)",
								SQLiteUtil.CreateParameter(DbType.String, raceid.ToString()),
								SQLiteUtil.CreateParameter(DbType.String, x.Key),
								SQLiteUtil.CreateParameter(DbType.String, x.Value)
							);
						}
						conn.Commit();
					}
				}

				return (
					rets.Any() ? Calc(rets.Sum(), rets.Count * pays.Sum(x => x.pay), (x, y) => x / y).GetSingle() : 0F,
					rets.Any() ? Calc(rets.Count(x => 0 < x), rets.Count, (x, y) => x / y).GetSingle() : 0F
				);
			}
		}

		private Dictionary<PredictionResult, Task<(float score, float rate)[]>> PredictionModel<TSrc, TDst>((int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[] pays, PredictionFactory<TSrc, TDst>[] factories) where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				return factories.ToDictionary(fac => fac.GetResult(), async fac =>
				{
					var rets = new List<float[]>();

					foreach (var raceid in await conn.GetRows(r => r.Get<long>(0), "SELECT DISTINCT ﾚｰｽID FROM t_model WHERE 開催日数 > ? AND ﾗﾝｸ1 = ?",
							SQLiteUtil.CreateParameter(DbType.Int64, tgtdate),
							SQLiteUtil.CreateParameter(DbType.Int64, AppUtil.ﾗﾝｸ2.Keys.IndexOf(fac.GetResult().Rank))
						))
					{
						var racs = new List<List<object>>();

						foreach (var m in await conn.GetRows("SELECT 馬番, ﾚｰｽID, Features FROM t_model WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.Int64, raceid)))
						{
							var tmp = new List<object>()
							{
								fac.Predict((byte[])m["Features"], m["ﾚｰｽID"].GetInt64()),
								string.Empty,
								string.Empty,
								string.Empty,
								string.Empty,
								string.Empty,
								m["馬番"],
							};

							racs.Add(tmp);
						}

						// ｽｺｱで順位付けをする
						if (racs.Any())
						{
							var n = 1;
							racs.OrderByDescending(x => x[0].GetDouble()).ForEach(x => x.Add(n++));

							await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_payout (ﾚｰｽID,key,val, PRIMARY KEY (ﾚｰｽID,key))");

							// 支払情報を出力
							var payoutDetail = await conn.GetRows("SELECT * FROM t_payout WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid.ToString())).RunAsync(async rows =>
							{
								if (rows.Any())
								{
									return rows.ToDictionary(x => $"{x["key"]}", x => $"{x["val"]}");
								}
								else
								{
									return await GetPayout(raceid.ToString());
								}
							});

							// 結果の平均を結果に詰める
							rets.Add(pays.Select(x => x.func(racs, payoutDetail, 7).GetSingle()).ToArray());

							await conn.BeginTransaction();
							foreach (var x in payoutDetail)
							{
								await conn.ExecuteNonQueryAsync("REPLACE INTO t_payout (ﾚｰｽID,key,val) VALUES (?,?,?)",
									SQLiteUtil.CreateParameter(DbType.String, raceid.ToString()),
									SQLiteUtil.CreateParameter(DbType.String, x.Key),
									SQLiteUtil.CreateParameter(DbType.String, x.Value)
								);
							}
							conn.Commit();
						}
					}

					return rets.Any()
						? pays.Select((pay, i) => (
							Calc(rets.Sum(x => x[i]), rets.Count * pay.pay, (s, p) => s / p).GetSingle(),
							Calc(rets.Count(x => 0 < x[i]), rets.Count, (c, l) => c / l).GetSingle()
						)).ToArray()
						: pays.Select(tmp => (0F, 0F)).ToArray();
				});
			}
		}

	}
}