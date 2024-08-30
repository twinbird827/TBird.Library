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
            RN = GetRank(x => x.RN);
            M1 = GetRank(x => x.M1);
            M2 = GetRank(x => x.M2);
            M3 = GetRank(x => x.M3);
            M4 = GetRank(x => x.M4);
            Avg = Arr(B1, B2, B3, B4, RN, M1, M2, M3, M4).Average(x => x.Single());

            int GetRank(Func<ResultDetail, float> func)
            {
                return all
                    .OrderByDescending(func)
                    .ThenByDescending(x => x.Sum)
                    .Select(func)
                    .IndexOf(func(target));
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

        public int Rank { get; set; }

        public int B1 { get; set; }

        public int B2 { get; set; }

        public int B3 { get; set; }

        public int B4 { get; set; }

        public int RN { get; set; }

        public int M1 { get; set; }

        public int M2 { get; set; }

        public int M3 { get; set; }

        public int M4 { get; set; }

        [DisplayFormat(DataFormatString = "{0:F2}")]
        public float Avg { get; set; }

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float Sum => Source.Sum;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B1Str => Source.B1;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B2Str => Source.B2;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B3Str => Source.B3;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float B4Str => Source.B4;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float RNStr => Source.RN;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float M1Str => Source.M1;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float M2Str => Source.M2;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float M3Str => Source.M3;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float M4Str => Source.M4;

        [DisplayFormat(DataFormatString = "{0:F1}")]
        public float Rate => Source.Run(s => Arr(s.B1, s.B2, s.B3, s.B4, s.RN)).Run(arr => arr.Count(x => 0F < x).Single() / arr.Count().Single());

    }
}