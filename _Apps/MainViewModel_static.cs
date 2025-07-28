using Microsoft.ML.AutoML;
using System;
using System.Collections.Generic;
using TBird.Core;

namespace Netkeiba
{
	public static class MainViewModel_static
	{
		public static float SINGLE(this Dictionary<string, object> x, string key) => x[key].GetSingle();

		public static AutoMLExperiment SetMicrosecondRandomTuner(this AutoMLExperiment ml)
		{
			var i = DateTime.Now.Microsecond % 5;
			return i switch
			{
				0 => ml.SetCostFrugalTuner(),
				1 => ml.SetSmacTuner(),
				2 => ml.SetGridSearchTuner(),
				3 => ml.SetRandomSearchTuner(),
				_ => ml.SetEciCostFrugalTuner()
			};
		}
	}
}