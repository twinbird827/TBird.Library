using MathNet.Numerics.Statistics;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Windows;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Wpf.Collections;
using TBird.Wpf.Controls;

namespace Netkeiba
{
    public class ModelViewModel : DialogViewModel
    {
        public ModelViewModel()
        {
            // BinaryClassificationﾘｽﾄ及び初期選択
            BinaryClassificationMetrics = new ComboboxViewModel(
                EnumUtil.GetValues<BinaryClassificationMetric>().Select(x => new ComboboxItemModel(x.ToString(), x.ToString()))
            );
            BinaryClassificationMetrics.SelectedItem = BinaryClassificationMetrics.GetItem(AppSetting.Instance.BinaryClassificationMetric.ToString());

            // Regressionﾘｽﾄ及び初期選択
            RegressionMetrics = new ComboboxViewModel(
                EnumUtil.GetValues<RegressionMetric>().Select(x => new ComboboxItemModel(x.ToString(), x.ToString()))
            );
            RegressionMetrics.SelectedItem = RegressionMetrics.GetItem(AppSetting.Instance.RegressionMetric.ToString());

            // BinaryClassificationResultsﾘｽﾄ
            BinaryClassificationResults = new BindableCollection<BinaryClassificationViewModel>(AppSetting.Instance.BinaryClassificationResults
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Index)
                .ThenBy(x => x.GetScore())
                .ToArray()
                .Select(x => new BinaryClassificationViewModel(this, x))
            );
            BinaryClassificationResultViews = BinaryClassificationResults
                .ToBindableContextCollection();

            RegressionResults = new BindableCollection<RegressionViewModel>(AppSetting.Instance.RegressionResults
                .OrderBy(x => x.Rank)
                .ThenBy(x => x.Index)
                .ThenBy(x => x.GetScore())
                .ToArray()
                .Select(x => new RegressionViewModel(this, x))
            );
            RegressionResultViews = RegressionResults
                .ToBindableContextCollection();

            ClickMerge = RelayCommand.Create(async _ =>
            {
                if (!await DirectoryUtil.Exists(MergePath)) return;

                // ﾓﾃﾞﾙのｺﾋﾟｰ
                DirectoryUtil.Copy("model", Path.Combine(MergePath, "model"));
                // 設定情報のｺﾋﾟｰ
                using (var setting = new AppSetting(Path.Combine(MergePath, @"lib\app-setting.json")))
                {
                    setting.BinaryClassificationResults.ForEach(tgt =>
                    {
                        var old = BinaryClassificationResults.FirstOrDefault(x => x.Index == tgt.Index && x.Rank == tgt.Rank);
                        if (old == null || old.GetScore() < tgt.GetScore())
                        {
                            if (old != null) old.ClickDelete.Execute(null);
                            BinaryClassificationResults.Add(new BinaryClassificationViewModel(this, tgt));
                        }
                    });
                    setting.RegressionResults.ForEach(tgt =>
                    {
                        var old = RegressionResults.FirstOrDefault(x => x.Index == tgt.Index && x.Rank == tgt.Rank);
                        if (old == null || old.GetScore() < tgt.GetScore())
                        {
                            if (old != null) old.ClickDelete.Execute(null);
                            RegressionResults.Add(new RegressionViewModel(this, tgt));
                        }
                    });
                }
            });

            ClickSave = RelayCommand.Create(_ =>
            {
                AppSetting.Instance.BinaryClassificationMetric = EnumUtil.ToEnum<BinaryClassificationMetric>(BinaryClassificationMetrics.SelectedItem.Value);
                AppSetting.Instance.RegressionMetric = EnumUtil.ToEnum<RegressionMetric>(RegressionMetrics.SelectedItem.Value);

                if (TrainingTimeSecond.Split(',').All(i => int.TryParse(i, out int tmp) && 0 < tmp))
                {
                    AppSetting.Instance.TrainingTimeSecond = TrainingTimeSecond.Split(',').Select(i => i.GetInt32()).ToArray();
                }

                AppSetting.Instance.MinimumTrainingTimeSecond = MinimumTrainingTimeSecond;
                AppSetting.Instance.MaximumTrainingTimeSecond = MaximumTrainingTimeSecond;
                AppSetting.Instance.TrainingCount = TrainingCount;
                AppSetting.Instance.UseFastForest = UseFastForest;
                AppSetting.Instance.UseFastTree = UseFastTree;
                AppSetting.Instance.UseLgbm = UseLgbm;
                AppSetting.Instance.UseLbfgsPoissonRegression = UseLbfgsPoissonRegression;
                AppSetting.Instance.UseSdca = UseSdca;
                AppSetting.Instance.UseSdcaLogisticRegression = UseSdcaLogisticRegression;
                AppSetting.Instance.UseLbfgsLogisticRegression = UseLbfgsLogisticRegression;
                AppSetting.Instance.BinaryClassificationResults = BinaryClassificationResults.Select(x => x.Source).ToArray();
                AppSetting.Instance.RegressionResults = RegressionResults.Select(x => x.Source).ToArray();
                AppSetting.Instance.Correls = _correls;
                AppSetting.Instance.Correl = Correl;
                AppSetting.Instance.DicCor = _diccor;
                AppSetting.Instance.NetkeibaId = NetkeibaId;
                AppSetting.Instance.NetkeibaPassword = NetkeibaPassword;
                AppSetting.Instance.OrderBys = OrderBys;
                AppSetting.Instance.NetkeibaResult = NetkeibaResult;
                AppSetting.Instance.Save();

                var files = AppSetting.Instance.BinaryClassificationResults.Select(x => x.Path)
                    .Concat(AppSetting.Instance.RegressionResults.Select(x => x.Path))
                    .Select(x => new FileInfo(x))
                    .ToArray();

                foreach (var remove in DirectoryUtil.GetFiles("model").Select(x => new FileInfo(x)))
                {
                    if (!files.Any(x => x.FullName == remove.FullName))
                    {
                        // 対象ではないﾌｧｲﾙは削除する。
                        remove.Delete();
                    }
                }
            });

