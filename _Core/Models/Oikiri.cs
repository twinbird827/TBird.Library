using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class Oikiri
	{
		private Oikiri(string rider,
			float time1, float time2, float time3, float time4, float time5,
			string timeRating1, string timeRating2, string timeRating3, string timeRating4, string timeRating5,
			string adaptation, string rating)
		{
			// 値は学習期間の調教師補正着順から決めたもの。既定分岐は騎手名を受ける
			Rider = rider switch
			{
				"師" => 1.00F,
				"攻手" => 0.81F,
				"助手" => 0.69F,
				"見習" => 0.13F,
				"" => 0.00F,
				_ => 0.58F
			};
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

			// 値は強度順でなく学習期間の調教師補正着順から決めたもの。強度順に並べ直さない
			Adaptation = adaptation.Normalize(NormalizationForm.FormKC) switch
			{
				"直強" => 1.00F,
				"仕掛" => 0.91F,
				"馬也" => 0.90F,
				"G強" => 0.86F,
				"G一" => 0.63F,
				"直一" => 0.57F,
				"強め" => 0.48F,
				"一杯" => 0.18F,
				_ => 0.00F
			};

			Rating = rating switch
			{
				"A" => 1.00F,
				"B" => 0.65F,
				"C" => 0.40F,
				"D" => 0.00F,
				_ => float.NaN
			};
		}

		public Oikiri(DbDataReader r, int offset) : this(
			r.GetValue(offset + 2).Str(),        // 乗り役
			ToTime(r.GetValue(offset + 3).Str()), // 時間1
			ToTime(r.GetValue(offset + 4).Str()), // 時間2
			ToTime(r.GetValue(offset + 5).Str()), // 時間3
			ToTime(r.GetValue(offset + 6).Str()), // 時間4
			ToTime(r.GetValue(offset + 7).Str()), // 時間5
			r.GetValue(offset + 8).Str(),        // 時間評価1
			r.GetValue(offset + 9).Str(),        // 時間評価2
			r.GetValue(offset + 10).Str(),       // 時間評価3
			r.GetValue(offset + 11).Str(),       // 時間評価4
			r.GetValue(offset + 12).Str(),       // 時間評価5
			r.GetValue(offset + 13).Str(),       // 脚色
			r.GetValue(offset + 15).Str()        // 評価
		)
		{ }

		public Oikiri(Dictionary<string, object> x) : this(
			x["乗り役"].Str(),
			ToTime(x["時間1"].Str()),
			ToTime(x["時間2"].Str()),
			ToTime(x["時間3"].Str()),
			ToTime(x["時間4"].Str()),
			ToTime(x["時間5"].Str()),
			x["時間評価1"].Str(),
			x["時間評価2"].Str(),
			x["時間評価3"].Str(),
			x["時間評価4"].Str(),
			x["時間評価5"].Str(),
			x["脚色"].Str(),
			x["評価"].Str()
		)
		{ }

		// 時計はすべて小数 1 桁。`?12.9` の印や `102.751.7` のようにつながった値は、列に合う後ろ側を採る
		private static float ToTime(string s) => Regex.Matches(s, @"\d+\.\d").LastOrDefault()?.Value is string v
			? float.Parse(v, CultureInfo.InvariantCulture)
			: float.NaN;

		public float Rider { get; set; }
		public float Time1 { get; set; }
		public float Time2 { get; set; }
		public float Time3 { get; set; }
		public float Time4 { get; set; }
		public float Time5 { get; set; }
		public float AdjustedTime5 => Time5.Run(x => 0 < x && x < 20 ? x : float.NaN);
		public float TimeRating1 { get; set; }
		public float TimeRating2 { get; set; }
		public float TimeRating3 { get; set; }
		public float TimeRating4 { get; set; }
		public float TimeRating5 { get; set; }
		public float TimeRating => (TimeRating5 + TimeRating4 + TimeRating3 + TimeRating2 + TimeRating1) / (1 + new[] { Time4, Time3, Time2, Time1 }.Count(x => x > 0F));

		public float Adaptation { get; set; }
		public float Rating { get; set; }

		/// <summary>総合調教質スコア（複合指標）</summary>
		public float TotalScore
		{
			get
			{
				var weight = 0.0F;
				var score = 0.0F;

				weight += 0.2F;
				score += Adaptation * 0.2F;

				// 評価（欠損は NaN が全体を NaN にするので外す）
				if (!float.IsNaN(Rating))
				{
					weight += 0.3F;
					score += Rating * 0.3F;
				}

				weight += 0.2F;
				score += Rider * 0.2F;

				// 時計評価
				weight += 0.3F;
				score += TimeRating * 0.3F;

				// 時計（欠損は Math.Max が NaN を返して全体を NaN にするので外す）
				if (!float.IsNaN(AdjustedTime5))
				{
					weight += 0.4F;
					score += 5F / Math.Max(AdjustedTime5 - 5F, 5F) * 0.4F;
				}

				// 重みで正規化して0-1スケールに
				return score / weight;
			}
		}
	}
}