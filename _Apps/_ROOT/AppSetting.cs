using Microsoft.ML.AutoML;
using System.Collections.Generic;
using System.Linq;
using TBird.Core;

namespace Netkeiba
{
    public class AppSetting : JsonBase<AppSetting>
    {
        public static AppSetting Instance { get; } = new AppSetting();

        public AppSetting(string path) : base(path)
        {
            if (!Load())
            {
                TrainingTimeSecond = [1800, 2000, 2200];
                BinaryClassificationResults = [];
                RegressionResults = [];
                NetkeibaResult = "result";

            }
        }

        public AppSetting() : this(@"lib\app-setting.json")
        {

        }

        public bool UseFastForest
        {
            get => GetProperty(_UseFastForest);
            set => SetProperty(ref _UseFastForest, value);
        }
        private bool _UseFastForest = false;

        public bool UseFastTree
        {
            get => GetProperty(_UseFastTree);
            set => SetProperty(ref _UseFastTree, value);
        }
        private bool _UseFastTree = false;

        public bool UseLgbm
        {
            get => GetProperty(_UseLgbm);
            set => SetProperty(ref _UseLgbm, value);
        }
        private bool _UseLgbm = true;

        public bool UseLbfgsPoissonRegression
        {
            get => GetProperty(_UseLbfgsPoissonRegression);
            set => SetProperty(ref _UseLbfgsPoissonRegression, value);
        }
        private bool _UseLbfgsPoissonRegression = false;

        public bool UseSdca
        {
            get => GetProperty(_UseSdca);
            set => SetProperty(ref _UseSdca, value);
        }
        private bool _UseSdca = false;

        public bool UseSdcaLogisticRegression
        {
            get => GetProperty(_UseSdcaLogisticRegression);
            set => SetProperty(ref _UseSdcaLogisticRegression, value);
        }
        private bool _UseSdcaLogisticRegression = false;

        public bool UseLbfgsLogisticRegression
        {
            get => GetProperty(_UseLbfgsLogisticRegression);
            set => SetProperty(ref _UseLbfgsLogisticRegression, value);
        }
        private bool _UseLbfgsLogisticRegression = false;

        public BinaryClassificationMetric BinaryClassificationMetric
        {
            get => GetProperty(_BinaryClassificationMetric);
            set => SetProperty(ref _BinaryClassificationMetric, value);
        }
        private BinaryClassificationMetric _BinaryClassificationMetric = BinaryClassificationMetric.AreaUnderRocCurve;

        public RegressionMetric RegressionMetric
        {
            get => GetProperty(_RegressionMetric);
            set => SetProperty(ref _RegressionMetric, value);
        }
        private RegressionMetric _RegressionMetric = RegressionMetric.RSquared;

        public int[] TrainingTimeSecond
        {
            get => GetProperty(_TrainingTimeSecond);
            set => SetProperty(ref _TrainingTimeSecond, value);
        }
        private int[] _TrainingTimeSecond = [];

        public uint MinimumTrainingTimeSecond
        {
            get => GetProperty(_MinimumTrainingTimeSecond);
            set => SetProperty(ref _MinimumTrainingTimeSecond, value);
        }
        private uint _MinimumTrainingTimeSecond = 600;

        public uint MaximumTrainingTimeSecond
        {
            get => GetProperty(_MaximumTrainingTimeSecond);
            set => SetProperty(ref _MaximumTrainingTimeSecond, value);
        }
        private uint _MaximumTrainingTimeSecond = 3600;

        public uint TrainingCount
        {
            get => GetProperty(_TrainingCount);
            set => SetProperty(ref _TrainingCount, value);
        }
        private uint _TrainingCount = 3;

        public string[]? Features
        {
            get => GetProperty(_Features);
            set => SetProperty(ref _Features, value);
        }
        private string[]? _Features;

        public BinaryClassificationResult[] BinaryClassificationResults
        {
            get => GetProperty(_BinaryClassificationResults);
            set => SetProperty(ref _BinaryClassificationResults, value);
        }
        public BinaryClassificationResult[] _BinaryClassificationResults = [];

        public void UpdateBinaryClassificationResults(BinaryClassificationResult now)
        {
            BinaryClassificationResults = BinaryClassificationResults.Concat(Arr(now)).ToArray();
            Save();
        }

