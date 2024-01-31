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
		private readonly int[] TrainingTimeSecond = new int[] { 128, 256, 512, 1024, 2048, 4096 };

		public IRelayCommand S3EXEC => RelayCommand.Create(async _ =>
		{
			//await MulticlassClassification().TryCatch();

			//await BinaryClassification(1).TryCatch();
			//await BinaryClassification(2).TryCatch();
			//await BinaryClassification(3).TryCatch();
			await BinaryClassification(4).TryCatch();
			await BinaryClassification(5).TryCatch();
			await BinaryClassification(6, r => r.GetValue("着順").GetDouble() > 3).TryCatch();
			await BinaryClassification(7, r => r.GetValue("着順").GetDouble() > 5).TryCatch();
			await BinaryClassification(8, r => r.GetValue("着順").GetDouble() > 7).TryCatch();

			await Regression().TryCatch();

			DirectoryUtil.Copy("model", $"model_{DateTime.Now.ToString("yyyyMMddHHmmss")}");
		});

		private async Task MulticlassClassification()
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath, reader => int.Parse($"{reader.GetValue("着順")}"));

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
					.Append(mlContext.Auto().MultiClassification(labelColumnName: columnInference.ColumnInformation.LabelColumnName));

			// Create AutoML experiment
			AutoMLExperiment experiment = mlContext.Auto().CreateExperiment();

			// Configure experiment
			experiment
				.SetPipeline(pipeline)
				.SetMulticlassClassificationMetric(MulticlassClassificationMetric.LogLossReduction, labelColumn: columnInference.ColumnInformation.LabelColumnName)
				.SetTrainingTimeInSeconds(240)
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
			mlContext.Model.Save(model, data.Schema, @"model\MulticlassClassification.zip");

			var trainedModelMetrics = mlContext.MulticlassClassification.Evaluate(testDataPredictions, labelColumnName: "着順");

			AddLog("=============== Begin of MulticlassClassification evaluation ===============");
			AddLog($"ConfusionMatrix: {trainedModelMetrics.ConfusionMatrix}");
			AddLog($"LogLoss: {trainedModelMetrics.LogLoss}");
			AddLog($"LogLossReduction: {trainedModelMetrics.LogLossReduction}");
			AddLog($"MacroAccuracy: {trainedModelMetrics.MacroAccuracy}");
			AddLog($"MicroAccuracy: {trainedModelMetrics.MicroAccuracy}");
			AddLog($"PerClassLogLoss: {trainedModelMetrics.PerClassLogLoss}");
			AddLog($"TopKAccuracy: {trainedModelMetrics.TopKAccuracy}");
			AddLog($"TopKAccuracyForAllK: {trainedModelMetrics.TopKAccuracyForAllK}");
			AddLog($"TopKPredictionCount: {trainedModelMetrics.TopKPredictionCount}");
			AddLog("=============== End of MulticlassClassification evaluation ===============");
		}

		private async Task BinaryClassification(int index, Func<DbDataReader, object> func_yoso)
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath, func_yoso);

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
						.Append(mlContext.Auto().BinaryClassification(labelColumnName: columnInference.ColumnInformation.LabelColumnName));

				// Create AutoML experiment
				AutoMLExperiment experiment = mlContext.Auto().CreateExperiment();

				// Configure experiment
				experiment
					.SetPipeline(pipeline)
					.SetBinaryClassificationMetric(BinaryClassificationMetric.Accuracy, labelColumn: columnInference.ColumnInformation.LabelColumnName)
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
				mlContext.Model.Save(model, data.Schema, $@"model\BinaryClassification{index.ToString(2)}_{second.ToString(4)}.zip");

				var trainedModelMetrics = mlContext.BinaryClassification.Evaluate(testDataPredictions, labelColumnName: "着順");

				AddLog($"=============== Begin of BinaryClassification evaluation {index} {second} ===============");
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
				AddLog($"=============== End of BinaryClassification evaluation {index} {second} ===============");
			}

			FileUtil.Delete(dataPath);
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
						.Append(mlContext.Auto().Regression(labelColumnName: columnInference.ColumnInformation.LabelColumnName));

				// Create AutoML experiment
				AutoMLExperiment experiment = mlContext.Auto().CreateExperiment();

				// Configure experiment
				experiment
					.SetPipeline(pipeline)
					.SetRegressionMetric(RegressionMetric.RSquared, "着順")
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
				mlContext.Model.Save(model, data.Schema, @$"model\Regression01_{second.ToString(4)}.zip");

				var trainedModelMetrics = mlContext.Regression.Evaluate(testDataPredictions, labelColumnName: "着順");

				AddLog($"=============== Begin of Regression evaluation {second} ===============");
				AddLog($"RSquared: {trainedModelMetrics.RSquared}");
				AddLog($"MeanSquaredError: {trainedModelMetrics.MeanSquaredError}");
				AddLog($"RootMeanSquaredError: {trainedModelMetrics.RootMeanSquaredError}");
				AddLog($"LossFunction: {trainedModelMetrics.LossFunction}");
				AddLog($"MeanAbsoluteError: {trainedModelMetrics.MeanAbsoluteError}");
				AddLog($"=============== End of Regression evaluation {second} ===============");
			}

			FileUtil.Delete(dataPath);
		}

		private async Task CreateModelInputData(string path, Func<DbDataReader, object> func_target)
		{
			FileUtil.BeforeCreate(path);

			AddLog($"Before Create: {path}");

			using (var conn = CreateSQLiteControl())
			{
				var mindate = await conn.ExecuteScalarAsync<int>("SELECT cast((MAX(開催日数) - MIN(開催日数)) * 0.2 as integer) + MIN(開催日数) FROM t_model");

				using var reader = await conn.ExecuteReaderAsync(
					"SELECT * FROM t_model WHERE 開催日数 >= ? ORDER BY ﾚｰｽID, 着順",
					SQLiteUtil.CreateParameter(DbType.Int64, mindate)
				);

				var list = new List<string>();
				var next = await reader.ReadAsync();

				Func<DbDataReader, IEnumerable<int>> func = r =>
				{
					var indexes = Enumerable.Range(0, r.FieldCount)
						.Where(i => !new[] { "着順", "単勝", "人気" }.Contains(r.GetName(i)) && (i == 0 || !Enumerable.Range(0, i - 1).Any(x => r.GetName(i) == r.GetName(x))))
						.ToArray();
					return indexes;
				};

				if (next)
				{
					list.Add(func(reader).Select(i => reader.GetName(i)).GetString(",") + ",着順");
				}

				while (next)
				{
					list.Add(func(reader).Select(i => reader.GetValue(i)).GetString(",") + $",{func_target(reader)}");

					if (10000 < list.Count)
					{
						await File.AppendAllTextAsync(path, list.GetString("\r\n") + "\r\n");

						list.Clear();
					}

					next = await reader.ReadAsync();
				}

				if (list.Any())
				{
					await File.AppendAllTextAsync(path, list.GetString("\r\n") + "\r\n");
				}
			}

			AddLog($"After Create: {path}");
		}

	}
}