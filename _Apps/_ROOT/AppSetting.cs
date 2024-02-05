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

		public AppSetting() : base(@"lib\app-setting.json")
		{
			if (!Load())
			{
				TrainingTimeSecond = new[] { 1800, 2000, 2200 };
			}
		}

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
			BinaryClassificationResults = BinaryClassificationResults.Where(x => x.Index != newResult.Index).Concat(new[] { newResult }).ToArray();
			Save();
		}

		public BinaryClassificationResult GetBinaryClassificationResult(int index)
		{
			return BinaryClassificationResults.First(x => x.Index == index);
		}

		public RegressionResult[] RegressionResults
		{
			get => GetProperty(_RegressionResults);
			set => SetProperty(ref _RegressionResults, value);
		}
		public RegressionResult[] _RegressionResults = new RegressionResult[] { };

		public void UpdateRegressionResults(RegressionResult newResult)
		{
			RegressionResults = RegressionResults.Where(x => x.Index != newResult.Index).Concat(new[] { newResult }).ToArray();
			Save();
		}

		public RegressionResult GetRegressionResult(int index)
		{
			return RegressionResults.First(x => x.Index == index);
		}
	}
}