using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba
{
	#region PredictionSource

	public class PredictionSource
	{
		public const int Count = 751;

		[LoadColumn(0, Count)]
		[VectorType(Count)]
		public float[] Features { get; set; } = new float[0];

		[LoadColumn(Count + 1)]
		public long ﾚｰｽID { get; set; }
	}

	public class BinaryClassificationSource : PredictionSource
	{
		[LoadColumn(Count)]
		public bool 着順 { get; set; }
	}

	public class MultiClassificationSource : PredictionSource
	{
		[LoadColumn(Count)]
		public uint 着順 { get; set; }
	}

	public class RegressionSource : PredictionSource
	{
		[LoadColumn(Count)]
		public float 着順 { get; set; }
	}

	#endregion

	#region ModelPrediction

	public abstract class ModelPrediction
	{
		[ColumnName("Label")]
		public float Label { get; set; }

		[ColumnName("Score")]
		public float Score { get; set; }

		public override string ToString()
		{
			return $"{Score}";
		}

		public virtual float GetScore()
		{
			return Score;
		}
	}

	public class BinaryClassificationPrediction : ModelPrediction
	{
		[ColumnName("Probability")]
		public float Probability { get; set; }

		[ColumnName("PredictedLabel")]
		public bool PredictedLabel { get; set; }

		public override float GetScore()
		{
			return base.GetScore() * Probability;
		}
	}

	public class MultiClassificationPrediction : ModelPrediction
	{
		[ColumnName("Probability")]
		public float Probability { get; set; }

		[ColumnName("PredictedLabel")]
		public bool PredictedLabel { get; set; }

		public override float GetScore()
		{
			return base.GetScore() * Probability;
		}
	}

	public class RegressionPrediction : ModelPrediction
	{
		public override float GetScore()
		{
			return 5 - base.GetScore();
		}
	}

	#endregion

	#region PredictionResult

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

	#endregion

	#region PredictionFactory

	public abstract class PredictionFactory<TSrc, TDst> where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
	{
		public PredictionFactory(MLContext context, string rank, int index, ITransformer model) : this(context, rank, index, true)
		{
			_engine = _context.Model.CreatePredictionEngine<TSrc, TDst>(model);
		}

		public PredictionFactory(MLContext context, string rank, int index, bool isnew)
		{
			_context = context;
			_rank = rank;
			_index = index;
			_isnew = isnew;
		}

		protected MLContext _context;
		protected string _rank;
		protected int _index;
		protected bool _isnew;
		protected float _score;
		protected PredictionResult? _result;
		protected PredictionEngineBase<TSrc, TDst>? _engine;

		public PredictionResult GetResult() => _result = _result ?? GetResult(_rank, _index);

		public PredictionEngineBase<TSrc, TDst> GetEngine()
		{
			return _engine = _engine ?? _context.Model.CreatePredictionEngine<TSrc, TDst>(_context.Model.Load(GetResult().Path, out DataViewSchema schema));
		}

		public float Predict(byte[] bytes, long raceid)
		{
			return Predict(AppUtil.ToSingles(bytes), raceid);
		}

		public virtual float Predict(float[] features, long raceid)
		{
			return _score = GetEngine().Predict(new TSrc() { Features = features, ﾚｰｽID = raceid }).GetScore() * (_isnew ? 1F : GetResult().GetScore());
		}

		public float GetScore()
		{
			return _score;
		}

		protected abstract PredictionResult GetResult(string rank, int index);
	}

	public class BinaryClassificationPredictionFactory : PredictionFactory<BinaryClassificationSource, BinaryClassificationPrediction>
	{
		public BinaryClassificationPredictionFactory(MLContext context, string rank, int index, ITransformer model) : base(context, rank, index, model)
		{

		}

		public BinaryClassificationPredictionFactory(MLContext context, string rank, int index) : base(context, rank, index, false)
		{

		}

		public override float Predict(float[] features, long raceid)
		{
			return base.Predict(features, raceid) * (_index < 5 ? 1 : -1);
		}

		protected override PredictionResult GetResult(string rank, int index)
		{
			return AppSetting.Instance.GetBinaryClassificationResult(index, rank);
		}
	}

	public class MultiClassificationPredictionFactory : PredictionFactory<MultiClassificationSource, MultiClassificationPrediction>
	{
		public MultiClassificationPredictionFactory(MLContext context, string rank, int index, ITransformer model) : base(context, rank, index, model)
		{

		}

		public MultiClassificationPredictionFactory(MLContext context, string rank, int index) : base(context, rank, index, false)
		{

		}

		public override float Predict(float[] features, long raceid)
		{
			return base.Predict(features, raceid) * (_index < 5 ? 1 : -1);
		}

		protected override PredictionResult GetResult(string rank, int index)
		{
			return AppSetting.Instance.GetMultiClassificationResult(index, rank);
		}
	}

	public class RegressionPredictionFactory : PredictionFactory<RegressionSource, RegressionPrediction>
	{
		public RegressionPredictionFactory(MLContext context, string rank, int index, ITransformer model) : base(context, rank, index, model)
		{

		}

		public RegressionPredictionFactory(MLContext context, string rank, int index) : base(context, rank, index, false)
		{

		}

		protected override PredictionResult GetResult(string rank, int index)
		{
			return AppSetting.Instance.GetRegressionResult(index, rank);
		}
	}

	#endregion

}