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
            Race = GetInt32(4);
            Waku = GetInt32(5);
            Umano = GetInt32(6);
            Umaname = line[7];
            Rank = GetInt32(8, -1);
            B1 = GetSingle(9 + 0);
            B2 = GetSingle(9 + 1);
            B3 = GetSingle(9 + 2);
            B4 = GetSingle(9 + 3);
            B6 = GetSingle(9 + 4);
            B7 = GetSingle(9 + 5);
            B8 = GetSingle(9 + 6);
            B9 = GetSingle(9 + 7);
            RN = 1F / GetSingle(9 + 8);
            Avg = Arr(B1, B2, B3, B4, B6, B7, B8, B9).Average();

            int GetInt32(int i, int def = 1) => int.TryParse(line[i], out int x) ? x : def;
            float GetSingle(int i, float def = 0F) => float.TryParse(line[i], out float x) ? x : def;
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

        public int Rank { get; set; }

        public float B1 { get; set; }

        public float B2 { get; set; }

        public float B3 { get; set; }

        public float B4 { get; set; }

        public float B6 { get; set; }

        public float B7 { get; set; }

        public float B8 { get; set; }

        public float B9 { get; set; }

        public float RN { get; set; }

        public float Avg { get; set; }

    }
}