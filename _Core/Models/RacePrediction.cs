using Microsoft.ML;
using Microsoft.ML.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;

namespace Netkeiba.Models
{
	public class RacePrediction : TBirdObject
	{
		private RacePrediction(RaceDetail detail, float score, RaceConfidence confidence)
		{
			Detail = detail;
			Win.Score = score;
			Confidence = confidence;
		}

		public RaceDetail Detail { get; }

		/// <summary>単一モデルのスコア・順位・勝率</summary>
		public RaceScore Win { get; } = new();

		/// <summary>2 着以内確率（p1 + P2）</summary>
		public float Top2Prob { get; private set; }

		/// <summary>3 着以内確率（p1 + P2 + P3）</summary>
		public float Top3Prob { get; private set; }

		/// <summary>EV = 勝率 × 単勝</summary>
		public RaceScore EV { get; } = new();

		/// <summary>レースの自信度（同じレースの全馬で同じ値）</summary>
		public RaceConfidence Confidence { get; }

		public int Result { get; set; }

		public string Name { get; set; } = string.Empty;

		public static void Initialize(MLContext ml)
		{
			_train = AppSetting.Instance.GetRankingTrain(STEP3Trainer.ModelName);
			_model = LoadModel(ml, _train);
		}

		public static async Task<RacePrediction[]> PredictShutsubaAsync(string raceid)
		{
			// WPF と scheduler が同じレースを同時に予想すると、片方が挿入した出馬表をもう片方が削除してしまう
			using (await LockPredictAsync())
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var getShutsuba = false;
				var racearr = new List<Dictionary<string, string>>();

				MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データを取得します。");
				if (!await conn.ExistsOrigAsync(raceid))
				{
					racearr = await NetkeibaGetter.GetRaceShutubas(raceid);
					await conn.BeginTransaction();
					await conn.InsertShutsubaAsync(racearr);
					await conn.InsertOikiriAsync(raceid);
					conn.Commit();
					getShutsuba = true;
					MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データが取得できました。");
				}

				try
				{
					// 発走 10 分前（scheduler の予想時刻）のオッズは予想後に消えるので、API から取った回だけ残す
					if (getShutsuba)
					{
						var odds = racearr
							.Select(x => (umaban: x["馬番"].Int32(), odds: x["単勝"].GetSingle()))
							.Where(x => 0 < x.odds)
							.ToArray();
						await conn.BeginTransaction();
						await conn.InsertOddsAsync(raceid, odds, DateTime.Now);
						conn.Commit();
					}

					var ml = new MLContext(seed: 1);

					Initialize(ml);

					MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データをデータベースから取得します。");
					var race = await conn.GetShutsubaRaceAsync(raceid).FirstOrDefaultAsync();
					if (race == null) return Array.Empty<RacePrediction>();

					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();
					var tcd = PreviousDataSets.GetTrackConditionDistances(race);

					MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データがデータベースから取得できました。");

					details.ForEach(x => x.SetHistoricalData(PreviousDataSets.GetHorses(x), details, tcd));

					MessageService.Debug($"ﾚｰｽID：{raceid} の関連情報を取得しました。");

					var features = details.Select(x => x.ExtractFeatures(details));

					var inraces = features.CalculateInRaces();

					MessageService.Debug($"ﾚｰｽID：{raceid} の特徴量を作成しました。");

					var predictions = CalculatePrediction(ml, details, inraces).ToArray();

					MessageService.Debug($"ﾚｰｽID：{raceid} のスコアを計算しました。");

					if (race.RaceDate < DateTime.Now)
					{
						var tya = await NetkeibaGetter.GetTyakujun(race.RaceId);

						predictions.ForEach(p =>
						{
							p.Result = tya
								.Where(x => x["馬番"].Int32() == p.Detail.Umaban)
								.Select(x => x["着順"].Int32())
								.FirstOrDefault();
						});
					}

					foreach (var p in predictions)
					{
						p.Name = (await conn.ExecuteScalarAsync($"SELECT 馬名 FROM t_uma WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.String, p.Detail.Horse))).Str();
					}

					MessageService.Debug($"ﾚｰｽID：{raceid} の処理が完了しました。");

					return predictions;
				}
				finally
				{
					if (getShutsuba)
					{
						await conn.BeginTransaction();
						await conn.DeleteOrigAsync(raceid);
						conn.Commit();
					}
				}
			}
		}

		// 名前付き Mutex はスレッド所有で await を跨げず、ファイルロックはプロセスが落ちても OS が解放する
		private static async Task<FileStream> LockPredictAsync()
		{
			while (true)
			{
				try
				{
					return new FileStream(AppUtil.Sqlitepath + ".predict.lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
				}
				catch (IOException)
				{
					await Task.Delay(1000);
				}
			}
		}

		public static IEnumerable<RacePrediction> CalculatePrediction(MLContext ml, RaceDetail[] details, OptimizedHorseFeatures[] features)
		{
			var scores = GetScores(_model, ml.Data.LoadFromEnumerable(features));
			var p1 = RacePredictionExtension.Softmax(scores, _train.Temperature);
			var (p2, p3) = RacePredictionExtension.Harville(p1);
			var confidence = RaceConfidence.Get(RacePredictionExtension.GetConfidenceBand(p1.Max(), _train.ConfidenceBounds));

			var results = details
				.Select((detail, i) => new RacePrediction(detail, scores[i], confidence)
				{
					Top2Prob = (float)(p1[i] + p2[i]),
					Top3Prob = (float)(p1[i] + p2[i] + p3[i]),
				})
				.ToArray();
			results.ForEach((x, i) => x.Win.WinProb = (float)p1[i]);
			results.CalculateRank(x => x.Win);

			results.ForEach(x => x.EV.Score = x.Win.WinProb * x.Detail.Odds);
			results.Where(x => !float.IsNaN(x.EV.Score)).OrderByDescending(x => x.EV.Score).ForEach((x, i) => x.EV.Rank = i + 1);

			return results.OrderBy(x => x.Detail.Umaban);
		}

		private static RankingTrain _train = RankingTrain.Default;

		private static ITransformer? _model;

		private static ITransformer LoadModel(MLContext ml, RankingTrain train)
		{
			using var stream = new FileStream(train.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
			return ml.Model.Load(stream, out var schema);
		}

		private static float[] GetScores(ITransformer? mo, IDataView view)
		{
			if (mo == null) throw new InvalidOperationException("予想モデルが読み込まれていません。");
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

	/// <summary>レースの自信度の帯（1 低〜5 高）と、その帯の過去の 1 着的中率・3 着以内率</summary>
	public record RaceConfidence(int Band, double WinRate, double Top3Rate)
	{
		// 研究の選択用 fold 4 fold 合算（netkeiba-feature-review.md 9 節）
		private static readonly RaceConfidence[] _bands =
		[
			new(1, 0.192, 0.446),
			new(2, 0.246, 0.567),
			new(3, 0.312, 0.630),
			new(4, 0.366, 0.708),
			new(5, 0.460, 0.791),
		];

		public static RaceConfidence Get(int band) => _bands[band - 1];

		public override string ToString() => $"自信度 {Band}（1着 {WinRate:P0} / 3着内 {Top3Rate:P0}）";
	}
}
