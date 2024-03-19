﻿using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Netkeiba
{
	public class PredictionSource
	{
		public const int Count = 751;

		[LoadColumn(0, Count)]
		[VectorType(Count)]
		public float[] Features { get; set; } = new float[0];

		public PredictionSource()
		{

		}

		public PredictionSource(byte[] bytes)
		{
			Features = Enumerable.Range(0, bytes.Length / 4).Select(i => BitConverter.ToSingle(bytes, i * 4)).ToArray();
		}
	}

	public class BinaryClassificationSource : PredictionSource
	{
		public BinaryClassificationSource()
		{

		}

		public BinaryClassificationSource(byte[] bytes) : base(bytes)
		{

		}

		[LoadColumn(Count)]
		public bool 着順 { get; set; }
	}

	public class RegressionSource : PredictionSource
	{
		public RegressionSource()
		{

		}

		public RegressionSource(byte[] bytes) : base(bytes)
		{

		}

		[LoadColumn(Count)]
		public float 着順 { get; set; }
	}

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

	public class RegressionPrediction : ModelPrediction
	{
		public override float GetScore()
		{
			return (5 - base.GetScore());
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

	public abstract class PredictionFactory<TSrc, TDst> where TSrc : PredictionSource where TDst : ModelPrediction, new()
	{
		public PredictionFactory(MLContext context, string rank, int index, bool minus)
		{
			_context = context;
			_rank = rank;
			_index = index;
			_minus = minus;
		}

		protected MLContext _context;
		protected string _rank;
		protected int _index;
		protected bool _minus;
		protected float _score;
		protected PredictionResult? _result;
		protected PredictionEngineBase<TSrc, TDst>? _engine;

		protected PredictionResult GetResult() => _result = _result ?? GetResult(_rank, _index);

		public PredictionEngineBase<TSrc, TDst> GetEngine()
		{
			return _engine = _engine ?? _context.Model.CreatePredictionEngine<TSrc, TDst>(_context.Model.Load(GetResult().Path, out DataViewSchema schema));
		}

		public float Predict(float[] features)
		{
			return _score = GetEngine().Predict(GetSrc(features)).GetScore() * GetResult().GetScore() * (_minus ? -1 : 1);
		}

		public float GetScore()
		{
			return _score;
		}

		public abstract string Name { get; }

		protected abstract PredictionResult GetResult(string rank, int index);

		protected abstract TSrc GetSrc(float[] bytes);
	}

	public class BinaryClassificationPredictionFactory : PredictionFactory<BinaryClassificationSource, BinaryClassificationPrediction>
	{
		public BinaryClassificationPredictionFactory(MLContext context, string rank, int index, bool minus) : base(context, rank, index, minus)
		{

		}

		public override string Name => $"Bin{_index}";

		protected override PredictionResult GetResult(string rank, int index)
		{
			return AppSetting.Instance.GetBinaryClassificationResult(index, rank);
		}

		protected override BinaryClassificationSource GetSrc(float[] features)
		{
			return new BinaryClassificationSource() { Features = features };
		}
	}

	public class RegressionPredictionFactory : PredictionFactory<RegressionSource, RegressionPrediction>
	{
		public RegressionPredictionFactory(MLContext context, string rank, int index, bool minus) : base(context, rank, index, minus)
		{

		}

		public override string Name => $"Reg{_index}";

		protected override PredictionResult GetResult(string rank, int index)
		{
			return AppSetting.Instance.GetRegressionResult(index, rank);
		}

		protected override RegressionSource GetSrc(float[] features)
		{
			return new RegressionSource() { Features = features };
		}
	}

	public class PredictionResult
	{
		public PredictionResult()
		{
			Path = string.Empty;
			Rank = string.Empty;
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

		public BinaryClassificationResult(string path, string rank, int index, uint second, CalibratedBinaryClassificationMetrics metrics, float score, float rate)
		{
			Index = index;
			Rank = rank;
			Second = second;
			Path = path;
			Score = score;
			Rate = rate;
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
		public static readonly RegressionResult Default = new RegressionResult()
		{
			Path = string.Empty
		};

		public RegressionResult()
		{

		}

		public RegressionResult(string path, string rank, int index, uint second, RegressionMetrics metrics, float score, float rate)
		{
			Path = path;
			Rank = rank;
			Index = index;
			Second = second;
			Score = score;
			Rate = rate;
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