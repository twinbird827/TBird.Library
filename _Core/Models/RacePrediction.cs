using Microsoft.ML;
using Microsoft.ML.Data;
using OpenQA.Selenium.DevTools.V141.Overlay;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;

namespace Netkeiba.Models
{
	public class RacePrediction : TBirdObject
	{
		private RacePrediction(RaceDetail detail, float total, float horse, float connection, float totalMedium, float totalSmall, float totalRaw, float totalRank, OptimizedHorseFeatures features)
		{
			Detail = detail;
			Total.Score = total;
			Horse.Score = horse;
			TotalMedium.Score = totalMedium;
			TotalSmall.Score = totalSmall;

			// Vars2 = total_vars2: Horse+TotalMedium+TotalSmall+TotalRaw+TotalRank（NDCG1²加重平均）
			var wH = _key[1].NDCG1 * _key[1].NDCG1; // Horse
			var wM = _key[3].NDCG1 * _key[3].NDCG1; // TotalMedium
			var wS = _key[4].NDCG1 * _key[4].NDCG1; // TotalSmall
			var wR = _key[5].NDCG1 * _key[5].NDCG1; // TotalRaw
			var wK = _key[6].NDCG1 * _key[6].NDCG1; // TotalRank
			Vars2.Score = (float)((horse * wH + totalMedium * wM + totalSmall * wS + totalRaw * wR + totalRank * wK) / (wH + wM + wS + wR + wK));

			// Vars1 = total_vars: Total+Horse+TotalMedium+TotalSmall+Connection（NDCG1²加重平均）
			var wT = _key[0].NDCG1 * _key[0].NDCG1; // Total
			var wC = _key[2].NDCG1 * _key[2].NDCG1; // Connection
			Vars1.Score = (float)((total * wT + horse * wH + connection * wC + totalMedium * wM + totalSmall * wS) / (wT + wH + wC + wM + wS));
		}

		public RaceDetail Detail { get; }

		public RaceScore Total { get; } = new();

		public RaceScore Horse { get; } = new();

		public RaceScore TotalMedium { get; } = new();

		public RaceScore TotalSmall { get; } = new();

		public RaceScore Vars2 { get; } = new();

		public RaceScore Vars1 { get; } = new();

		public int Result { get; set; }

		public static void Initialize(MLContext ml)
		{
			_key = new[]
			{
				AppSetting.Instance.GetRankingTrain(FeaturesType.Total.GetLabel()),      // 0
				AppSetting.Instance.GetRankingTrain(FeaturesType.Horse.GetLabel()),      // 1
				AppSetting.Instance.GetRankingTrain(FeaturesType.Connection.GetLabel()), // 2
				AppSetting.Instance.GetRankingTrain(FeaturesType.TotalMedium.GetLabel()),// 3
				AppSetting.Instance.GetRankingTrain(FeaturesType.TotalSmall.GetLabel()), // 4
				AppSetting.Instance.GetRankingTrain(FeaturesType.TotalRaw.GetLabel()),   // 5
				AppSetting.Instance.GetRankingTrain(FeaturesType.TotalRank.GetLabel()),  // 6
			};

			_dic = _key.ToDictionary(key => key, key => LoadModel(ml, key));
		}

		public static IEnumerable<RacePrediction> CalculatePrediction(MLContext ml, RaceDetail[] details, OptimizedHorseFeatures[] features)
		{
			// ｽｺｱ計算
			var view = ml.Data.LoadFromEnumerable(features);
			var totl = GetScores(_dic[_key[0]], view);
			var hrse = GetScores(_dic[_key[1]], view);
			var conn = GetScores(_dic[_key[2]], view);
			var tmed = GetScores(_dic[_key[3]], view);
			var tsml = GetScores(_dic[_key[4]], view);
			var traw = GetScores(_dic[_key[5]], view);
			var trnk = GetScores(_dic[_key[6]], view);

			var results = details
				.Select((detail, i) => new RacePrediction(detail, totl[i], hrse[i], conn[i], tmed[i], tsml[i], traw[i], trnk[i], features[i]))
				.ToArray();

			results.CalculateRank(x => x.Total);
			results.CalculateRank(x => x.Horse);
			results.CalculateRank(x => x.TotalMedium);
			results.CalculateRank(x => x.TotalSmall);
			results.CalculateRank(x => x.Vars2);
			results.CalculateRank(x => x.Vars1);

			results.CalculateWinProb(x => x.Total, _key[0].Temperature);
			results.CalculateWinProb(x => x.Horse, _key[1].Temperature);
			results.CalculateWinProb(x => x.TotalMedium, _key[3].Temperature);
			results.CalculateWinProb(x => x.TotalSmall, _key[4].Temperature);
			results.CalculateWinProb(x => x.Vars2, _key[0].Temperature); // アンサンブルはTotal基準
			results.CalculateWinProb(x => x.Vars1, _key[0].Temperature); // アンサンブルはTotal基準

			return results.OrderBy(x => x.Detail.Umaban);
		}

		private static RankingTrain[] _key;

		private static Dictionary<RankingTrain, ITransformer> _dic = new();

		private static ITransformer LoadModel(MLContext ml, RankingTrain train)
		{
			using var stream = new FileStream(Path.Combine(PathSetting.Instance.RootDirectory, train.Path), FileMode.Open, FileAccess.Read, FileShare.Read);
			return ml.Model.Load(stream, out var schema);
		}

		private static float[] GetScores(ITransformer mo, IDataView view)
		{
			var predictions = mo.Transform(view);
			var scores = predictions.GetColumn<float>("Score").ToArray();
			return scores;
		}
	}

	public class RaceScore
	{
		public float Score { get; set; }

		public int Rank { get; set; }

		public float WinProb { get; set; }
	}

	public static class RaceScoreExtension
	{
		public static void CalculateRank(this RacePrediction[] results, Func<RacePrediction, RaceScore> func)
		{
			results.OrderByDescending(x => func(x).Score).ForEach((x, i) =>
				func(x).Rank = i + 1
			);
		}

		public static void CalculateWinProb(this RacePrediction[] results, Func<RacePrediction, RaceScore> func, double temperature)
		{
			var t = (float)temperature;
			var maxScore = results.Max(x => func(x).Score);
			var exps = results.Select(x => MathF.Exp((func(x).Score - maxScore) / t)).ToArray();
			var sumExp = exps.Sum();
			results.ForEach((x, i) => func(x).WinProb = exps[i] / sumExp);
		}
	}
}