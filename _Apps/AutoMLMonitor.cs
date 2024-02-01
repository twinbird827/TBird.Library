using Microsoft.ML.AutoML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

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
			_vm.AddLog("Begin best trial report *******************************");
			ReportCompletedTrial(result);
			_vm.AddLog("End best trial report *******************************");
			return;
		}

		public void ReportCompletedTrial(TrialResult result)
		{
			var id = result.TrialSettings.TrialId;
			var ms = result.DurationInMilliseconds;
			var mc = result.Metric;
			var pl = _pipeline.ToString(result.TrialSettings.Parameter);
			_vm.AddLog($"Trial={id}; DurationInMilliseconds={ms}; Loss={result.Loss}; Metric={mc}; Pipeline={pl};");
			_completedTrials.Add(result);
		}

		public void ReportFailTrial(TrialSettings settings, Exception exception = null)
		{
			if (exception.Message.Contains("Operation was canceled."))
			{
				_vm.AddLog($"{settings.TrialId} cancelled. Time budget exceeded.");
			}
			_vm.AddLog($"{settings.TrialId} failed with exception {exception.Message}");
		}

		public void ReportRunningTrial(TrialSettings setting)
		{
			return;
		}
	}
}