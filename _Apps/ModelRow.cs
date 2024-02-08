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
		[LoadColumn(1, 630)]
		[VectorType(630)]
		public float[] Features { get; set; } = new float[0];
	}

	public class BinaryClassificationSource : PredictionSource
	{
		[LoadColumn(631)]
		public bool 着順 { get; set; }
	}

	public class RegressionSource : PredictionSource
	{
		[LoadColumn(631)]
		public float 着順 { get; set; }
	}

	public class RegressionPrediction
	{
		[ColumnName("Label")]
		public float Label { get; set; }

		[ColumnName("Score")]
		public float Score { get; set; }

		public static string[] GetHeaders(string head) => new[] { $"{head}_{nameof(Score)}" };

		public override string ToString()
		{
			return $"{Score}";
		}

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

		public float Magnification { get; set; }

		public BinaryClassificationPrediction SetMagnification(float magnification)
		{
			Magnification = magnification;
			return this;
		}

		public static string[] GetHeaders(string head) => new[] { $"{head}_{nameof(Score)}" };

		public override string ToString()
		{
			return $"{Score}";
		}
	}

	public class PredictionResult
	{
		public PredictionResult()
		{
			Path = string.Empty;
		}

		public int Index { get; set; }

		public int Second { get; set; }

		public string Path { get; set; }

		public override string ToString()
		{
			return Path;
		}

		public override int GetHashCode()
		{
			return Path.GetHashCode();
		}

		public override bool Equals(object? obj)
		{
			if (obj is PredictionResult tmp)
			{
				return Path == tmp.Path;
			}
			else
			{
				return base.Equals(obj);
			}
		}
	}

	public class BinaryClassificationResult : PredictionResult
	{
		public BinaryClassificationResult()
		{

		}

		public BinaryClassificationResult(string path, int index, int second, CalibratedBinaryClassificationMetrics metrics)
		{
			Index = index;
			Second = second;
			Path = path;
			Accuracy = metrics.Accuracy;
			AreaUnderPrecisionRecallCurve = metrics.AreaUnderPrecisionRecallCurve;
			AreaUnderRocCurve = metrics.AreaUnderRocCurve;
			Entropy = metrics.Entropy;
			F1Score = metrics.F1Score;
			LogLoss = metrics.LogLoss;
			LogLossReduction = metrics.LogLossReduction;
			NegativePrecision = metrics.NegativePrecision;
			NegativeRecall = metrics.NegativeRecall;
			PositivePrecision = metrics.PositivePrecision;
			PositiveRecall = metrics.PositiveRecall;
		}

		public double Accuracy { get; set; }

		public double AreaUnderPrecisionRecallCurve { get; set; }

		public double AreaUnderRocCurve { get; set; }

		public double Entropy { get; set; }

		public double F1Score { get; set; }

		public double LogLoss { get; set; }

		public double LogLossReduction { get; set; }

		public double NegativePrecision { get; set; }

		public double NegativeRecall { get; set; }

		public double PositivePrecision { get; set; }

		public double PositiveRecall { get; set; }

	}

	public class RegressionResult : PredictionResult
	{
		public RegressionResult()
		{

		}

        public RegressionResult(string path, int index, int second, RegressionMetrics metrics)
		{
			Path = path;
			Index = index;
			Second = second;
			RSquared = metrics.RSquared;
			MeanSquaredError = metrics.MeanSquaredError;
			RootMeanSquaredError = metrics.RootMeanSquaredError;
			LossFunction = metrics.LossFunction;
			MeanAbsoluteError = metrics.MeanAbsoluteError;
		}

		public double RSquared { get; set; }

		public double MeanSquaredError { get; set; }

		public double RootMeanSquaredError { get; set; }

		public double LossFunction { get; set; }

		public double MeanAbsoluteError { get; set; }
	}
}