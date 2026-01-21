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
using TBird.Wpf;
using Tensorflow;

namespace Netkeiba.Models
{
	public class RacePrediction : TBirdObject
	{
		private RacePrediction(RaceDetail detail, float all, float horse, float jockey, float blood, float connection, OptimizedHorseFeatures features)
		{
			Detail = detail;
			All.Score = all;
			Horse.Score = horse;
			Jockey.Score = jockey;
			Blood.Score = blood;
			Connection.Score = connection;
			Total.Score = Arr(
				GetScore(All.Score, _key[0]),
				GetScore(Horse.Score, _key[1]),
				GetScore(Jockey.Score, _key[2]),
				GetScore(Blood.Score, _key[3]),
				GetScore(Connection.Score, _key[4])
			).Average();
		}

		public RaceDetail Detail { get; }

		public RaceScore All { get; } = new();

		public RaceScore Horse { get; } = new();

		public RaceScore Jockey { get; } = new();

		public RaceScore Blood { get; } = new();

		public RaceScore Connection { get; } = new();

		public RaceScore Total { get; } = new();

		public int Result { get; set; }

		public static void Initialize(MLContext ml)
		{
			_key = new[]
			{
				AppSetting.Instance.GetRankingTrain(FeaturesType.All.GetLabel()),
				AppSetting.Instance.GetRankingTrain(FeaturesType.Horse.GetLabel()),
				AppSetting.Instance.GetRankingTrain(FeaturesType.Jockey.GetLabel()),
				AppSetting.Instance.GetRankingTrain(FeaturesType.Blood.GetLabel()),
				AppSetting.Instance.GetRankingTrain(FeaturesType.Connection.GetLabel()),
			};

			_dic = _key.ToDictionary(key => key, key => LoadModel(ml, key));
		}

		public static IEnumerable<RacePrediction> CalculatePrediction(MLContext ml, RaceDetail[] details, OptimizedHorseFeatures[] features)
		{
			// ｽｺｱ計算
			var view = ml.Data.LoadFromEnumerable(features);
			var alls = GetScores(_dic[_key[0]], view);
			var hrse = GetScores(_dic[_key[1]], view);
			var jock = GetScores(_dic[_key[2]], view);
			var blod = GetScores(_dic[_key[3]], view);
			var conn = GetScores(_dic[_key[4]], view);

			var results = details
				.Select((detail, i) => new RacePrediction(detail, alls[i], hrse[i], jock[i], blod[i], conn[i], features[i]))
				.ToList();

			results.OrderByDescending(x => x.All.Score).ForEach((x, i) =>
				x.All.Rank = i + 1
			);
			results.OrderByDescending(x => x.Horse.Score).ForEach((x, i) =>
				x.Horse.Rank = i + 1
			);
			results.OrderByDescending(x => x.Jockey.Score).ForEach((x, i) =>
				x.Jockey.Rank = i + 1
			);
			results.OrderByDescending(x => x.Blood.Score).ForEach((x, i) =>
				x.Blood.Rank = i + 1
			);
			results.OrderByDescending(x => x.Connection.Score).ForEach((x, i) =>
				x.Connection.Rank = i + 1
			);
			results.OrderByDescending(x => x.Total.Score).ForEach((x, i) =>
				x.Total.Rank = i + 1
			);

			return results.OrderBy(x => x.Detail.Umaban);
		}

		private static RankingTrain[] _key;

		private static Dictionary<RankingTrain, ITransformer> _dic = new();

		private static ITransformer LoadModel(MLContext ml, RankingTrain train)
		{
			using var stream = new FileStream(train.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return ml.Model.Load(stream, out var schema);
		}

		private static float[] GetScores(ITransformer mo, IDataView view)
		{
			var predictions = mo.Transform(view);
			var scores = predictions.GetColumn<float>("Score").ToArray();
			return scores;
		}

		private static float GetScore(float score, RankingTrain train) => score * (float)(train.NDCG1 + train.NDCG3 / 2 + train.NDCG5 / 3);
	}

	public class RaceScore : BindableBase
	{
		public float Score
		{
			get => _Score;
			set => SetProperty(ref _Score, value);
		}
		private float _Score;

		public int Rank
		{
			get => _Rank;
			set => SetProperty(ref _Rank, value);
		}
		private int _Rank;

	}
}