        //public BinaryClassificationResult GetBinaryClassificationResult(int skip, bool left, string rank)
        //{
        //    return BinaryClassificationResults
        //        .Where(x => x.Rank == rank && (left ? x.Index < 6 : 6 <= x.Index))
        //        .OrderByDescending(x => x.GetScore())
        //        .Skip(skip).FirstOrDefault() ?? BinaryClassificationResult.Default;
        //    //return BinaryClassificationResults.Where(x => x.Index == index && x.Rank == rank).Run(arr =>
        //    //{
        //    //	return arr.FirstOrDefault(x => x.GetScore() == arr.Max(y => y.GetScore())) ?? BinaryClassificationResult.Default;
        //    //});
        //}

        public IEnumerable<BinaryClassificationResult> GetBinaryClassificationResults(string index, string rank)
        {
            return BinaryClassificationResults.Where(x => x.Index == index && x.Rank == rank);
        }

        public BinaryClassificationResult GetBinaryClassificationResult(string index, string rank)
        {
            return BinaryClassificationResults.Where(x => x.Index == index && x.Rank == rank).Run(arr =>
            {
                return arr.FirstOrDefault(x => x.GetScore() == arr.Max(y => y.GetScore())) ?? BinaryClassificationResult.Default;
            });
        }

        public RankingResult[] RankingResults
        {
            get => GetProperty(_RankingResults);
            set => SetProperty(ref _RankingResults, value);
        }
        public RankingResult[] _RankingResults = new RankingResult[] { };

        public void UpdateRankingResults(RankingResult now)
        {
            RankingResults = RankingResults.Concat(Arr(now)).ToArray();
            Save();
        }

        public IEnumerable<RankingResult> GetRankingResults(string index, string rank)
        {
            return RankingResults.Where(x => x.Index == index && x.Rank == rank);
        }

        public RankingResult GetRankingResult(string index, string rank)
        {
            return GetRankingResults(index, rank).Run(arr =>
            {
                return arr.FirstOrDefault(x => x.GetScore() == arr.Max(y => y.GetScore())) ?? RankingResult.Default;
            });
        }

        public RegressionResult[] RegressionResults
        {
            get => GetProperty(_RegressionResults);
            set => SetProperty(ref _RegressionResults, value);
        }
        public RegressionResult[] _RegressionResults = [];

        public void UpdateRegressionResults(RegressionResult now)
        {
            RegressionResults = RegressionResults.Concat(Arr(now)).ToArray();
            Save();
        }

        public IEnumerable<RegressionResult> GetRegressionResults(string index, string rank)
        {
            return RegressionResults.Where(x => x.Index == index && x.Rank == rank);
        }

        public RegressionResult GetRegressionResult(string index, string rank)
        {
            return RegressionResults.Where(x => x.Index == index && x.Rank == rank).Run(arr =>
            {
                return arr.FirstOrDefault(x => x.GetScore() == arr.Max(y => y.GetScore())) ?? RegressionResult.Default;
            });
        }

        public string[] Correls
        {
            get => GetProperty(_Correls);
            set => SetProperty(ref _Correls, value);
        }
        public string[] _Correls = Enumerable.Empty<string>().ToArray();

        public List<ColumnFilter> DicCor
        {
            get => GetProperty(_DicCor);
            set => SetProperty(ref _DicCor, value);
        }
        public List<ColumnFilter> _DicCor = new();

        public string Correl
        {
            get => GetProperty(_Correl);
            set => SetProperty(ref _Correl, value);
        }
        private string _Correl = "0.025";

        public string NetkeibaId
        {
            get => GetProperty(_NetkeibaId);
            set => SetProperty(ref _NetkeibaId, value);
        }
        private string _NetkeibaId = string.Empty;

        public string NetkeibaPassword
        {
            get => GetProperty(_NetkeibaPassword);
            set => SetProperty(ref _NetkeibaPassword, value);
        }
        private string _NetkeibaPassword = string.Empty;

        public string NetkeibaResult
        {
            get => GetProperty(_NetkeibaResult);
            set => SetProperty(ref _NetkeibaResult, value);
        }
        private string _NetkeibaResult = string.Empty;

        public string OrderBys
        {
            get => GetProperty(_OrderBys);
            set => SetProperty(ref _OrderBys, value);
        }
        private string _OrderBys = "3,4,5";

    }
}