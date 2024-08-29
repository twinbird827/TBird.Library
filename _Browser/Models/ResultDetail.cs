using TBird.Core;

namespace Browser.Models
{
    public class ResultDetail : TBirdObject
    {
        public ResultDetail(string[] line)
        {
            Netkeiba = line[0];
            Class1 = line[1];
            RaceName = line[2];
            Place = line[3];
            Race = int.TryParse(line[4], out int i1) ? i1 : 1;
            Waku = int.TryParse(line[5], out int i2) ? i2 : 1;
            Umano = int.TryParse(line[6], out int i3) ? i3 : 1;
            Umaname = line[7];
            // tyakujun = line[8]
            B1 = float.TryParse(line[9 + 0], out float f1) ? f1 : 0F;
            B2 = float.TryParse(line[9 + 1], out float f2) ? f2 : 0F;
            B3 = float.TryParse(line[9 + 2], out float f3) ? f3 : 0F;
            B4 = float.TryParse(line[9 + 3], out float f4) ? f4 : 0F;
            RN = float.TryParse(line[9 + 4], out float f5) ? f5 : 0F;
            M1 = float.TryParse(line[9 + 5], out float f6) ? f6 : 0F;
            M2 = float.TryParse(line[9 + 6], out float f7) ? f7 : 0F;
            M3 = float.TryParse(line[9 + 7], out float f8) ? f8 : 0F;
            M4 = float.TryParse(line[9 + 8], out float f9) ? f9 : 0F;
            Sum = Arr(B1, B2, B3, B4, RN).Sum();
        }

        public bool IsOK() => new string[] { Netkeiba, RaceName, Class1, Place, Umaname }.All(x => !string.IsNullOrEmpty(x)) &&
            1 <= Race && Race <= 12 &&
            0 < Waku && 0 < Umano &&
            new float[] { B1, B2, B3, B4, RN }.Any(x => x != 0F);

        public string Netkeiba { get; set; }

        public string RaceName { get; set; }

        public string Class1 { get; set; }

        public string Place { get; set; }

        public int Race { get; set; }

        public int Waku { get; set; }

        public int Umano { get; set; }

        public string Umaname { get; set; }

        public float Sum { get; set; }

        public float B1 { get; set; }

        public float B2 { get; set; }

        public float B3 { get; set; }

        public float B4 { get; set; }

        public float RN { get; set; }

        public float M1 { get; set; }

        public float M2 { get; set; }

        public float M3 { get; set; }

        public float M4 { get; set; }

    }
}