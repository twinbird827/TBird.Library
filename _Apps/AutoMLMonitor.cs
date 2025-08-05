using Microsoft.ML.AutoML;
using System;
using System.Collections.Generic;

namespace Netkeiba
{
	public class AutoMLMonitor : IMonitor
	{
		private readonly SweepablePipeline _pipeline;
		private readonly MainViewModel _vm;

		public AutoMLMonitor(SweepablePipeline pipeline, MainViewModel vm)
		{
			_completedTrials = new List<TrialResult>();
			_pipeline = pipeline;
			_vm = vm;
		}

		private readonly List<TrialResult> _completedTrials;

		public IEnumerable<TrialResult> GetCompletedTrials() => _completedTrials;

		public void ReportBestTrial(TrialResult result)
		{
			var id = result.TrialSettings.TrialId;
			var ms = result.DurationInMilliseconds;
			var mc = result.Metric;
			var pl = _pipeline.ToString(result.TrialSettings.Parameter);
			MainViewModel.AddLog($"Best Trial={id}; DurationInMilliseconds={ms}; Loss={result.Loss}; Metric={mc}; Pipeline={pl};");
			return;
		}

		public void ReportCompletedTrial(TrialResult result)
		{
			_completedTrials.Add(result);
		}

#pragma warning disable CS8625 // null リテラルを null 非許容参照型に変換できません。

		public void ReportFailTrial(TrialSettings settings, Exception exception = null)
#pragma warning restore CS8625 // null リテラルを null 非許容参照型に変換できません。
		{
			if (exception.Message.Contains("Operation was canceled."))
			{
				MainViewModel.AddLog($"{settings.TrialId} cancelled. Time budget exceeded.");
			}
			MainViewModel.AddLog($"{settings.TrialId} failed with exception {exception.Message}");
		}

		public void ReportRunningTrial(TrialSettings setting)
		{
			return;
		}
	}
}