            ClickDeleteAll = RelayCommand.Create(_ =>
            {
                while (BinaryClassificationResults.Any())
                {
                    BinaryClassificationResults.First().ClickDelete.Execute(null);
                }
                while (RegressionResults.Any())
                {
                    RegressionResults.First().ClickDelete.Execute(null);
                }
            });

            ClickCorrelation = RelayCommand.Create(async _ =>
            {
                _diccor.Clear();

                var messages = new StringBuilder();

                foreach (var rank in AppUtil.ﾗﾝｸ2Arr)
                {
                    var tgt = new List<double>();
                    var features = new List<double>[3000];
                    for (var i = 0; i < features.Length; i++) features[i] = new List<double>();

                    using (var conn = AppUtil.CreateSQLiteControl())
                    {
                        using var reader = await conn.ExecuteReaderAsync($"SELECT 着順, Features FROM t_model WHERE ﾗﾝｸ2 = ? ORDER BY ﾚｰｽID, 馬番", SQLiteUtil.CreateParameter(DbType.Int64, AppUtil.Getﾗﾝｸ2(rank)));

                        while (await reader.ReadAsync())
                        {
                            tgt.Add(reader.Get<double>(0));

                            reader.GetValue(1).Run(x => (byte[])x).Run(x => AppUtil.ToSingles(x)).ForEach((x, i) =>
                            {
                                features[i].Add(x);
                            });
                        }
                    }

                    _diccor.Add(new ColumnFilter()
                    {
                        Key = rank,
                        Value = features
                            .Where(lst => tgt.Count == lst.Count)
                            .Select((lst, i) => (Math.Abs(Correlation.Pearson(tgt, lst)) < Correl.GetDouble() || lst.Distinct().Count() == 1, i))
                            .Where(x => x.Item1)
                            .Select(x => x.i)
                            .GetString(",")

                        //Value = features
                        //    .Where(lst => tgt.Count == lst.Count)
                        //    .Select((lst, i) => (i, Correlation.Pearson(tgt, lst), lst.Distinct().Count()))
                        //    .Where(x => Math.Abs(x.Item2) < Correl.GetDouble() || x.Item3 == 1)
                        //    .Select(x => $"C{x.i.ToString(4)}")
                        //    .ToArray()
                    });

                    messages.AppendLine($"{rank}:{tgt.Count}件のデータに対して相関係数を計算しました。{features.Count(lst => tgt.Count == lst.Count)}個中{_diccor[_diccor.Count - 1].Value.Split(',').Length}個の要素を除外します。");
                    //_correls = features.Where(lst => tgt.Count == lst.Count).Select((lst, i) => (i, Correlation.Pearson(tgt, lst))).Where(x => Math.Abs(x.Item2) < Correl.GetDouble()).Select(x => $"C{x.i.ToString(4)}").ToArray();
                }

                MessageService.Info(messages.ToString());

                //var tgt = new List<double>();
                //var features = new List<double>[3000];
                //for (var i = 0; i < features.Length; i++) features[i] = new List<double>();

                //using (var conn = AppUtil.CreateSQLiteControl())
                //{
                //	using var reader = await conn.ExecuteReaderAsync("SELECT 着順, Features FROM t_model ORDER BY ﾚｰｽID, 馬番");

                //	while (await reader.ReadAsync())
                //	{
                //		tgt.Add(reader.Get<double>(0));

                //		reader.GetValue(1).Run(x => (byte[])x).Run(x => AppUtil.ToSingles(x)).ForEach((x, i) =>
                //		{
                //			features[i].Add(x);
                //		});
                //	}
                //}

                //_correls = features.Where(lst => tgt.Count == lst.Count).Select((lst, i) => (i, Correlation.Pearson(tgt, lst))).Where(x => Math.Abs(x.Item2) < Correl.GetDouble()).Select(x => $"C{x.i.ToString(4)}").ToArray();

                //MessageService.Info($"{tgt.Count}件のデータに対して相関係数を計算しました。{features.Count(lst => tgt.Count == lst.Count)}個中{_correls.Length}個の要素を除外します。");
            });

