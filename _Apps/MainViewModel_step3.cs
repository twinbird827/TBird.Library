using AngleSharp.Common;
using ControlzEx.Standard;
using Microsoft.ML;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
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

        private long tgtdate;

        public IRelayCommand S3EXECCHECK => RelayCommand.Create(_ =>
        {
            var check = CreateModelSources.Any(x => x.Value.IsChecked);
            CreateModelSources.ForEach(x => x.Value.IsChecked = !check);
        });

        public IRelayCommand S3EXECPREDICT => RelayCommand.Create(async _ =>
        {
            // ｽｺｱ数=7
            IEnumerable<int> GetAwase(int start) => Enumerable.Range(start, 7 - (start - 1));

            var pays = Arr(
                // 指定したｽｺｱがrank以内であるか
                Enumerable.Range(1, 3).SelectMany(rank => GetAwase(1).Select(i => Payment.Create順(i, rank))),
                // 指定したｽｺｱの単勝
                GetAwase(1).Select(i => Payment.Create倍A(i)),
                // 指定したｽｺｱ内で最も倍率が高い単勝
                GetAwase(1).Select(i => Payment.Create倍B(i)),
                // 1, 2固定で3=指定したｽｺｱの昇順で1-4通り
                Enumerable.Range(0, 4).SelectMany(take => GetAwase(3 + take).Select(i => Payment.Create複A(i, take + 1))),
                // 1, 2固定で3=倍率が最も高い1-4通り
                Enumerable.Range(0, 4).SelectMany(take => GetAwase(3 + take).Select(i => Payment.Create複B(i, take + 1))),
                // 1固定で2, 3=倍率が高い1-4通り
                Enumerable.Range(0, 4).SelectMany(take => GetAwase(3 + take).Select(i => Payment.Create複C(i, take + 1))),
                // 1固定で2=倍率が高い1-4通り
                Enumerable.Range(0, 4).SelectMany(take => GetAwase(2 + take).Select(i => Payment.Create単A(i, take + 1))),
                // 2固定で1=倍率が高い1-4通り
                Enumerable.Range(0, 4).SelectMany(take => GetAwase(2 + take).Select(i => Payment.Create単B(i, take + 1))),
                // 2=1, 2で倍率が低い方, 1=倍率が高い1-4通り
                Enumerable.Range(0, 4).SelectMany(take => GetAwase(2 + take).Select(i => Payment.Create単C(i, take + 1))),
                // 1固定, 2=倍率高い順1-4通り
                Enumerable.Range(0, 4).SelectMany(take => GetAwase(2 + take).Select(i => Payment.CreateワA(i, take + 1)))
            ).SelectMany(_ => _).ToArray();

            var path = Path.Combine("result", DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-Prediction.csv");

            // Initialize MLContext
            MLContext mlContext = new MLContext();

            using (var conn = AppUtil.CreateSQLiteControl())
            {
                var maxdate = await conn.ExecuteScalarAsync<long>("SELECT MAX(開催日数) FROM t_model");
                var mindate = await conn.ExecuteScalarAsync<long>("SELECT MIN(開催日数) FROM t_model");
                tgtdate = Calc(maxdate, (maxdate - mindate) * 0.025, (x, y) => x - y).GetInt64();

                using (var file = new FileAppendWriter(path))
                {
                    // ﾍｯﾀﾞの書き込み
                    await file.WriteLineAsync(
                        Arr(
                            Arr("Rank", "Index", "Count"),
                            pays.SelectMany(x => Arr(x.head + "+S", x.head + "+R"))
                        ).SelectMany(_ => _).GetString(",")
                    );

                    Progress.Value = 0;
                    Progress.Minimum = 0;
                    Progress.Maximum = AppUtil.ﾗﾝｸ2Arr.Length;

                    foreach (var rank in AppUtil.ﾗﾝｸ2Arr)
                    {
                        var raceids = await conn.GetRows(r => r.Get<long>(0), "SELECT DISTINCT ﾚｰｽID FROM t_model WHERE 開催日数 > ? AND ﾗﾝｸ2 = ?",
                            SQLiteUtil.CreateParameter(DbType.Int64, tgtdate),
                            SQLiteUtil.CreateParameter(DbType.Int64, AppUtil.Getﾗﾝｸ2(rank))
                        );

                        // 支払情報を取得
                        await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_payout (ﾚｰｽID,key,val, PRIMARY KEY (ﾚｰｽID,key))");
                        var payoutDetails = new Dictionary<long, Dictionary<string, string>>();
                        await conn.BeginTransaction();
                        foreach (var raceid in raceids)
                        {
                            payoutDetails[raceid] = await conn.GetRows("SELECT * FROM t_payout WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.String, raceid.ToString())).RunAsync(async rows =>
                            {
                                if (rows.Any())
                                {
                                    return rows.ToDictionary(x => x["key"].Str(), x => x["val"].Str());
                                }
                                else
                                {
                                    return await GetPayout(raceid.ToString());
                                }
                            });

                            foreach (var x in payoutDetails[raceid])
                            {
                                await conn.ExecuteNonQueryAsync("REPLACE INTO t_payout (ﾚｰｽID,key,val) VALUES (?,?,?)",
                                    SQLiteUtil.CreateParameter(DbType.String, raceid.Str()),
                                    SQLiteUtil.CreateParameter(DbType.String, x.Key),
                                    SQLiteUtil.CreateParameter(DbType.String, x.Value)
                                );
                            }
                        }
                        conn.Commit();

                        var models = new Dictionary<long, List<Dictionary<string, object>>>();
                        foreach (var raceid in raceids)
                        {
                            models[raceid] = await conn.GetRows("SELECT 馬番, ﾚｰｽID, 着順, 単勝, Features FROM t_model WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.Int64, raceid));
                        }

                        async Task PredictionModelArr<TSrc, TDst>(string index, PredictionFactory<TSrc, TDst>[] facs) where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
                        {
                            var rets = new List<float[]>();

                            foreach (var raceid in raceids)
                            {
                                var racs = new List<List<object>>();

                                foreach (var m in models[raceid])
                                {
                                    racs.Add(Payment.GetPredictionBase(m, facs));
                                }

                                // ｽｺｱで順位付けをする
                                if (racs.Any())
                                {
                                    Payment.AddOrderByDescendingScoreIndex(racs);

                                    // 結果の平均を結果に詰める
                                    rets.Add(pays.Select(x => x.func(racs, payoutDetails[raceid], Payment.OrderByDescendingScoreIndex).GetSingle()).ToArray());
                                }
                            }

                            await file.WriteLineAsync(
                                Arr(
                                    Arr(rank, index),
                                    Arr(rets.Count.Str()),
                                    pays.SelectMany((pay, i) => Arr(
                                        rets.Sum(x => x[i]) / (rets.Count * pay.pay) * 1F,
                                        rets.Count(x => x[i] > 0) * 1F / rets.Count * 1F
                                    )).Select(x => x.ToString("F4"))
                                ).SelectMany(_ => _).GetString(",")
                            );
                        }
                        Task PredictionModel<TSrc, TDst>(string index, PredictionFactory<TSrc, TDst> fac) where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
                        {
                            return PredictionModelArr(index, Arr(fac));
                        }

                        const double PredictionModelLength = 6;
                        foreach (var o in AppUtil.OrderBys)
                        {
                            var binaries1 = AppSetting.Instance.GetBinaryClassificationResults($"1-{o}", rank)
                                .OrderByDescending(x => x.GetScore())
                                .Take(4);
                            var binaries6 = AppSetting.Instance.GetBinaryClassificationResults($"6-{o}", rank)
                                .OrderByDescending(x => x.GetScore())
                                .Take(4);
                            var binaries = binaries1.Concat(binaries6)
                                .Select(x => new BinaryClassificationPredictionFactory(mlContext, x.Rank, x.Index, x))
                                .ToArray();

                            await PredictionModel($"B1-{o}", binaries[0]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModel($"B2-{o}", binaries[1]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModel($"B3-{o}", binaries[2]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModel($"B4-{o}", binaries[3]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModel($"B6-{o}", binaries[4]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModel($"B7-{o}", binaries[5]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModel($"B8-{o}", binaries[6]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModel($"B9-{o}", binaries[7]);
                            Progress.Value += 1 / PredictionModelLength;
                            await PredictionModelArr($"BX-{o}", binaries);
                            Progress.Value += 1 / PredictionModelLength;
                        }
                        await PredictionModel("R1", AppSetting.Instance.GetRegressionResult("1", rank).Run(result => new RegressionPredictionFactory(mlContext, rank, "1", result)));
                        Progress.Value += 1 / PredictionModelLength;
                    }
                }
            }

            System.Diagnostics.Process.Start("EXPLORER.EXE", Path.GetFullPath("result"));
        });

        public IRelayCommand S3EXEC => RelayCommand.Create(async _ =>
        {
            var seconds = AppSetting.Instance.TrainingCount;

            DirectoryUtil.DeleteInFiles("model", x => Path.GetExtension(x.FullName) == ".csv");

            using (var conn = AppUtil.CreateSQLiteControl())
            {
                var maxdate = await conn.ExecuteScalarAsync<long>("SELECT MAX(開催日数) FROM t_model");
                var mindate = await conn.ExecuteScalarAsync<long>("SELECT MIN(開催日数) FROM t_model");
                tgtdate = Calc(maxdate, (maxdate - mindate) * 0.1, (x, y) => x - y).GetInt64();
            }

            var checkes = CreateModels
                .Select(x => x.Value)
                .Where(x => x.IsChecked)
                .ToArray();

            const int NumberOfCreateModel = 4;
            Progress.Value = 0;
            Progress.Minimum = 0;
            Progress.Maximum = seconds * AppUtil.OrderBys.Count() * checkes.Length * (NumberOfCreateModel * 2 + 1);

            AppSetting.Instance.Save();

            for (var tmp = 0; tmp < seconds * NumberOfCreateModel; tmp++)
            {
                var random = new Random();
                foreach (var o in AppUtil.OrderBys)
                {
                    var second = (uint)random.Next((int)AppSetting.Instance.MinimumTrainingTimeSecond, (int)AppSetting.Instance.MaximumTrainingTimeSecond);

                    foreach (var x in checkes)
                    {
                        var rank = x.Value;

                        (float 着順, float 単勝) GET着勝(DbDataReader r) => (r.GetValue("着順").GetSingle(), r.GetValue("単勝").GetSingle());

                        await BinaryClassification($"1-{o}", rank, second, BinaryClassificationMetric.AreaUnderRocCurve, r => GET着勝(r).Run(x => x.着順 <= o));
                        await BinaryClassification($"6-{o}", rank, second, BinaryClassificationMetric.AreaUnderRocCurve, r => GET着勝(r).Run(x => x.着順 > o));

                        if (tmp % NumberOfCreateModel == 0) await Regression(rank, second);
                    }
                }
            }
        });

        private async Task BinaryClassification(string index, string rank, uint second, BinaryClassificationMetric metric, Func<DbDataReader, object> func_yoso)
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
                x.GroupIdColumnName = Group;
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
            var trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.1, samplingKeyColumnName: Group);

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
            var savepath = $@"model\BinaryClassification_{rank}_{index}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

            var trained = mlContext.BinaryClassification.Evaluate(testDataPredictions, labelColumnName: Label);
            var now = await PredictionModel(rank, new BinaryClassificationPredictionFactory(mlContext, rank, index, model)).RunAsync(x =>
                new BinaryClassificationResult(savepath, rank, index, second, trained, x.score, x.rate)
            );

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

            AppSetting.Instance.UpdateBinaryClassificationResults(now);

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
                x.GroupIdColumnName = Group;
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
            var trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.1, samplingKeyColumnName: Group);

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
            var now = await PredictionModel(rank, new RegressionPredictionFactory(mlContext, rank, "1", model)).RunAsync(x =>
                new RegressionResult(savepath, rank, "1", second, trained, x.score, x.rate)
            );

            AddLog($"=============== Result of Regression Model Data {rank} {second} ===============");
            AddLog($"MeanSquaredError: {trained.MeanSquaredError}");
            AddLog($"RootMeanSquaredError: {trained.RootMeanSquaredError}");
            AddLog($"LossFunction: {trained.LossFunction}");
            AddLog($"MeanAbsoluteError: {trained.MeanAbsoluteError}");
            AddLog($"RSquared: {trained.RSquared}");
            AddLog($"Rate: {now.Rate:N4}     Score: {now.Score:N4}     S^2*R: {now.GetScore():N4}");
            AddLog($"=============== End of Regression evaluation {rank} {second} ===============");

            mlContext.Model.Save(model, data.Schema, savepath);

            AppSetting.Instance.UpdateRegressionResults(now);

            Progress.Value += 1;

            AppUtil.DeleteEndress(dataPath);
        }

        //private async Task Ranking(string index, string rank, uint second)
        //{
        //    // Initialize MLContext
        //    MLContext mlContext = new MLContext();

        //    // ﾓﾃﾞﾙ作成用ﾃﾞｰﾀﾌｧｲﾙ
        //    var dataPath = Path.Combine("model", DateTime.Now.ToString("yyMMddHHmmss") + ".csv");

        //    // ﾃﾞｰﾀﾌｧｲﾙを作製する
        //    await CreateModelInputData(dataPath, rank, (int 着順) => (uint)着順);

        //    AddLog($"=============== Begin Update of Ranking evaluation {rank} {index} {second} ===============");

        //    // Infer column information
        //    var columnInference = mlContext.Auto().InferColumns(dataPath, new ColumnInformation().Run(x =>
        //    {
        //        x.LabelColumnName = Label;
        //        x.SamplingKeyColumnName = Group;
        //        x.GroupIdColumnName = Group;
        //    }), groupColumns: false);
        //    columnInference.TextLoaderOptions.Run(x =>
        //    {
        //        x.Columns[0].DataKind = DataKind.UInt32;
        //        x.Columns[1].DataKind = DataKind.Int64;
        //    });
        //    // Create text loader
        //    TextLoader loader = mlContext.Data.CreateTextLoader(columnInference.TextLoaderOptions);

        //    // Load data into IDataView
        //    IDataView data = loader.Load(dataPath);

        //    // Split into train (80%), validation (20%) sets
        //    var trainValidationData = mlContext.Data.TrainTestSplit(data, testFraction: 0.1, samplingKeyColumnName: Group);

        //    //Define pipeline
        //    SweepablePipeline pipeline = mlContext
        //            .Auto()
        //            .Featurizer(data, columnInformation: columnInference.ColumnInformation)
        //            .Append(mlContext.Ranking.Trainers.LightGbm(labelColumnName: Label, rowGroupColumnName: Group));

        //    // Log experiment trials
        //    var monitor = new AutoMLMonitor(pipeline, this);

        //    // Create AutoML experiment
        //    var experiment = mlContext.Auto().CreateExperiment()
        //        .SetPipeline(pipeline)
        //        .SetTrainingTimeInSeconds(second)
        //        .SetEciCostFrugalTuner()
        //        .SetDataset(trainValidationData)
        //        .SetMonitor(monitor);

        //    try
        //    {
        //        // Run experiment
        //        var cts = new CancellationTokenSource();
        //        TrialResult experimentResults = await experiment.RunAsync(cts.Token);

        //        // Get best model
        //        var model = experimentResults.Model;

        //        // Get all completed trials
        //        var completedTrials = monitor.GetCompletedTrials();

        //        // Measure trained model performance
        //        // Apply data prep transformer to test data
        //        // Use trained model to make inferences on test data
        //        IDataView testDataPredictions = model.Transform(trainValidationData.TestSet);

        //        // Save model
        //        var savepath = $@"model\Ranking_{rank}_{1.ToString(2)}_{second}_{DateTime.Now.ToString("yyMMddHHmmss")}.zip";

        //        var trained = mlContext.Ranking.Evaluate(testDataPredictions, labelColumnName: Label);
        //        var now = await PredictionModel(rank, new RankingPredictionFactory(mlContext, rank, "1", model)).RunAsync(x =>
        //            new RankingResult(savepath, rank, "1", second, trained, x.score, x.rate)
        //        );

        //        AddLog($"=============== Result of Ranking Model Data {rank} {second} ===============");
        //        AddLog($"NormalizedDiscountedCumulativeGains: {trained.NormalizedDiscountedCumulativeGains}");
        //        AddLog($"DiscountedCumulativeGains: {trained.DiscountedCumulativeGains}");
        //        AddLog($"Rate: {now.Rate:N4}     Score: {now.Score:N4}     S^2*R: {now.GetScore():N4}");
        //        AddLog($"=============== End of Ranking evaluation {rank} {second} ===============");

        //        mlContext.Model.Save(model, data.Schema, savepath);

        //        AppSetting.Instance.UpdateRankingResults(now);

        //        Progress.Value += 1;

        //        AppUtil.DeleteEndress(dataPath);
        //    }
        //    catch (Exception ex)
        //    {
        //        throw;
        //    }
        //}

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
                using var reader = await conn.ExecuteReaderAsync($"SELECT * FROM t_model WHERE 開催日数 <= ? AND ﾗﾝｸ2 = ? ORDER BY ﾚｰｽID, 馬番",
                    SQLiteUtil.CreateParameter(DbType.Int64, tgtdate),
                    SQLiteUtil.CreateParameter(DbType.Int64, AppUtil.Getﾗﾝｸ2(rank))
                );

                var next = await reader.ReadAsync();

                if (!next) return;

                var first = AppUtil.ToSingles((byte[])reader.GetValue("Features"), rank);
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
                    await file.WriteLineAsync(Arr(func_target(reader), reader.GetValue("ﾚｰｽID").GetInt64())
                        .Concat(AppUtil.ToSingles((byte[])reader.GetValue("Features"), rank).Select(x => (object)x))
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
                var pays = new[]
                {
                    Payment.Create複2(),
                    Payment.Createワ1(),
                    Payment.Create勝(1),
                    Payment.Create連1(),
                };

                var rets = new List<float>();

                foreach (var raceid in await conn.GetRows(r => r.Get<long>(0), $"SELECT DISTINCT ﾚｰｽID FROM t_model WHERE 開催日数 > ? AND ﾗﾝｸ2 = ?",
                        SQLiteUtil.CreateParameter(DbType.Int64, tgtdate),
                        SQLiteUtil.CreateParameter(DbType.Int64, AppUtil.Getﾗﾝｸ2(rank))
                    ))
                {
                    var racs = new List<List<object>>();

                    foreach (var m in await conn.GetRows("SELECT 馬番, ﾚｰｽID, 着順, 単勝, Features FROM t_model WHERE ﾚｰｽID = ?", SQLiteUtil.CreateParameter(DbType.Int64, raceid)))
                    {
                        // ｽｺｱ算出
                        racs.Add(Payment.GetPredictionBase(m, Arr(factory)));
                    }

                    // ｽｺｱで順位付けをする
                    if (racs.Any())
                    {
                        Payment.AddOrderByDescendingScoreIndex(racs);

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
                        rets.Add(pays.Select(x => x.func(racs, payoutDetail, Payment.OrderByDescendingScoreIndex).GetSingle()).Sum());

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
    }
}