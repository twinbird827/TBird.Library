using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Netkeiba.Models
{
	public class RacePrediction
	{
		public RacePrediction(RaceDetail detail, float score, OptimizedHorseFeatures features)
		{
			Detail = detail;
			Score = score;
			Confidence = CalculateConfidence(detail, features);
		}

		public RaceDetail Detail { get; }

		public float Score { get; }

		public float Confidence { get; }

		public int Rank { get; private set; }

		public int Result { get; set; }

		private float CalculateConfidence(RaceDetail detail, OptimizedHorseFeatures features)
		{
			float confidence = 0.5f; // ベースライン

			// 経験による信頼度
			if (detail.RaceCount >= 5)
				confidence += 0.2f;
			else if (detail.RaceCount >= 2)
				confidence += 0.1f;

			// 条件適性の信頼度
			confidence += features.AptitudeReliability * 0.2f;

			// 最近の実績による信頼度
			if (features.Recent3AdjustedAvg > 0.3f)
				confidence += 0.1f;

			return Math.Min(confidence, 1.0f);
		}

		public static IEnumerable<RacePrediction> CalculatePrediction(MLContext ml, ITransformer mo, RaceDetail[] details, OptimizedHorseFeatures[] features)
		{
			// ｽｺｱ計算
			var view = ml.Data.LoadFromEnumerable(features);
			var predictions = mo.Transform(view);
			var scores = predictions.GetColumn<float>("Score").ToArray();

			var results = details
				.Select((detail, i) => new RacePrediction(detail, scores[i], features[i]))
				.OrderByDescending(p => p.Score)
				.ToList();

			for (int i = 0; i < results.Count; i++)
			{
				results[i].Rank = i + 1;
			}

			return results.OrderBy(x => x.Detail.Umaban);
		}
	}
}