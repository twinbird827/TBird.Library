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
            Initialize(context, rank, index, model);
        }

        public PredictionFactory(MLContext context, string rank, string index, PredictionResult result) : this(context, rank, index)
        {
            Initialize(context, rank, index, result);
        }

        private async void Initialize(MLContext context, string rank, string index, ITransformer model)
        {
            using (await Locker.LockAsync(_lockkey))
            {
                _engine = await Task.Run(() => _context.Model.CreatePredictionEngine<TSrc, TDst>(model));
            }
        }

        private async void Initialize(MLContext context, string rank, string index, PredictionResult result)
        {
            ITransformer model;
            using (await Locker.LockAsync(_lockkey))
            {
                model = await Task.Run(() => context.Model.Load(result.Path, out DataViewSchema schema));
            }
            Initialize(context, rank, index, model);
        }

        private static string _lockkey = Guid.NewGuid().ToString();

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
        protected PredictionEngineBase<TSrc, TDst>? _engine;

        public PredictionEngineBase<TSrc, TDst> GetEngine()
        {
            while (_engine == null) Thread.Sleep(10);
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