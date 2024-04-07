using Microsoft.ML.Data;

namespace Netkeiba
{
	public class PredictionResult
	{
		public PredictionResult()
		{
			Path = string.Empty;
			Rank = string.Empty;
		}

		public PredictionResult(string path, string rank, int index, uint second, float score, float rate)
		{
			Path = path;
			Rank = rank;
			Index = index;
			Second = second;
			Score = score;
			Rate = rate;
		}

		public float Score { get; set; }

		public float Rate { get; set; }

		public int Index { get; set; }

		public uint Second { get; set; }

		public string Path { get; set; }

		public string Rank { get; set; }

		public float GetScore()
		{
			return Score * Score * Rate;
		}

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
		public static readonly BinaryClassificationResult Default = new BinaryClassificationResult()
		{
			Path = string.Empty
		};

		public BinaryClassificationResult()
		{

		}

		public BinaryClassificationResult(string path, string rank, int index, uint second, CalibratedBinaryClassificationMetrics metrics, float score, float rate) : base(path, rank, index, second, score, rate)
		{
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

	public class MultiClassificationResult : PredictionResult
	{
		public static readonly MultiClassificationResult Default = new MultiClassificationResult()
		{
			Path = string.Empty
		};

		public MultiClassificationResult()
		{

		}

		public MultiClassificationResult(string path, string rank, int index, uint second, MulticlassClassificationMetrics metrics, float score, float rate) : base(path, rank, index, second, score, rate)
		{
			LogLoss = metrics.LogLoss;
			LogLossReduction = metrics.LogLossReduction;
			MacroAccuracy = metrics.MacroAccuracy;
			MicroAccuracy = metrics.MicroAccuracy;
			TopKAccuracy = metrics.TopKAccuracy;
			TopKPredictionCount = metrics.TopKPredictionCount;
		}

		public double LogLoss { get; set; }

		public double LogLossReduction { get; set; }

		public double MacroAccuracy { get; set; }

		public double MicroAccuracy { get; set; }

		public double TopKAccuracy { get; set; }

		public double TopKPredictionCount { get; set; }
	}

	public class RegressionResult : PredictionResult
	{
		public static readonly RegressionResult Default = new RegressionResult()
		{
			Path = string.Empty
		};

		public RegressionResult()
		{

		}

		public RegressionResult(string path, string rank, int index, uint second, RegressionMetrics metrics, float score, float rate) : base(path, rank, index, second, score, rate)
		{
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