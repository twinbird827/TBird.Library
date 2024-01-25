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
		public IRelayCommand S3EXEC => RelayCommand.Create(async _ =>
		{
			// Initialize MLContext
			MLContext mlContext = new MLContext();

			// ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
			var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

			// ﾃﾞｰﾀﾌｧｲﾙを作製する
			await CreateModelInputData(dataPath);

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
			mlContext.Model.Save(model, data.Schema, @"model\model.zip");

			var trainedModelMetrics = mlContext.BinaryClassification.Evaluate(testDataPredictions, labelColumnName: "着順");

			AddLog($"Accuracy: {trainedModelMetrics.Accuracy:P2}");
			AddLog($"Auc: {trainedModelMetrics.AreaUnderRocCurve:P2}");
			AddLog($"F1Score: {trainedModelMetrics.F1Score:P2}");
			AddLog("=============== End of model evaluation ===============");
		});

		private async Task CreateModelInputData(string path)
		{
			FileUtil.BeforeCreate(path);

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
					list.Add(func(reader).Select(i => reader.GetValue(i)).GetString(",") + $",{reader.GetValue("着順").GetDouble() <= 3}");

					if (10000 < list.Count)
					{
						await File.AppendAllLinesAsync(path, list);

						list.Clear();
					}

					next = await reader.ReadAsync();
				}
			}
		}

	}
}