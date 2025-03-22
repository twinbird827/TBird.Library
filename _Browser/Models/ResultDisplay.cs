using System.ComponentModel.DataAnnotations;
using TBird.Core;

namespace Browser.Models
{
    public class ResultDisplay : TBirdObject
    {
        public ResultDisplay(ResultDetail target, IEnumerable<ResultDetail> all)
        {
            Source = target;

            B1 = GetRank(x => x.B1);
            B2 = GetRank(x => x.B2);
            B3 = GetRank(x => x.B3);
            B4 = GetRank(x => x.B4);
            B6 = GetRank(x => x.B6);
            B7 = GetRank(x => x.B7);
            B8 = GetRank(x => x.B8);
            B9 = GetRank(x => x.B9);
            RN = GetRank(x => x.RN);
            Avg = GetRank(x => x.Avg);
            All = Arr(B1Str, B2Str, B3Str, B4Str, B6Str, B7Str, B8Str, B9Str, AvgStr).All(x => 0 < x);
            Any = Arr(B1Str, B2Str, B3Str, B4Str, B6Str, B7Str, B8Str, B9Str, AvgStr).Any(x => 0 < x);

            int GetRank(Func<ResultDetail, float> func)
            {
                return all
                    .OrderByDescending(func)
                    .ThenByDescending(x => x.Avg)
                    .Select(x => x.Umano)
                    .IndexOf(target.Umano);
            }
        }

        public ResultDetail Source { get; set; }

        public string Netkeiba => Source.Netkeiba;

        public string RaceName => Source.RaceName;

        public string Class1 => Source.Class1;

        public string Place => Source.Place;

        public int Race => Source.Race;

        [Display(Name = "枠番")]
        public int Waku => Source.Waku;

        [Display(Name = "馬番")]
        public int Umano => Source.Umano;

        [Display(Name = "馬名")]
        public string Umaname => Source.Umaname;

        [Display(Name = "順位")]
        public int Rank => Source.Rank;

        [DisplayFormat(DataFormatString = "{0:F2}")]
        public float AvgStr => Source.Avg;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B1Str => Source.B1;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B2Str => Source.B2;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B3Str => Source.B3;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B4Str => Source.B4;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B6Str => Source.B6;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B7Str => Source.B7;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B8Str => Source.B8;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B9Str => Source.B9;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float RNStr => (1000F / Source.RN) - 100F;

        public int B1 { get; set; }

        public int B2 { get; set; }

        public int B3 { get; set; }

        public int B4 { get; set; }

        public int B6 { get; set; }

        public int B7 { get; set; }

        public int B8 { get; set; }

        public int B9 { get; set; }

        public int RN { get; set; }

        public int Avg { get; set; }

        public bool All { get; set; }

        public bool Any { get; set; }

    }
}