using Microsoft.ML.AutoML;
using NumSharp.Utilities;
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
		private bool _UseFastForest = false;

		public bool UseFastTree
		{
			get => GetProperty(_UseFastTree);
			set => SetProperty(ref _UseFastTree, value);
		}
		private bool _UseFastTree = false;

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
		private bool _UseLbfgsPoissonRegression = false;

		public bool UseSdca
		{
			get => GetProperty(_UseSdca);
			set => SetProperty(ref _UseSdca, value);
		}
		private bool _UseSdca = false;

		public bool UseSdcaLogisticRegression
		{
			get => GetProperty(_UseSdcaLogisticRegression);
			set => SetProperty(ref _UseSdcaLogisticRegression, value);
		}
		private bool _UseSdcaLogisticRegression = false;

		public bool UseLbfgsLogisticRegression
		{
			get => GetProperty(_UseLbfgsLogisticRegression);
			set => SetProperty(ref _UseLbfgsLogisticRegression, value);
		}
		private bool _UseLbfgsLogisticRegression = false;

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

		public uint MinimumTrainingTimeSecond
		{
			get => GetProperty(_MinimumTrainingTimeSecond);
			set => SetProperty(ref _MinimumTrainingTimeSecond, value);
		}
		private uint _MinimumTrainingTimeSecond = 600;

		public uint MaximumTrainingTimeSecond
		{
			get => GetProperty(_MaximumTrainingTimeSecond);
			set => SetProperty(ref _MaximumTrainingTimeSecond, value);
		}
		private uint _MaximumTrainingTimeSecond = 3600;

		public uint TrainingCount
		{
			get => GetProperty(_TrainingCount);
			set => SetProperty(ref _TrainingCount, value);
		}
		private uint _TrainingCount = 3;

		public string[]? Features
		{
			get => GetProperty(_Features);
			set => SetProperty(ref _Features, value);
		}
		private string[]? _Features;

		public BinaryClassificationResult[] BinaryClassificationResults
		{
			get => GetProperty(_BinaryClassificationResults);
			set => SetProperty(ref _BinaryClassificationResults, value);
		}
		public BinaryClassificationResult[] _BinaryClassificationResults = new BinaryClassificationResult[] { };

		public void UpdateBinaryClassificationResults(BinaryClassificationResult now, BinaryClassificationResult? old)
		{
			BinaryClassificationResults = Arr(now).Concat(BinaryClassificationResults.Where(x => !x.Equals(old))).ToArray();
			Save();
		}

		public BinaryClassificationResult GetBinaryClassificationResult(int index, string rank)
		{
			return BinaryClassificationResults.Where(x => x.Index == index && x.Rank == rank).Run(arr =>
			{
				return arr.FirstOrDefault(x => x.GetScore() == arr.Max(y => y.GetScore())) ?? BinaryClassificationResult.Default;
			});
		}

		public MultiClassificationResult[] MultiClassificationResults
		{
			get => GetProperty(_MultiClassificationResults);
			set => SetProperty(ref _MultiClassificationResults, value);
		}
		public MultiClassificationResult[] _MultiClassificationResults = new MultiClassificationResult[] { };

		public void UpdateMultiClassificationResults(MultiClassificationResult now, MultiClassificationResult? old)
		{
			MultiClassificationResults = Arr(now).Concat(MultiClassificationResults.Where(x => !x.Equals(old))).ToArray();
			Save();
		}

		public MultiClassificationResult GetMultiClassificationResult(int index, string rank)
		{
			return MultiClassificationResults.Where(x => x.Index == index && x.Rank == rank).Run(arr =>
			{
				return arr.FirstOrDefault(x => x.GetScore() == arr.Max(y => y.GetScore())) ?? MultiClassificationResult.Default;
			});
		}

		public RegressionResult[] RegressionResults
		{
			get => GetProperty(_RegressionResults);
			set => SetProperty(ref _RegressionResults, value);
		}
		public RegressionResult[] _RegressionResults = new RegressionResult[] { };

		public void UpdateRegressionResults(RegressionResult now, RegressionResult? old)
		{
			RegressionResults = Arr(now).Concat(RegressionResults.Where(x => !x.Equals(old))).ToArray();
			Save();
		}

		public RegressionResult GetRegressionResult(int index, string rank)
		{
			return RegressionResults.Where(x => x.Index == index && x.Rank == rank).Run(arr =>
			{
				return arr.FirstOrDefault(x => x.GetScore() == arr.Max(y => y.GetScore())) ?? RegressionResult.Default;
			});
		}
	}
}