using Microsoft.ML.AutoML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public class AppSetting : JsonBase<AppSetting>
	{
		public static AppSetting Instance { get; } = new AppSetting();

		public AppSetting(string path) : base(path)
		{
			if (!Load())
			{
				TrainingTimeSecond = new[] { 1800, 2000, 2200 };
				BinaryClassificationResults = new BinaryClassificationResult[] { };
				RegressionResults = new RegressionResult[] { };
			}
		}

		public AppSetting() : this(@"lib\app-setting.json")
		{

		}

		public bool UseFastForest
		{
			get => GetProperty(_UseFastForest);
			set => SetProperty(ref _UseFastForest, value);
		}
		private bool _UseFastForest = true;

		public bool UseFastTree
		{
			get => GetProperty(_UseFastTree);
			set => SetProperty(ref _UseFastTree, value);
		}
		private bool _UseFastTree = true;

		public bool UseLgbm
		{
			get => GetProperty(_UseLgbm);
			set => SetProperty(ref _UseLgbm, value);
		}
		private bool _UseLgbm = true;

		public bool UseLbfgsPoissonRegression
		{
			get => GetProperty(_UseLbfgsPoissonRegression);
			set => SetProperty(ref _UseLbfgsPoissonRegression, value);
		}
		private bool _UseLbfgsPoissonRegression = true;

		public bool UseSdca
		{
			get => GetProperty(_UseSdca);
			set => SetProperty(ref _UseSdca, value);
		}
		private bool _UseSdca = true;

		public bool UseSdcaLogisticRegression
		{
			get => GetProperty(_UseSdcaLogisticRegression);
			set => SetProperty(ref _UseSdcaLogisticRegression, value);
		}
		private bool _UseSdcaLogisticRegression = true;

		public bool UseLbfgsLogisticRegression
		{
			get => GetProperty(_UseLbfgsLogisticRegression);
			set => SetProperty(ref _UseLbfgsLogisticRegression, value);
		}
		private bool _UseLbfgsLogisticRegression = true;

		public BinaryClassificationMetric BinaryClassificationMetric
		{
			get => GetProperty(_BinaryClassificationMetric);
			set => SetProperty(ref _BinaryClassificationMetric, value);
		}
		private BinaryClassificationMetric _BinaryClassificationMetric = BinaryClassificationMetric.AreaUnderRocCurve;

		public RegressionMetric RegressionMetric
		{
			get => GetProperty(_RegressionMetric);
			set => SetProperty(ref _RegressionMetric, value);
		}
		private RegressionMetric _RegressionMetric = RegressionMetric.RSquared;

		public int[] TrainingTimeSecond
		{
			get => GetProperty(_TrainingTimeSecond);
			set => SetProperty(ref _TrainingTimeSecond, value);
		}
		private int[] _TrainingTimeSecond = new int[] { };

		public BinaryClassificationResult[] BinaryClassificationResults
		{
			get => GetProperty(_BinaryClassificationResults);
			set => SetProperty(ref _BinaryClassificationResults, value);
		}
		public BinaryClassificationResult[] _BinaryClassificationResults = new BinaryClassificationResult[] { };

		public void UpdateBinaryClassificationResults(BinaryClassificationResult newResult)
		{
			BinaryClassificationResults = Arr(newResult).Concat(BinaryClassificationResults).ToArray();
			Save();
		}

		public BinaryClassificationResult GetBinaryClassificationResult(int index)
		{
			return BinaryClassificationResults.Where(x => x.Index == index).OrderByDescending(x =>
			{
				switch (BinaryClassificationMetric)
				{
					case BinaryClassificationMetric.Accuracy:
						return x.Accuracy;
					case BinaryClassificationMetric.AreaUnderRocCurve:
						return x.AreaUnderRocCurve;
					case BinaryClassificationMetric.F1Score:
						return x.F1Score;
					case BinaryClassificationMetric.AreaUnderPrecisionRecallCurve:
						return x.AreaUnderPrecisionRecallCurve;
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
			}).First();
		}

		public RegressionResult[] RegressionResults
		{
			get => GetProperty(_RegressionResults);
			set => SetProperty(ref _RegressionResults, value);
		}
		public RegressionResult[] _RegressionResults = new RegressionResult[] { };

		public void UpdateRegressionResults(RegressionResult newResult)
		{
			RegressionResults = Arr(newResult).Concat(RegressionResults).ToArray();
			Save();
		}

		public RegressionResult GetRegressionResult(int index)
		{
			return RegressionResults.Where(x => x.Index == index).OrderByDescending(x =>
			{
				switch (RegressionMetric)
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
			}).First();
		}
	}
}