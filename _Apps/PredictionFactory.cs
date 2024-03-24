using Microsoft.ML;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba
{
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
			var src = new TSrc().Run(x =>
			{
				x.SetFeatures(features);
				x.ﾚｰｽID = raceid;
			});

			return _score = GetEngine().Predict(src).GetScore() * (_isnew ? 1F : GetResult().GetScore());
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
}