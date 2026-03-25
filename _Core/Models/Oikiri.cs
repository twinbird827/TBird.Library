using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class Oikiri
	{
		private Oikiri(string course, string track, string rider,
			float time1, float time2, float time3, float time4, float time5,
			string timeRating1, string timeRating2, string timeRating3, string timeRating4, string timeRating5,
			string adaptation, string comment, string rating)
		{
			Course = course;
			Track = track;
			Rider = rider.Run(x => x == "助手" ? 0F : 1F);
			Time1 = time1;
			Time2 = time2;
			Time3 = time3;
			Time4 = time4;
			Time5 = time5;

			static float ToTimeRating(string s) => s switch
			{
				"TokeiColor01" => 1.0F,
				"TokeiColor02" => 0.5F,
				_ => 0.0F
			};

			TimeRating1 = ToTimeRating(timeRating1);
			TimeRating2 = ToTimeRating(timeRating2);
			TimeRating3 = ToTimeRating(timeRating3);
			TimeRating4 = ToTimeRating(timeRating4);
			TimeRating5 = ToTimeRating(timeRating5);

			Adaptation = adaptation switch
			{
				"一杯" => 1.00F,
				"Ｇ強" => 0.80F,
				"強め" => 0.60F,
				"馬也" => 0.40F,
				"攻手" => 0.20F,
				_ => 0.00F
			};

			Comment = comment;
			Rating = rating switch
			{
				"A" => 1.00F,
				"B" => 0.65F,
				"C" => 0.40F,
				"D" => 0.00F,
				_ => 0.30F
			};
		}

		public Oikiri(DbDataReader r, int hTrackIndex = 4, int offset = 31) : this(
			r.GetValue(offset + 0).Str(),       // コース
			r.GetValue(hTrackIndex).Str(),       // 馬場 (h.馬場互換)
			r.GetValue(offset + 2).Str(),        // 乗り役
			r.GetValue(offset + 3).Single(),     // 時間1
			r.GetValue(offset + 4).Single(),     // 時間2
			r.GetValue(offset + 5).Single(),     // 時間3
			r.GetValue(offset + 6).Single(),     // 時間4
			r.GetValue(offset + 7).Single(),     // 時間5
			r.GetValue(offset + 8).Str(),        // 時間評価1
			r.GetValue(offset + 9).Str(),        // 時間評価2
			r.GetValue(offset + 10).Str(),       // 時間評価3
			r.GetValue(offset + 11).Str(),       // 時間評価4
			r.GetValue(offset + 12).Str(),       // 時間評価5
			r.GetValue(offset + 13).Str(),       // 脚色
			r.GetValue(offset + 14).Str(),       // 一言
			r.GetValue(offset + 15).Str()        // 評価
		)
		{ }

		public Oikiri(Dictionary<string, object> x) : this(
			x["コース"].Str(),
			x["馬場"].Str(),
			x["乗り役"].Str(),
			x["時間1"].Single(),
			x["時間2"].Single(),
			x["時間3"].Single(),
			x["時間4"].Single(),
			x["時間5"].Single(),
			x["時間評価1"].Str(),
			x["時間評価2"].Str(),
			x["時間評価3"].Str(),
			x["時間評価4"].Str(),
			x["時間評価5"].Str(),
			x["脚色"].Str(),
			x["一言"].Str(),
			x["評価"].Str()
		)
		{ }

		public string Course { get; set; }
		public string Track { get; set; }
		public float Rider { get; set; }
		public float Time1 { get; set; }
		public float Time2 { get; set; }
		public float Time3 { get; set; }
		public float Time4 { get; set; }
		public float Time5 { get; set; }
		public float AdjustedTime5 => Time5.Run(x => 0 < x && x < 20 ? x : 13.5F);
		public float TimeRating1 { get; set; }
		public float TimeRating2 { get; set; }
		public float TimeRating3 { get; set; }
		public float TimeRating4 { get; set; }
		public float TimeRating5 { get; set; }
		public float TimeRating => (TimeRating5 + TimeRating4 + TimeRating3 + TimeRating2 + TimeRating1) / (1 + new[] { Time4, Time3, Time2, Time1 }.Count(x => x > 0F));

		public float Adaptation { get; set; }
		public string Comment { get; set; }
		public float Rating { get; set; }

		/// <summary>総合調教質スコア（複合指標）</summary>
		public float TotalScore
		{
			get
			{
				var weight = 0.0F;
				var score = 0.0F;

				// 調教強度スコア(一杯=5, Ｇ強=4, 強め=3, 馬也=2, 攻手=1, 欠損=0)
				weight += 0.2F;
				score += Adaptation * 0.2F;

				// 評価スコア(A=4, B=3, C=2, D=1, 欠損=2)
				weight += 0.3F;
				score += Rating * 0.3F;

				// 乗り役(0=助手, 1=騎手)
				weight += 0.2F;
				score += Rider * 0.2F;

				// 時計評価
				weight += 0.3F;
				score += TimeRating * 0.3F;

				// 時計
				weight += 0.4F;
				score += 5F / Math.Max(Time5 - 5F, 5F) * 0.4F;

				// 重みで正規化して0-1スケールに
				return score / weight;
			}
		}
	}
}