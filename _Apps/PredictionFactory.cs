using ControlzEx.Standard;
using Microsoft.ML;
using System;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core;
using TBird.Wpf;
using Tensorflow.Keras.Engine;

namespace Netkeiba
{
    public abstract class PredictionFactory<TSrc, TDst> where TSrc : PredictionSource, new() where TDst : ModelPrediction, new()
    {
        public PredictionFactory(MLContext context, string rank, string index, ITransformer model) : this(context, rank, index)
        {
            _model = model;
        }

        public PredictionFactory(MLContext context, string rank, string index, PredictionResult result) : this(context, rank, index)
        {
            _result = result;
        }

        private PredictionFactory(MLContext context, string rank, string index)
        {
            _context = context;
            _rank = rank;
            _index = index;
        }

        protected MLContext _context;
        protected string _rank;
        protected string _index;
        protected float _score;
        protected PredictionResult? _result;
        protected ITransformer? _model;
        protected PredictionEngineBase<TSrc, TDst>? _engine;

        public PredictionEngineBase<TSrc, TDst> GetEngine()
        {
            _model = _model ?? _context.Model.Load(_result.NotNull().Path, out DataViewSchema schema);
            _engine = _engine ?? _context.Model.CreatePredictionEngine<TSrc, TDst>(_model);
            return _engine;
        }

        public float Predict(byte[] bytes, long raceid)
        {
            return Predict(AppUtil.ToSingles(bytes), raceid);
        }

        public virtual float Predict(float[] features, long raceid)
        {
            var src = new TSrc().Run(x =>
            {
                x.SetFeatures(AppUtil.FilterFeatures(features, _rank));
                x.ﾚｰｽID = raceid;
            });

            return _score = GetEngine().Predict(src).GetScore() * 1F/*(_isnew ? 1F : GetResult().GetScore())*/;
        }

        public float GetScore()
        {
            return _score;
        }
    }

    public class BinaryClassificationPredictionFactory : PredictionFactory<BinaryClassificationSource, BinaryClassificationPrediction>
    {
        public BinaryClassificationPredictionFactory(MLContext context, string rank, string index, ITransformer model) : base(context, rank, index, model)
        {

        }

        public BinaryClassificationPredictionFactory(MLContext context, string rank, string index, PredictionResult result) : base(context, rank, index, result)
        {

        }

        public override float Predict(float[] features, long raceid)
        {
            return base.Predict(features, raceid) * (_index.Split('-')[0].GetInt32() < 6 ? 1 : -1);
        }
    }

    public class RankingPredictionFactory : PredictionFactory<RankingSource, RankingPrediction>
    {
        public RankingPredictionFactory(MLContext context, string rank, string index, ITransformer model) : base(context, rank, index, model)
        {

        }

        public RankingPredictionFactory(MLContext context, string rank, string index, PredictionResult result) : base(context, rank, index, result)
        {

        }

        public override float Predict(float[] features, long raceid)
        {
            return base.Predict(features, raceid) * (_index.Split('-')[0].GetInt32() < 6 ? 1 : -1);
        }
    }

    public class RegressionPredictionFactory : PredictionFactory<RegressionSource, RegressionPrediction>
    {
        public RegressionPredictionFactory(MLContext context, string rank, string index, ITransformer model) : base(context, rank, index, model)
        {

        }

        public RegressionPredictionFactory(MLContext context, string rank, string index, PredictionResult result) : base(context, rank, index, result)
        {

        }
    }
}