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
using Microsoft.ML.Trainers.LightGbm;
using Microsoft.ML.AutoML.CodeGen;
using Microsoft.ML.SearchSpace;
using Microsoft.ML.SearchSpace.Option;
using TBird.Wpf.Collections;
using ICSharpCode.SharpZipLib.Core;
using Tensorflow;
using MathNet.Numerics.RootFinding;

namespace Netkeiba
{
	public partial class MainViewModel
	{
		private const string Label = "着順";

		public BindableCollection<CheckboxItemModel> CreateModelSources { get; } = new BindableCollection<CheckboxItemModel>();

		public BindableContextCollection<CheckboxItemModel> CreateModels { get; }

		public IRelayCommand S3EXECCHECK => RelayCommand.Create(_ =>
		{
			var check = CreateModelSources.Count(x => !x.IsChecked) < CreateModelSources.Count(x => x.IsChecked);

			CreateModelSources.ForEach(x => x.IsChecked = !check);
		});

		public IRelayCommand S3EXEC => RelayCommand.Create(async _ =>
		{
			var seconds = AppSetting.Instance.TrainingCount;
			var metrics = Arr(BinaryClassificationMetric.AreaUnderRocCurve, BinaryClassificationMetric.F1Score);

			DirectoryUtil.DeleteInFiles("model", x => Path.GetExtension(x.FullName) == ".csv");

			Progress.Value = 0;
			Progress.Minimum = 0;
			Progress.Maximum =
				seconds * CreateModels.Count(x => x.IsChecked && x.Value.StartsWith("B-")) * metrics.Length +
				seconds * CreateModels.Count(x => x.IsChecked && x.Value.StartsWith("R-"));

			AppSetting.Instance.Save();

			Func<DbDataReader, (float 着順, float 単勝)> 着勝 = r => (r.GetValue("着順").GetSingle(), r.GetValue("単勝").GetSingle());

			var dic = new Dictionary<int, Func<DbDataReader, object>>()
			{
				{ 1, r => 着勝(r).Run(x => 50 < x.単勝 && x.着順 <= 1) },
				{ 2, r => 着勝(r).Run(x => 50 < x.単勝 && x.着順 <= 2) },
				{ 3, r => 着勝(r).Run(x => 50 < x.単勝 && x.着順 <= 3) },
				{ 6, r => 着勝(r).Run(x => 50 < x.単勝 && x.着順 > 1) },
				{ 7, r => 着勝(r).Run(x => 50 < x.単勝 && x.着順 > 2) },
				{ 8, r => 着勝(r).Run(x => 50 < x.単勝 && x.着順 > 3) },
			};

			var random = new Random();
			for (var tmp = 0; tmp < seconds; tmp++)
			{
				var second = (uint)random.Next((int)AppSetting.Instance.MinimumTrainingTimeSecond, (int)AppSetting.Instance.MaximumTrainingTimeSecond);

				foreach (var metric in metrics)
				{
					foreach (var x in CreateModels.Where(x => x.IsChecked && x.Value.StartsWith("B-")))
					{
						var args = x.Value.Split("-");
						var index = args[2].GetInt32();
						var rank = args[1];

						await BinaryClassification(index, rank, second, metric, dic[index]).TryCatch();
					}
				}

				foreach (var x in CreateModels.Where(x => x.IsChecked && x.Value.StartsWith("R-")))
				{
					var args = x.Value.Split("-");
					await Regression(args[1], second).TryCatch();
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
			var columnInference =
				mlContext.Auto().InferColumns(dataPath, labelColumnName: Label, groupColumns: true);

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

			// Log experiment trials
			var monitor = new AutoMLMonitor(pipeline, this);

			// Create AutoML experiment
			var experiment = mlContext.Auto().CreateExperiment()
				.SetPipeline(pipeline)
				.SetBinaryClassificationMetric(AppSetting.Instance.BinaryClassificationMetric, labelColumn: Label)
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
			var savepath = $@"model\BinaryClassification_{rank}_{index.ToString(2)}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

			var trainedModelMetrics = mlContext.BinaryClassification.Evaluate(testDataPredictions, labelColumnName: Label);
			var now = new BinaryClassificationResult(savepath, rank, index, second, trainedModelMetrics,
				await PredictionModel(rank,
					index < 5 ? mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(model) : null,
					4 < index ? mlContext.Model.CreatePredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>(model) : null,
					null
				)
			);
			var old = AppSetting.Instance.GetBinaryClassificationResult(index, rank);
			var bst = old == null || old.Score < now.Score ? now : old;

			AddLog($"=============== Result of BinaryClassification Model Data {rank} {index} {second} ===============");
			AddLog($"Accuracy: {trainedModelMetrics.Accuracy}");
			AddLog($"AreaUnderPrecisionRecallCurve: {trainedModelMetrics.AreaUnderPrecisionRecallCurve}");
			AddLog($"Entropy: {trainedModelMetrics.Entropy}");
			AddLog($"F1Score: {trainedModelMetrics.F1Score}");
			AddLog($"LogLoss: {trainedModelMetrics.LogLoss}");
			AddLog($"LogLossReduction: {trainedModelMetrics.LogLossReduction}");
			AddLog($"NegativePrecision: {trainedModelMetrics.NegativePrecision}");
			AddLog($"NegativeRecall: {trainedModelMetrics.NegativeRecall}");
			AddLog($"PositivePrecision: {trainedModelMetrics.PositivePrecision}");
			AddLog($"PositiveRecall: {trainedModelMetrics.PositiveRecall}");
			AddLog($"{trainedModelMetrics.ConfusionMatrix.GetFormattedConfusionTable()}");
			AddLog($"AreaUnderRocCurve: {trainedModelMetrics.AreaUnderRocCurve}");
			AddLog($"Score: {now.Score}");
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
			await CreateModelInputData(dataPath, rank, reader => reader.GetValue("着順").GetDouble());

			AddLog($"=============== Begin of Regression evaluation {rank} {second} ===============");

			// Infer column information
			var columnInference =
				mlContext.Auto().InferColumns(dataPath, labelColumnName: Label, groupColumns: true);

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

			var trainedModelMetrics = mlContext.Regression.Evaluate(testDataPredictions, labelColumnName: Label);
			var now = new RegressionResult(savepath, rank, 1, second, trainedModelMetrics,
				await PredictionModel(rank, null, null, mlContext.Model.CreatePredictionEngine<RegressionSource, RegressionPrediction>(model))
			);
			var old = AppSetting.Instance.GetRegressionResult(1, rank);
			var bst = old == null || old.Score < now.Score ? now : old;

			AddLog($"=============== Result of Regression Model Data {rank} {second} ===============");
			AddLog($"MeanSquaredError: {trainedModelMetrics.MeanSquaredError}");
			AddLog($"RootMeanSquaredError: {trainedModelMetrics.RootMeanSquaredError}");
			AddLog($"LossFunction: {trainedModelMetrics.LossFunction}");
			AddLog($"MeanAbsoluteError: {trainedModelMetrics.MeanAbsoluteError}");
			AddLog($"RSquared: {trainedModelMetrics.RSquared}");
			AddLog($"Score: {now.Score}");
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

		private Task CreateModelInputData(string path, string rank, Func<DbDataReader, object> func_target)
		{
			return CreateModelInputData(path, rank, func_target, head => head.Concat(Arr(Label)), (x, r) => x);
		}

		private async Task CreateModelInputData(string path, string rank, Func<DbDataReader, object> func_target, Func<IEnumerable<string>, IEnumerable<string>> func_head, Func<IEnumerable<object>, DbDataReader, IEnumerable<object>> func_row)
		{
			FileUtil.BeforeCreate(path);

			AddLog($"Before Create: {path}");

			using (var conn = CreateSQLiteControl())
			using (var file = new FileAppendWriter(path))
			{
				var ﾗﾝｸ2 = await AppUtil.Getﾗﾝｸ2(conn);
				using var reader = await conn.ExecuteReaderAsync("SELECT * FROM t_model WHERE ﾗﾝｸ2 = ? ORDER BY ﾚｰｽID, 馬番", SQLiteUtil.CreateParameter(DbType.Int64, ﾗﾝｸ2.IndexOf(rank)));

				var next = await reader.ReadAsync();

				if (!next) return;

				var first = func_row(GetReaderRows(reader, func_target), reader).ToArray();
				var headers = Enumerable.Repeat("COL", first.Length - 1).Select((c, i) => $"{c}{i.ToString(4)}");

				await file.WriteLineAsync(func_head(headers).GetString(","));
				await file.WriteLineAsync(first.GetString(","));

				while (next)
				{
					await file.WriteLineAsync(func_row(GetReaderRows(reader, func_target), reader).GetString(","));
					next = await reader.ReadAsync();
				}
			}

			AddLog($"After Create: {path}");
		}

		private IEnumerable<object> GetReaderRows(DbDataReader reader, Func<DbDataReader, object> func_target) => ((byte[])reader.GetValue("Features"))
			.Run(bytes => Enumerable.Range(0, bytes.Length / 4).Select(i => (object)BitConverter.ToSingle(bytes, i * 4)))
			.Concat(Arr(func_target(reader)));

		private async Task<float> PredictionModel(string rank,
			PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>? bin,
			PredictionEngine<BinaryClassificationSource, BinaryClassificationPrediction>? tya,
			PredictionEngine<RegressionSource, RegressionPrediction>? reg)
		{
			using (var conn = CreateSQLiteControl())
			{
				(int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[] pays = new (int pay, string head, Func<List<List<object>>, Dictionary<string, string>, int, object> func)[]
				{
                    // 複2の予想結果
                    (200, "複2", (arr, payoutDetail, j) => Get三連複(payoutDetail,
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 2),
						arr.Where(x => x[j].GetInt32() <= 4))
					),
                    // 複2の予想結果
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
				var ﾗﾝｸ2 = await AppUtil.Getﾗﾝｸ2(conn);
				var 馬性 = await AppUtil.Get馬性(conn);
				var 調教場所 = await AppUtil.Get調教場所(conn);
				var 追切 = await AppUtil.Get追切(conn);

				var raceids = GetRaceIds().ToArray();

				// ﾚｰｽﾃﾞｰﾀ取得→なかったら次へ
				var racearrs = await raceids.Select(raceid => GetRaceShutubas(raceid).RunAsync(async arr =>
				{
					if (arr.Count == 0) return;

					if (arr.First()["ﾗﾝｸ2"] != rank) return;

					// 着順情報取得
					var tyaku = await GetTyakujun(raceid);

					// 追切情報取得
					var oikiri = await GetOikiris(raceid);

					await arr.Select(async row =>
					{
						var tya = tyaku.FirstOrDefault(x => x["枠番"] == row["枠番"] && x["馬番"] == row["馬番"]);
						row["着順"] = tya != null ? tya["着順"] : string.Empty;

						var oik = oikiri.FirstOrDefault(x => x["枠番"] == row["枠番"] && x["馬番"] == row["馬番"]);
						row["一言"] = oik != null ? oik["一言"] : string.Empty;
						row["追切"] = oik != null ? oik["追切"] : string.Empty;

						var ban = await conn
							.GetRows<string>("SELECT 馬主名, 馬主ID FROM t_orig WHERE 馬ID = ? LIMIT 1", SQLiteUtil.CreateParameter(DbType.String, row["馬ID"]))
							.RunAsync(async tmp =>
							{
								if (0 < tmp.Count)
								{
									return tmp[0];
								}
								else
								{
									return await GetBanushi(row["馬ID"]);
								}
							});
						row["馬主名"] = ban["馬主名"];
						row["馬主ID"] = ban["馬主ID"];
					}).WhenAll();
				})).WhenAll();

				// 馬ID
				var 馬IDs = racearrs.SelectMany(arr => arr.Select(x => x["馬ID"]).Distinct());

				// 血統情報の作成
				await RefreshKetto(conn, 馬IDs);

				// 産駒成績の更新
				await RefreshSanku(conn, true, 馬IDs);

				foreach (var racearr in racearrs)
				{
					if (!racearr.Any()) continue;
					if (racearr.First()["ﾗﾝｸ2"] != rank) continue;

					var raceid = racearr.First()["ﾚｰｽID"];

					await conn.BeginTransaction();

					// 元ﾃﾞｰﾀにﾚｰｽﾃﾞｰﾀがあれば削除してから取得したﾚｰｽﾃﾞｰﾀを挿入する
					await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid));
					foreach (var x in racearr)
					{
						var sql = "INSERT INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
						var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
						await conn.ExecuteNonQueryAsync(sql, prm);
					}

					var arr = new List<List<object>>();

					// ﾚｰｽ情報の初期化
					await InitializeModelBase(conn);

					// ﾓﾃﾞﾙﾃﾞｰﾀ作成
					foreach (var m in await CreateRaceModel(conn, raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切))
					{
						var tmp = new List<object>();
						var src = racearr.First(x => x["馬ID"].GetInt64() == (long)m["馬ID"]);

						var binaryClassificationSource = new BinaryClassificationSource()
						{
							Features = (AppSetting.Instance.Features ?? throw new ArgumentNullException()).Select(x => m[x].GetSingle()).ToArray()
						};
						var regressionSource = new RegressionSource()
						{
							Features = binaryClassificationSource.Features
						};

						// ｽｺｱ算出
						var score = bin != null
							? bin.Predict(binaryClassificationSource).GetScore2()
							: tya != null
							? tya.Predict(binaryClassificationSource).Run(x => { x.Score = x.Score * -1; }).GetScore2()
							: reg.Predict(regressionSource).Score * -1;
						tmp.Add(score);

						// 共通ﾍｯﾀﾞ
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(string.Empty);
						tmp.Add(src["着順"]);

						arr.Add(tmp);
					}

					// ｽｺｱで順位付けをする
					if (arr.Any())
					{
						var n = 1;
						arr.OrderBy(x => x[0].GetDouble()).ForEach(x => x.Add(n++));

						// 支払情報を出力
						var payoutDetail = await GetPayout(raceid);

						// 結果の平均を結果に詰める
						rets.Add(pays.Select(x => x.func(arr, payoutDetail, 7).GetSingle()).Sum());
					}

					conn.Rollback();
				}

				return rets.Any() ? Calc(rets.Sum(), rets.Count * pays.Sum(x => x.pay), (x, y) => x / y).GetSingle() : 0F;
			}
		}
	}
}