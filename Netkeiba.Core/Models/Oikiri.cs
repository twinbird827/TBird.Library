using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class Oikiri
	{
		public Oikiri(Dictionary<string, object> x, RaceDetail detail)
		{
			Detail = detail;
			Course = x["コース"].Str();
			Track = x["馬場"].Str();
			Rider = x["乗り役"].Str().Run(x => x == "助手" ? 0F : 1F);
			Time1 = x["時間1"].Single();
			Time2 = x["時間2"].Single();
			Time3 = x["時間3"].Single();
			Time4 = x["時間4"].Single();
			Time5 = x["時間5"].Single();

			float GetTimeRating(string s) => x[s].Str().Run(x => x switch
			{
				"TokeiColor01" => 1.0F,
				"TokeiColor02" => 0.5F,
				_ => 0.0F
			});

			TimeRating1 = GetTimeRating("時間評価1");
			TimeRating2 = GetTimeRating("時間評価2");
			TimeRating3 = GetTimeRating("時間評価3");
			TimeRating4 = GetTimeRating("時間評価4");
			TimeRating5 = GetTimeRating("時間評価5");

			Adaptation = x["脚色"].Str().Run(x => x switch
			{
				"一杯" => 1.00F,
				"Ｇ強" => 0.80F,
				"強め" => 0.60F,
				"馬也" => 0.40F,
				"攻手" => 0.20F,
				_ => 0.00F
			});

			Comment = x["一言"].Str();
			Rating = x["評価"].Str().Run(x => x switch
			{
				"A" => 1.00F,
				"B" => 0.65F,
				"C" => 0.40F,
				"D" => 0.00F,
				_ => 0.30F
			});
		}

		public RaceDetail Detail { get; }
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