            ClickOutput = RelayCommand.Create(_ =>
            {
                var sb = new StringBuilder();

                var binaryheaders = Arr("Index", "Second", "Rank", "Score", "Rate", "Path", "Accuracy", "AreaUnderPrecisionRecallCurve", "AreaUnderRocCurve", "Entropy", "F1Score", "LogLoss", "LogLossReduction", "NegativePrecision", "NegativeRecall", "PositivePrecision", "PositiveRecall")
                    .GetString("\t");
                sb.AppendLine(binaryheaders);

                var binaryrows = BinaryClassificationResults
                    .Select(x => new object[] { x.Index, x.Second, x.Rank, x.Score, x.Rate, x.Path, x.Accuracy, x.AreaUnderPrecisionRecallCurve, x.AreaUnderRocCurve, x.Entropy, x.F1Score, x.LogLoss, x.LogLossReduction, x.NegativePrecision, x.NegativeRecall, x.PositivePrecision, x.PositiveRecall })
                    .Select(objects => objects.GetString("\t"));
                binaryrows.ForEach(x => sb.AppendLine(x));

                var regressionheaders = Arr("Index", "Second", "Rank", "Score", "Rate", "Path", "RSquared", "MeanSquaredError", "RootMeanSquaredError", "LossFunction", "MeanAbsoluteError")
                    .GetString("\t");
                sb.AppendLine(regressionheaders);

                var regressionrows = RegressionResults
                    .Select(x => new object[] { x.Index, x.Second, x.Rank, x.Score, x.Rate, x.Path, x.RSquared, x.MeanSquaredError, x.RootMeanSquaredError, x.LossFunction, x.MeanAbsoluteError })
                    .Select(objects => objects.GetString("\t"));
                regressionrows.ForEach(x => sb.AppendLine(x));

                Clipboard.SetText(sb.ToString());
            });
        }

        public uint MinimumTrainingTimeSecond
        {
            get => _MinimumTrainingTimeSecond;
            set => SetProperty(ref _MinimumTrainingTimeSecond, value);
        }
        private uint _MinimumTrainingTimeSecond = AppSetting.Instance.MinimumTrainingTimeSecond;

        public uint MaximumTrainingTimeSecond
        {
            get => _MaximumTrainingTimeSecond;
            set => SetProperty(ref _MaximumTrainingTimeSecond, value);
        }
        private uint _MaximumTrainingTimeSecond = AppSetting.Instance.MaximumTrainingTimeSecond;

        public uint TrainingCount
        {
            get => _TrainingCount;
            set => SetProperty(ref _TrainingCount, value);
        }
        private uint _TrainingCount = AppSetting.Instance.TrainingCount;

        public bool UseFastForest
        {
            get => _UseFastForest;
            set => SetProperty(ref _UseFastForest, value);
        }
        private bool _UseFastForest = AppSetting.Instance.UseFastForest;

        public bool UseFastTree
        {
            get => _UseFastTree;
            set => SetProperty(ref _UseFastTree, value);
        }
        private bool _UseFastTree = AppSetting.Instance.UseFastTree;

        public bool UseLgbm
        {
            get => _UseLgbm;
            set => SetProperty(ref _UseLgbm, value);
        }
        private bool _UseLgbm = AppSetting.Instance.UseLgbm;

        public bool UseLbfgsPoissonRegression
        {
            get => _UseLbfgsPoissonRegression;
            set => SetProperty(ref _UseLbfgsPoissonRegression, value);
        }
        private bool _UseLbfgsPoissonRegression = AppSetting.Instance.UseLbfgsPoissonRegression;

        public bool UseSdca
        {
            get => _UseSdca;
            set => SetProperty(ref _UseSdca, value);
        }
        private bool _UseSdca = AppSetting.Instance.UseSdca;

        public bool UseSdcaLogisticRegression
        {
            get => _UseSdcaLogisticRegression;
            set => SetProperty(ref _UseSdcaLogisticRegression, value);
        }
        private bool _UseSdcaLogisticRegression = AppSetting.Instance.UseSdcaLogisticRegression;

