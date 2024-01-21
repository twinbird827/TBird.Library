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

			//var dataPath = @"C:\Work\GitHub\TBird.Library\_Apps\bin\Debug\net7.0-windows\step5\inputdata.csv";

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
					.Append(mlContext.Auto().BinaryClassification(labelColumnName: columnInference.ColumnInformation.LabelColumnName, useFastForest: false, useLgbm: true));

			// Create AutoML experiment
			AutoMLExperiment experiment = mlContext.Auto().CreateExperiment();

			// Configure experiment
			experiment
				.SetPipeline(pipeline)
				.SetBinaryClassificationMetric(BinaryClassificationMetric.Accuracy, labelColumn: columnInference.ColumnInformation.LabelColumnName)
				.SetTrainingTimeInSeconds(120)
				.SetEciCostFrugalTuner()
				.SetDataset(trainValidationData);

			// Log experiment trials
			var monitor = new AutoMLMonitor(pipeline);
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
			mlContext.Model.Save(model, data.Schema, "model.zip");
			using FileStream stream = File.Create("./onnx_model.onnx");

			var trainedModelMetrics = mlContext.BinaryClassification.Evaluate(testDataPredictions, labelColumnName: "着順");

			Console.WriteLine();
			Console.WriteLine("Model quality metrics evaluation");
			Console.WriteLine("--------------------------------");
			Console.WriteLine($"Accuracy: {trainedModelMetrics.Accuracy:P2}");
			Console.WriteLine($"Auc: {trainedModelMetrics.AreaUnderRocCurve:P2}");
			Console.WriteLine($"F1Score: {trainedModelMetrics.F1Score:P2}");
			Console.WriteLine("=============== End of model evaluation ===============");

			// Load Trained Model
			DataViewSchema predictionPipelineSchema;
			ITransformer predictionPipeline = model;

			//// Create PredictionEngines
			//PredictionEngine<Passenger, TitanicPrediction> predictionEngine = mlContext.Model.CreatePredictionEngine<Passenger, TitanicPrediction>(predictionPipeline);

			//// Input Data
			//var inputData = new List<Passenger>()
			//{
			//	new Passenger()
			//	{
			//		PassengerId = 1,
			//		Pclass = 3,
			//		Name = "Braund, Mr. Owen Harris",
			//		Sex = "male",
			//		Age = 22,
			//		SibSp = 1,
			//		Parch = 0,
			//		Ticket = "A/5 21171",
			//		Fare = 7.25f,
			//		Cabin = "",
			//		Embarked = "S"
			//	},
			//	new Passenger()
			//	{
			//		PassengerId = 2,
			//		Pclass = 1,
			//		Name = "Cumings, Mrs. John Bradley (Florence Briggs Thayer)",
			//		Sex = "female",
			//		Age = 38,
			//		SibSp = 1,
			//		Parch = 0,
			//		Ticket = "PC 17599",
			//		Fare = 71.2833f,
			//		Cabin = "C85",
			//		Embarked = "C"
			//	},
			//	new Passenger()
			//	{
			//		PassengerId = 3,
			//		Pclass = 3,
			//		Name = "Heikkinen, Miss. Laina",
			//		Sex = "female",
			//		Age = 26,
			//		SibSp = 0,
			//		Parch = 0,
			//		Ticket = "STON/O2. 3101282",
			//		Fare = 7.925f,
			//		Cabin = "",
			//		Embarked = "S"
			//	},
			//};

			//// Get Prediction
			//foreach (var input in inputData)
			//{
			//	var prediction = predictionEngine.Predict(input);

			//	Console.WriteLine($"Id:{input.PassengerId} Name:{input.Name} Survived:{prediction.PredictedSurvived}");
			//}

		});

		private async Task CreateModelInputData(string path)
		{
			FileUtil.BeforeCreate(path);

			using (var sw = new StreamWriter(path, true, Encoding.UTF8, 5 * 1024 * 1024))
			using (var conn = CreateSQLiteControl())
			using (var reader = await conn.ExecuteReaderAsync("SELECT * FROM t_model ORDER BY ﾚｰｽID, 着順"))
			{
				Func<DbDataReader, Dictionary<string, object>> func = r =>
				{
					var indexes = Enumerable.Range(0, r.FieldCount)
						.Where(i => i == 0 || !Enumerable.Range(0, i - 1).Any(x => r.GetName(i) == r.GetName(x)))
						.ToArray();
					return indexes.ToDictionary(i => r.GetName(i), i => r.GetValue(i));
				};

				// 不要なﾃﾞｰﾀ
				var drops = new[] { "着順", "単勝", "人気", "上り", "時間", "通過", "着差", "ﾗﾝｸ1", "ﾗﾝｸ2" };

				// 予測したい内容
				Func<Dictionary<string, object>, bool> yoso = x => x["着順"].GetDouble() <= 3;

				bool first = true;
				while (await reader.ReadAsync())
				{
					var dic = func(reader);
					var withoutdrops = dic.Keys.Where(x => !drops.Contains(x));

					if (first)
					{
						first = false;
						// 最初はﾍｯﾀﾞ行を追加
						await sw.WriteLineAsync(withoutdrops.GetString(",") + ",着順");
					}

					// 明細行を追加
					await sw.WriteLineAsync(withoutdrops.Select(x => dic[x]).GetString(",") + $",{yoso(dic)}");
				}
			}
		}
	}
}