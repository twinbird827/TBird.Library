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

namespace Netkeiba
{
    public partial class MainViewModel
    {
        private const string Label = "着順";

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

            foreach (var rank in Arr("RANK1", "RANK2", "RANK3", "RANK4"))
            {
                if (S3B01.IsChecked) await BinaryClassification(1, rank).TryCatch();
                if (S3B02.IsChecked) await BinaryClassification(2, rank).TryCatch();
                if (S3B03.IsChecked) await BinaryClassification(3, rank).TryCatch();
                if (S3B06.IsChecked) await BinaryClassification(6, rank, r => r.GetValue("着順").GetDouble() > 3).TryCatch();
                if (S3B07.IsChecked) await BinaryClassification(7, rank, r => r.GetValue("着順").GetDouble() > 2).TryCatch();
                if (S3B08.IsChecked) await BinaryClassification(8, rank, r => r.GetValue("着順").GetDouble() > 1).TryCatch();

                if (S3R01.IsChecked) await Regression(rank).TryCatch();
            }
        });

        private async Task BinaryClassification(int index, string rank, Func<DbDataReader, object> func_yoso)
        {
            // Initialize MLContext
            MLContext mlContext = new MLContext();

            // ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
            var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

            // ﾃﾞｰﾀﾌｧｲﾙを作製する
            await CreateModelInputData(dataPath, rank, func_yoso);

            foreach (var second in AppSetting.Instance.TrainingTimeSecond)
            {
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
                var now = new BinaryClassificationResult(savepath, rank, index, second, trainedModelMetrics);
                var old = AppSetting.Instance.BinaryClassificationResults.FirstOrDefault(x => x.Index == index);
                var bst = old == null || old.AreaUnderRocCurve < now.AreaUnderRocCurve ? now : old;

                AddLog($"=============== Begin Update of BinaryClassification evaluation {rank} {index} {second} ===============");
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
                AddLog($"=============== End Update of BinaryClassification evaluation {rank} {index} {second} ===============");

                mlContext.Model.Save(model, data.Schema, savepath);

                AppSetting.Instance.UpdateBinaryClassificationResults(now);

                Progress.Value += 1;
            }

            AppUtil.DeleteEndress(dataPath);
        }

        private Task BinaryClassification(int index, string rank)
        {
            return BinaryClassification(index, rank, reader => reader.GetValue("着順").GetDouble() <= index);
        }

        private async Task Regression(string rank)
        {
            // Initialize MLContext
            MLContext mlContext = new MLContext();

            // ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
            var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

            // ﾃﾞｰﾀﾌｧｲﾙを作製する
            await CreateModelInputData(dataPath, rank, reader => reader.GetValue("着順").GetDouble());

            foreach (var second in AppSetting.Instance.TrainingTimeSecond)
            {
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
                var now = new RegressionResult(savepath, rank, 1, second, trainedModelMetrics);
                var old = AppSetting.Instance.RegressionResults.FirstOrDefault(x => x.Index == 1);
                var bst = old == null || old.RSquared < now.RSquared ? now : old;

                AddLog($"=============== Begin of Regression evaluation {rank} {second} ===============");
                AddLog($"RSquared: {trainedModelMetrics.RSquared}");
                AddLog($"MeanSquaredError: {trainedModelMetrics.MeanSquaredError}");
                AddLog($"RootMeanSquaredError: {trainedModelMetrics.RootMeanSquaredError}");
                AddLog($"LossFunction: {trainedModelMetrics.LossFunction}");
                AddLog($"MeanAbsoluteError: {trainedModelMetrics.MeanAbsoluteError}");
                AddLog($"=============== End of Regression evaluation {rank} {second} ===============");

                mlContext.Model.Save(model, data.Schema, savepath);

                AppSetting.Instance.UpdateRegressionResults(now);

                Progress.Value += 1;
            }

            AppUtil.DeleteEndress(dataPath);
        }

        private async Task CreateModelInputData(string path, string rank, Func<DbDataReader, object> func_target)
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

                var first = GetReaderRows(reader, func_target).ToArray();

                await file.WriteLineAsync(Enumerable.Range(0, first.Length - 1).Select(i => $"COL{i.ToString(4)}").GetString(",") + "," + Label);
                await file.WriteLineAsync(first.GetString(","));

                while (next)
                {
                    await file.WriteLineAsync(GetReaderRows(reader, func_target).GetString(","));
                    next = await reader.ReadAsync();
                }
            }

            AddLog($"After Create: {path}");
        }

        private IEnumerable<object> GetReaderRows(DbDataReader reader, Func<DbDataReader, object> func_target) => Arr(
            reader.GetValue("ﾚｰｽID"),
            reader.GetValue("開催日数"),
            reader.GetValue("枠番"),
            reader.GetValue("馬番")
        ).Concat(
            GetFeatures((byte[])reader.GetValue("Features"))
        ).Concat(Arr(func_target(reader)));

        private IEnumerable<object> GetFeatures(byte[] bytes) => Enumerable.Range(0, bytes.Length / 4).Select(i => (object)BitConverter.ToSingle(bytes, i * 4));
    }
}