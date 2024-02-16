using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Core;
using System.IO;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Data;
using static Microsoft.ML.DataOperationsCatalog;
using System.Threading;
using Microsoft.ML.AutoML;
using ControlzEx.Standard;
using System.Security.Cryptography;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Reflection;
using System.Windows.Media.TextFormatting;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		private readonly int[] TrainingTimeSecond = AppSetting.Instance.TrainingTimeSecond;

		public CheckboxItemModel S3B01 { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3B02 { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3B03 { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3B06 { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3B07 { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3B08 { get; } = new CheckboxItemModel("", "") { IsChecked = true };
		public CheckboxItemModel S3R01 { get; } = new CheckboxItemModel("", "") { IsChecked = true };

		public IRelayCommand S3EXEC => RelayCommand.Create(async _ =>
		{
			DirectoryUtil.DeleteInFiles("model", x => Path.GetExtension(x.FullName) == ".csv");

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum = AppSetting.Instance.TrainingTimeSecond.Length * Arr(S3B01, S3B02, S3B03, S3B06, S3B07, S3B08, S3R01).Count(x => x.IsChecked);

			AppSetting.Instance.Save();

			if (S3B01.IsChecked) await BinaryClassification(1).TryCatch();
			if (S3B02.IsChecked) await BinaryClassification(2).TryCatch();
			if (S3B03.IsChecked) await BinaryClassification(3).TryCatch();
			if (S3B06.IsChecked) await BinaryClassification(6, r => r.GetValue("着順").GetDouble() > 3).TryCatch();
			if (S3B07.IsChecked) await BinaryClassification(7, r => r.GetValue("着順").GetDouble() > 2).TryCatch();
			if (S3B08.IsChecked) await BinaryClassification(8, r => r.GetValue("着順").GetDouble() > 1).TryCatch();

			if (S3R01.IsChecked) await Regression().TryCatch();
		});

		private async Task BinaryClassification(int index, Func<DbDataReader, object> func_yoso)
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath, func_yoso);

			foreach (var second in AppSetting.Instance.TrainingTimeSecond)
			{
				// Infer column information
				var columnInference =
					mlContext.Auto().InferColumns(dataPath, labelColumnName: "着順", groupColumns: true);

				// Create text loader
				TextLoader loader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);

				// Load data into IDataView
				IDataView data = loader.Load(dataPath);

				// Split into train (80%), validation (20%) sets
				TrainTestData trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

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

				// Create AutoML experiment
				AutoMLExperiment experiment = mlContext.Auto().CreateExperiment();

				// Configure experiment
				experiment
					.SetPipeline(pipeline)
					.SetBinaryClassificationMetric(AppSetting.Instance.BinaryClassificationMetric, labelColumn: columnInference.ColumnInformation.LabelColumnName)
					.SetTrainingTimeInSeconds((uint)second)
					.SetEciCostFrugalTuner()
					.SetDataset(trainValidationData);

				// Log experiment trials
				var monitor = new AutoMLMonitor(pipeline, this);
				experiment.SetMonitor(monitor);

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
				var savepath = $@"model\BinaryClassification{index.ToString(2)}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

				var trainedModelMetrics = mlContext.BinaryClassification.Evaluate(testDataPredictions, labelColumnName: "着順");
				var now = new BinaryClassificationResult(savepath, index, second, trainedModelMetrics);
				var old = AppSetting.Instance.BinaryClassificationResults.FirstOrDefault(x => x.Index == index);
				var bst = old == null || old.AreaUnderRocCurve < now.AreaUnderRocCurve ? now : old;

				AddLog($"=============== Begin Update of BinaryClassification evaluation {index} {second} ===============");
				AddLog($"Accuracy: {trainedModelMetrics.Accuracy}");
				AddLog($"AreaUnderPrecisionRecallCurve: {trainedModelMetrics.AreaUnderPrecisionRecallCurve}");
				AddLog($"AreaUnderRocCurve: {trainedModelMetrics.AreaUnderRocCurve}");
				AddLog($"Entropy: {trainedModelMetrics.Entropy}");
				AddLog($"F1Score: {trainedModelMetrics.F1Score}");
				AddLog($"LogLoss: {trainedModelMetrics.LogLoss}");
				AddLog($"LogLossReduction: {trainedModelMetrics.LogLossReduction}");
				AddLog($"NegativePrecision: {trainedModelMetrics.NegativePrecision}");
				AddLog($"NegativeRecall: {trainedModelMetrics.NegativeRecall}");
				AddLog($"PositivePrecision: {trainedModelMetrics.PositivePrecision}");
				AddLog($"PositiveRecall: {trainedModelMetrics.PositiveRecall}");
				AddLog($"{trainedModelMetrics.ConfusionMatrix.GetFormattedConfusionTable()}");
				AddLog($"=============== End Update of BinaryClassification evaluation {index} {second} ===============");

				mlContext.Model.Save(model, data.Schema, savepath);

				AppSetting.Instance.UpdateBinaryClassificationResults(now);

				Progress.Value += 1;
			}

			AppUtil.DeleteEndress(dataPath);
		}

		private Task BinaryClassification(int index)
		{
			return BinaryClassification(index, reader => reader.GetValue("着順").GetDouble() <= index);
		}

		private async Task Regression()
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath, reader => reader.GetValue("着順").GetDouble());

			foreach (var second in TrainingTimeSecond)
			{
				// Infer column information
				var columnInference =
					mlContext.Auto().InferColumns(dataPath, labelColumnName: "着順", groupColumns: true);

				// Create text loader
				TextLoader loader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);

				// Load data into IDataView
				IDataView data = loader.Load(dataPath);

				// Split into train (80%), validation (20%) sets
				TrainTestData trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.2);

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

				// Create AutoML experiment
				AutoMLExperiment experiment = mlContext.Auto().CreateExperiment();

				// Configure experiment
				experiment
					.SetPipeline(pipeline)
					.SetRegressionMetric(AppSetting.Instance.RegressionMetric, "着順")
					//.SetBinaryClassificationMetric(BinaryClassificationMetric.Accuracy, labelColumn: columnInference.ColumnInformation.LabelColumnName)
					.SetTrainingTimeInSeconds((uint)second)
					.SetEciCostFrugalTuner()
					.SetDataset(trainValidationData);

				// Log experiment trials
				var monitor = new AutoMLMonitor(pipeline, this);
				experiment.SetMonitor(monitor);

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
				var savepath = $@"model\Regression{1.ToString(2)}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

				var trainedModelMetrics = mlContext.Regression.Evaluate(testDataPredictions, labelColumnName: "着順");
				var now = new RegressionResult(savepath, 1, second, trainedModelMetrics);
				var old = AppSetting.Instance.RegressionResults.FirstOrDefault(x => x.Index == 1);
				var bst = old == null || old.RSquared < now.RSquared ? now : old;

				AddLog($"=============== Begin of Regression evaluation {second} ===============");
				AddLog($"RSquared: {trainedModelMetrics.RSquared}");
				AddLog($"MeanSquaredError: {trainedModelMetrics.MeanSquaredError}");
				AddLog($"RootMeanSquaredError: {trainedModelMetrics.RootMeanSquaredError}");
				AddLog($"LossFunction: {trainedModelMetrics.LossFunction}");
				AddLog($"MeanAbsoluteError: {trainedModelMetrics.MeanAbsoluteError}");
				AddLog($"=============== End of Regression evaluation {second} ===============");

				mlContext.Model.Save(model, data.Schema, savepath);

				AppSetting.Instance.UpdateRegressionResults(now);

				Progress.Value += 1;
			}

			AppUtil.DeleteEndress(dataPath);
		}

		private async Task CreateModelInputData(string path, Func<DbDataReader, object> func_target)
		{
			FileUtil.BeforeCreate(path);

			AddLog($"Before Create: {path}");

			using (var conn = CreateSQLiteControl())
			using (var file = new FileAppendWriter(path))
			{
				using var reader = await conn.ExecuteReaderAsync("SELECT * FROM t_model ORDER BY ﾚｰｽID, 着順");

				var next = await reader.ReadAsync();

				if (!next) return;

				var indexes = Enumerable.Range(0, reader.FieldCount)
					.Where(i => !new[] { "着順", "単勝", "人気" }.Contains(reader.GetName(i)) && (i == 0 || !Enumerable.Range(0, i - 1).Any(x => reader.GetName(i) == reader.GetName(x))))
					.ToArray();

				await file.WriteLineAsync(indexes.Select(i => reader.GetName(i)).GetString(",") + ",着順");

				while (next)
				{
					await file.WriteLineAsync(indexes.Select(i => reader.GetValue(i)).GetString(",") + $",{func_target(reader)}");
					next = await reader.ReadAsync();
				}
			}

			AddLog($"After Create: {path}");
		}

	}
}