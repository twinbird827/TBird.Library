using Microsoft.ML.Data;
using Microsoft.ML.OnnxRuntime.Tensors;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
	public partial class PredictionSource
	{
		public void SetFeatures(float[] features)
		{
			features.ForEach((x, i) => dic[i](x));
		}
	}

	public class BinaryClassificationSource : PredictionSource
	{
		[LoadColumn(0)]
		public bool 着順 { get; set; }
	}

	public class MultiClassificationSource : PredictionSource
	{
		[LoadColumn(0)]
		public uint 着順 { get; set; }
	}

	public class RegressionSource : PredictionSource
	{
		[LoadColumn(0)]
		public float 着順 { get; set; }
	}
}