        public bool UseLbfgsLogisticRegression
        {
            get => _UseLbfgsLogisticRegression;
            set => SetProperty(ref _UseLbfgsLogisticRegression, value);
        }
        private bool _UseLbfgsLogisticRegression = AppSetting.Instance.UseLbfgsLogisticRegression;

        public ComboboxViewModel BinaryClassificationMetrics { get; }

        public ComboboxViewModel RegressionMetrics { get; }

        public string TrainingTimeSecond
        {
            get => _TrainingTimeSecond;
            set => SetProperty(ref _TrainingTimeSecond, value);
        }
        private string _TrainingTimeSecond = AppSetting.Instance.TrainingTimeSecond.GetString(",");

        public string NetkeibaId
        {
            get => _NetkeibaId;
            set => SetProperty(ref _NetkeibaId, value);
        }
        private string _NetkeibaId = AppSetting.Instance.NetkeibaId;

        public string NetkeibaPassword
        {
            get => _NetkeibaPassword;
            set => SetProperty(ref _NetkeibaPassword, value);
        }
        private string _NetkeibaPassword = AppSetting.Instance.NetkeibaPassword;

        public string NetkeibaResult
        {
            get => _NetkeibaResult;
            set => SetProperty(ref _NetkeibaResult, value);
        }
        private string _NetkeibaResult = AppSetting.Instance.NetkeibaResult;

        public string OrderBys
        {
            get => _OrderBys;
            set => SetProperty(ref _OrderBys, value);
        }
        private string _OrderBys = AppSetting.Instance.OrderBys;

        public BindableCollection<BinaryClassificationViewModel> BinaryClassificationResults { get; }
        public BindableContextCollection<BinaryClassificationViewModel> BinaryClassificationResultViews { get; }
        public BindableCollection<RegressionViewModel> RegressionResults { get; }
        public BindableContextCollection<RegressionViewModel> RegressionResultViews { get; }

        public string MergePath
        {
            get => _MergePath;
            set => SetProperty(ref _MergePath, value);
        }
        private string _MergePath = string.Empty;

        public IRelayCommand ClickMerge { get; }

        public IRelayCommand ClickSave { get; }

        public string Correl
        {
            get => _Correl;
            set => SetProperty(ref _Correl, value);
        }
        private string _Correl = AppSetting.Instance.Correl;

        private string[] _correls = Enumerable.Empty<string>().ToArray();

        private List<ColumnFilter> _diccor = AppSetting.Instance.DicCor;

        public IRelayCommand ClickCorrelation { get; }

        public IRelayCommand ClickOutput { get; }

        public IRelayCommand ClickDeleteAll { get; }
    }

    public class BinaryClassificationViewModel : BinaryClassificationResult
    {
        public ModelViewModel Parent { get; }

        public BinaryClassificationResult Source { get; }

        public BinaryClassificationViewModel(ModelViewModel m, BinaryClassificationResult source)
        {
            Parent = m;
            Source = source;

            Index = source.Index;
            Rank = source.Rank;
            Score = source.Score;
            Rate = source.Rate;
            Second = source.Second;
            Path = source.Path;
            Accuracy = source.Accuracy;
            AreaUnderPrecisionRecallCurve = source.AreaUnderPrecisionRecallCurve;
            AreaUnderRocCurve = source.AreaUnderRocCurve;
            Entropy = source.Entropy;
            F1Score = source.F1Score;
            LogLoss = source.LogLoss;
            LogLossReduction = source.LogLossReduction;
            NegativePrecision = source.NegativePrecision;
            NegativeRecall = source.NegativeRecall;
            PositivePrecision = source.PositivePrecision;
            PositiveRecall = source.PositiveRecall;

            ClickDelete = RelayCommand.Create(_ =>
            {
                FileUtil.Delete(Path);
                Parent.BinaryClassificationResults.Remove(this);
            });
        }

        public IRelayCommand ClickDelete { get; }
    }

    public class RegressionViewModel : RegressionResult
    {
        public ModelViewModel Parent { get; }

        public RegressionResult Source { get; }

        public RegressionViewModel(ModelViewModel m, RegressionResult source)
        {
            Parent = m;
            Source = source;

            Index = source.Index;
            Rank = source.Rank;
            Rate = source.Rate;
            Score = source.Score;
            Second = source.Second;
            Path = source.Path;
            RSquared = source.RSquared;
            MeanSquaredError = source.MeanSquaredError;
            RootMeanSquaredError = source.RootMeanSquaredError;
            LossFunction = source.LossFunction;
            MeanAbsoluteError = source.MeanAbsoluteError;

            ClickDelete = RelayCommand.Create(_ =>
            {
                FileUtil.Delete(Path);
                Parent.RegressionResults.Remove(this);
            });
        }

        public IRelayCommand ClickDelete { get; }
    }

}