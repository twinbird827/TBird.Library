using Microsoft.ML.Data;
using System.Linq;

namespace Netkeiba
{
    public class PredictionResult
    {
        public PredictionResult()
        {
            Path = string.Empty;
            Rank = string.Empty;
            Index = string.Empty;
        }

        public PredictionResult(string path, string rank, string index, uint second, float score, float rate)
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

        public string Index { get; set; }

        public uint Second { get; set; }

        public string Path { get; set; }

        public string Rank { get; set; }

        public float GetScore()
        {
            return Score * Rate * Rate * Rate;
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

        public BinaryClassificationResult(string path, string rank, string index, uint second, CalibratedBinaryClassificationMetrics metrics, float score, float rate) : base(path, rank, index, second, score, rate)
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

    public class RankingResult : PredictionResult
    {
        public static readonly RankingResult Default = new RankingResult()
        {
            Path = string.Empty
        };

        public RankingResult()
        {

        }

        public RankingResult(string path, string rank, string index, uint second, RankingMetrics metrics, float score, float rate) : base(path, rank, index, second, score, rate)
        {
            DiscountedCumulativeGains = metrics.DiscountedCumulativeGains.Average();
            NormalizedDiscountedCumulativeGains = metrics.NormalizedDiscountedCumulativeGains.Average();
        }

        public double DiscountedCumulativeGains { get; set; }

        public double NormalizedDiscountedCumulativeGains { get; set; }

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

        public RegressionResult(string path, string rank, string index, uint second, RegressionMetrics metrics, float score, float rate) : base(path, rank, index, second, score, rate)
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