using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace Netkeiba.Models
{
	public class Oikiri
	{
		public Oikiri(Dictionary<string, object> x, RaceDetail detail)
		{
			Detail = detail;
			Course = x["コース"].Str();
			Track = x["馬場"].Str();
			Rider = x["乗り役"].Str();
			Time1 = x["時間1"].Single();
			Time2 = x["時間2"].Single();
			Time3 = x["時間3"].Single();
			Time4 = x["時間4"].Single();
			Time5 = x["時間5"].Single();
			TimeRating1 = x["時間評価1"].Str();
			TimeRating2 = x["時間評価2"].Str();
			TimeRating3 = x["時間評価3"].Str();
			TimeRating4 = x["時間評価4"].Str();
			TimeRating5 = x["時間評価5"].Str();
			Adaptation = x["脚色"].Str();
			Comment = x["一言"].Str();
			Rating = x["評価"].Str();
		}

		public RaceDetail Detail { get; }
		public string Course { get; set; }
		public string Track { get; set; }
		public string Rider { get; set; }
		public float Time1 { get; set; }
		public float Time2 { get; set; }
		public float Time3 { get; set; }
		public float Time4 { get; set; }
		public float Time5 { get; set; }

		public string TimeRating1 { get; set; }
		public string TimeRating2 { get; set; }
		public string TimeRating3 { get; set; }
		public string TimeRating4 { get; set; }
		public string TimeRating5 { get; set; }
		public string Adaptation { get; set; }
		public string Comment { get; set; }
		public string Rating { get; set; }

		// === 優先度S: 調教特徴量 ===

		/// <summary>最終ラップタイム（欠損=0）</summary>
		public float Lap5Time => Time5;

		/// <summary>3Fラップタイム（欠損=0）</summary>
		public float Lap3Time => Time3;

		/// <summary>評価スコア（A=4, B=3, C=2, D=1, 欠損=2）</summary>
		public float EvaluationScore
		{
			get
			{
				if (string.IsNullOrEmpty(Rating)) return 2f; // デフォルトC相当

				return Rating switch
				{
					"A" => 4f,
					"B" => 3f,
					"C" => 2f,
					"D" => 1f,
					_ => 2f
				};
			}
		}

		/// <summary>TokeiColor総数（0-5）</summary>
		public float TokeiColorTotalCount
		{
			get
			{
				var ratings = new[] { TimeRating1, TimeRating2, TimeRating3, TimeRating4, TimeRating5 };
				return ratings.Count(x => x == "TokeiColor01" || x == "TokeiColor02");
			}
		}

		/// <summary>TokeiColor01の数（特に良いラップ）</summary>
		public int TokeiColor01Count
		{
			get
			{
				var ratings = new[] { TimeRating1, TimeRating2, TimeRating3, TimeRating4, TimeRating5 };
				return ratings.Count(x => x == "TokeiColor01");
			}
		}

		/// <summary>調教強度スコア（一杯=5, Ｇ強=4, 強め=3, 馬也=2, 攻手=1, 欠損=0）</summary>
		public float IntensityScore
		{
			get
			{
				if (string.IsNullOrEmpty(Adaptation)) return 0f;

				return Adaptation switch
				{
					"一杯" => 5f,
					"Ｇ強" => 4f,
					"強め" => 3f,
					"馬也" => 2f,
					"攻手" => 1f,
					_ => 0f
				};
			}
		}

		/// <summary>コメントポジティブスコア</summary>
		public float CommentPositiveScore
		{
			get
			{
				if (string.IsNullOrEmpty(Comment)) return 0f;

				var positiveWords = new[] { "上々", "良化", "仕上", "好気配", "軽快", "力強", "良好", "好調", "態勢整", "出来" };
				return positiveWords.Count(word => Comment.Contains(word));
			}
		}

		/// <summary>総合調教質スコア（複合指標）</summary>
		public float QualityScore
		{
			get
			{
				double score = 0.0;
				double weightSum = 0.0;

				// 評価がある場合のみ加算
				if (!string.IsNullOrEmpty(Rating))
				{
					score += EvaluationScore * 0.3;
					weightSum += 0.3;
				}

				// 脚色がある場合のみ加算
				if (!string.IsNullOrEmpty(Adaptation))
				{
					score += IntensityScore * 0.2;
					weightSum += 0.2;
				}

				// TokeiColorは常に計算可能
				score += TokeiColorTotalCount * 0.25;
				weightSum += 0.25;

				// コメントがある場合のみ加算
				if (!string.IsNullOrEmpty(Comment))
				{
					score += CommentPositiveScore * 0.15;
					weightSum += 0.15;
				}

				// 騎手騎乗フラグ（助手でない=1）
				float isJockeyRiding = string.IsNullOrEmpty(Rider) || Rider == "助手" ? 0f : 1f;
				score += isJockeyRiding * 0.1;
				weightSum += 0.1;

				// 重みで正規化して0-10スケールに
				return weightSum > 0 ? (float)(score / weightSum * 10) : 0f;
			}
		}
	}
}