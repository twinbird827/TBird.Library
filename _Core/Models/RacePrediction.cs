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

		public RaceScore EV { get; } = new();

		public int Result { get; set; }

		public string Name { get; set; } = string.Empty;

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

		public static async Task<RacePrediction[]> PredictShutsubaAsync(string raceid)
		{
			// WPF と scheduler が同じレースを同時に予想すると、片方が挿入した出馬表をもう片方が削除してしまう
			using (await LockPredictAsync())
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var getShutsuba = false;

				MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データを取得します。");
				if (!await conn.ExistsOrigAsync(raceid))
				{
					var racearr = await NetkeibaGetter.GetRaceShutubas(raceid);
					await conn.BeginTransaction();
					await conn.InsertShutsubaAsync(racearr);
					await conn.InsertOikiriAsync(raceid);
					conn.Commit();
					getShutsuba = true;
					MessageService.Debug($"ﾚｰｽID：{raceid} の出馬表データが取得できました。");
				}

				try
				{
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

			// EV = 全モデルWinProbの中央値 × Odds
			results.ForEach(x =>
			{
				var probs = new[] { x.Vars1.WinProb, x.Vars2.WinProb, x.TotalMedium.WinProb, x.TotalSmall.WinProb, x.Total.WinProb, x.Horse.WinProb };
				Array.Sort(probs);
				x.EV.Score = (probs[2] + probs[3]) / 2f * x.Detail.Odds;
			});
			results.Where(x => !float.IsNaN(x.EV.Score)).OrderByDescending(x => x.EV.Score).ForEach((x, i) => x.EV.Rank = i + 1);

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
	}

	public class RaceScore
	{
		public float Score { get; set; }

		public int Rank { get; set; }

		public float WinProb { get; set; }
	}
}