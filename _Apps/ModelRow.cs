using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	public class PredictionSource
	{
		[LoadColumn(1, 152)]
		[VectorType(152)]
		public float[] Features { get; set; } = new float[0];
	}

	public class BinaryClassificationSource : PredictionSource
	{
		[LoadColumn(153)]
		public bool 着順 { get; set; }
	}

	public class RegressionSource : PredictionSource
	{
		[LoadColumn(153)]
		public float 着順 { get; set; }
	}

	public class RegressionPrediction
	{
		[ColumnName("Label")]
		public float Label { get; set; }

		[ColumnName("Score")]
		public float Score { get; set; }

		public static string[] GetHeaders(string head) => new[] { $"{head}_{nameof(Score)}" };

		public string[] GetResults() => new[] { $"{Score}" };

	}

	public class BinaryClassificationPrediction
	{
		[ColumnName("Label")]
		public float Label { get; set; }

		[ColumnName("Score")]
		public float Score { get; set; }

		[ColumnName("Probability")]
		public float Probability { get; set; }

		[ColumnName("PredictedLabel")]
		public bool PredictedLabel { get; set; }

		public static string[] GetHeaders(string head) => new[] { $"{head}_{nameof(Score)}" };

		public string[] GetResults() => new[] { $"{Score}" };
	}

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
		//[ColumnName("PredictedLabel")]
		//// Predicted label from the trainer.
		//public bool Predicted { get; set; }
		[ColumnName("Label")]
		// Predicted label from the trainer.
		public float Predicted { get; set; }

		[ColumnName("Score")]
		// Predicted label from the trainer.
		public float Score { get; set; }

		//[ColumnName("Probability")]
		public float Probability { get; set; }
	}
}