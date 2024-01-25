using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	public class ModelRow
	{
		[LoadColumn(1, 152)]
		[VectorType(152)]
		public float[] Features { get; set; }
		[LoadColumn(153)]
		public bool 着順 { get; set; }

		internal Dictionary<string, double> Source { get; set; }
		internal ModelRowPrediction Prediction { get; set; }
	}

	public class ModelRowPrediction
	{
		[ColumnName("PredictedLabel")]
		// Predicted label from the trainer.
		public bool Predicted { get; set; }

		[ColumnName("Score")]
		// Predicted label from the trainer.
		public float Score { get; set; }

		[ColumnName("Probability")]
		public float Probability { get; set; }
	}
}
