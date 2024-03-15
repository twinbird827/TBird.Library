using MathNet.Numerics.RootFinding;
using Microsoft.ML.AutoML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
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
				.ThenBy(x => GetBinaryClassificationSortedValue(x))
				.ToArray()
				.Select(x => new BinaryClassificationViewModel(this, x))
			);
			BinaryClassificationResultViews = BinaryClassificationResults
				.ToBindableContextCollection();

			RegressionResults = new BindableCollection<RegressionViewModel>(AppSetting.Instance.RegressionResults
				.OrderBy(x => x.Rank)
				.ThenBy(x => x.Index)
				.ThenBy(x => GetRegressionResultSortedValue(x))
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
					BinaryClassificationResults.AddRange(setting.BinaryClassificationResults.Select(x => new BinaryClassificationViewModel(this, x)));

					RegressionResults.AddRange(setting.RegressionResults.Select(x => new RegressionViewModel(this, x)));
				}
			});

			AddDisposed((sender, e) =>
			{
				AppSetting.Instance.BinaryClassificationMetric = EnumUtil.ToEnum<BinaryClassificationMetric>(BinaryClassificationMetrics.SelectedItem.Value);
				AppSetting.Instance.RegressionMetric = EnumUtil.ToEnum<RegressionMetric>(RegressionMetrics.SelectedItem.Value);

				if (TrainingTimeSecond.Split(',').All(i => int.TryParse(i, out int tmp) && 0 < tmp))
				{
					AppSetting.Instance.TrainingTimeSecond = TrainingTimeSecond.Split(',').Select(i => i.GetInt32()).ToArray();
				}

				AppSetting.Instance.UseFastForest = UseFastForest;
				AppSetting.Instance.UseFastTree = UseFastTree;
				AppSetting.Instance.UseLgbm = UseLgbm;
				AppSetting.Instance.UseLbfgsPoissonRegression = UseLbfgsPoissonRegression;
				AppSetting.Instance.UseSdca = UseSdca;
				AppSetting.Instance.UseSdcaLogisticRegression = UseSdcaLogisticRegression;
				AppSetting.Instance.UseLbfgsLogisticRegression = UseLbfgsLogisticRegression;
				AppSetting.Instance.BinaryClassificationResults = BinaryClassificationResults.ToArray();
				AppSetting.Instance.RegressionResults = RegressionResults.ToArray();
				AppSetting.Instance.Save();
			});
		}

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

		public BindableCollection<BinaryClassificationViewModel> BinaryClassificationResults { get; }
		public BindableContextCollection<BinaryClassificationViewModel> BinaryClassificationResultViews { get; }
		public BindableCollection<RegressionViewModel> RegressionResults { get; }
		public BindableContextCollection<RegressionViewModel> RegressionResultViews { get; }

		private double GetBinaryClassificationSortedValue(BinaryClassificationResult x)
		{
			switch (EnumUtil.ToEnum<BinaryClassificationMetric>(BinaryClassificationMetrics.SelectedItem.Value))
			{
				case BinaryClassificationMetric.Accuracy:
					return x.Accuracy;
				case BinaryClassificationMetric.AreaUnderPrecisionRecallCurve:
					return x.AreaUnderPrecisionRecallCurve;
				case BinaryClassificationMetric.AreaUnderRocCurve:
					return x.AreaUnderRocCurve;
				case BinaryClassificationMetric.F1Score:
					return x.F1Score;
				case BinaryClassificationMetric.NegativePrecision:
					return x.NegativePrecision;
				case BinaryClassificationMetric.NegativeRecall:
					return x.NegativeRecall;
				case BinaryClassificationMetric.PositivePrecision:
					return x.PositivePrecision;
				case BinaryClassificationMetric.PositiveRecall:
					return x.PositiveRecall;
				default:
					return x.AreaUnderRocCurve;
			}
		}

		private double GetRegressionResultSortedValue(RegressionResult x)
		{
			switch (EnumUtil.ToEnum<RegressionMetric>(RegressionMetrics.SelectedItem.Value))
			{
				case RegressionMetric.MeanAbsoluteError:
					return x.MeanAbsoluteError;
				case RegressionMetric.MeanSquaredError:
					return x.MeanSquaredError;
				case RegressionMetric.RootMeanSquaredError:
					return x.RootMeanSquaredError;
				case RegressionMetric.RSquared:
					return x.RSquared;
				default:
					return x.RSquared;
			}
		}

		public string MergePath
		{
			get => _MergePath;
			set => SetProperty(ref _MergePath, value);
		}
		private string _MergePath = string.Empty;

		public IRelayCommand ClickMerge { get; }
	}

	public class BinaryClassificationViewModel : BinaryClassificationResult
	{
		public ModelViewModel Parent { get; }

		public BinaryClassificationViewModel(ModelViewModel m, BinaryClassificationResult source)
		{
			Parent = m;

			Index = source.Index;
			Rank = source.Rank;
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

		public RegressionViewModel(ModelViewModel m, RegressionResult source)
		{
			Parent = m;

			Index = source.Index;
			Rank = Rank;
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