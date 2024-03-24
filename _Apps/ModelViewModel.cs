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