using Browser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.ML;
using Netkeiba;
using Netkeiba.Models;
using TBird.Core;
using TBird.DB.SQLite;

namespace Browser.Pages
{
	public class RaceModel : PageModel
	{
		public RaceModel(ILogger<RaceModel> logger)
		{
			MessageService.SetService(new RazorMessageService(logger));
		}

		public async Task<IActionResult> OnGetAsync(string id)
		{
			if (string.IsNullOrEmpty(id))
			{
				return NotFound();
			}

			RaceId = id;

			try
			{
				// STEP4処理を実行
				await ExecuteSTEP4(id);
				return Page();
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				ErrorMessage = $"エラーが発生しました: {ex.Message}";
				return Page();
			}
		}

		private async Task ExecuteSTEP4(string raceid)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				var getShutsuba = false;

				var ini = InitializePreviousDataSets(conn);

				// 該当レースの出馬表を取得する
				MessageService.Debug($"レースID：{raceid} の出馬表データを取得します。");
				await conn.BeginTransaction();
				await foreach (var racearr in GetSTEP4Racearrs(conn, raceid))
				{
					await conn.InsertShutsubaAsync(racearr);
					await conn.InsertOikiriAsync(raceid);
					getShutsuba = true;
					MessageService.Debug($"レースID：{raceid} の出馬表データが取得できました。");
				}
				conn.Commit();

				await ini;

				var ml = new MLContext(seed: 1);

				RacePrediction.Initialize(ml);

				// 出馬表からレースデータを作成する
				MessageService.Debug($"レースID：{raceid} の出馬表データをデータベースから取得します。");
				await foreach (var race in conn.GetShutsubaRaceAsync(raceid))
				{
					// 今レースの情報を取得する
					var details = conn.GetRaceDetailsAsync(race).ToBlockingEnumerable().ToArray();

					MessageService.Debug($"レースID：{raceid} の出馬表データがデータベースから取得できました。");

					// 過去データ設定
					details.ForEach(x => x.SetHistoricalData(PreviousDataSets.GetHorses(x), details, PreviousDataSets.GetTrackConditionDistances(x)));

					MessageService.Debug($"レースID：{raceid} の関連情報を取得しました。");

					// 今レースのレーティング情報をセットする
					race.AverageRating = details.Average(x => x.AverageRating);

					// 特徴量を生成
					var features = details.Select(x =>
					{
						var value = x.ExtractFeatures(details);

						// ラベル生成（難易度調整済み着順スコア）
						value.Label = 0;

						return value;
					}).CalculateInRaces();

					MessageService.Debug($"レースID：{raceid} の特徴量を作成しました。");

					// スコア計算
					var predictions = RacePrediction.CalculatePrediction(ml, details, features);

					MessageService.Debug($"レースID：{raceid} のスコアを計算しました。");

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

					RaceHeader = $"[{race.RaceId}] [{race.Place}] [R{race.RaceId.Right(2)}] [{race.Grade}] {race.CourseName}";

					// 結果を設定
					Results = await predictions.Select(async x =>
					{
						var name = await conn.ExecuteScalarAsync($"SELECT 馬名 FROM t_uma WHERE 馬ID = ?", TBird.DB.SQLite.SQLiteUtil.CreateParameter(System.Data.DbType.String, x.Detail.Horse));

						return new RaceResultItem
						{
							Wakuban = x.Detail.Wakuban,
							Umaban = x.Detail.Umaban,
							Name = name.Str(),
							Result = x.Result.Str(),
							TotalScore = x.Total.Score,
							TotalRank = x.Total.Rank,
							HorseScore = x.Horse.Score,
							HorseRank = x.Horse.Rank,
							TotalMediumScore = x.TotalMedium.Score,
							TotalMediumRank = x.TotalMedium.Rank,
							TotalSmallScore = x.TotalSmall.Score,
							TotalSmallRank = x.TotalSmall.Rank,
							Vars2Score = x.Vars2.Score,
							Vars2Rank = x.Vars2.Rank,
							Vars1Score = x.Vars1.Score,
							Vars1Rank = x.Vars1.Rank
						};
					}).WhenAll();

					MessageService.Debug($"レースID：{raceid} の処理が完了しました。");
				}

				if (getShutsuba)
				{
					await conn.BeginTransaction();
					await conn.DeleteOrigAsync(raceid);
					conn.Commit();
				}
			}
		}

		private async Task InitializePreviousDataSets(SQLiteControl conn)
		{
			await PreviousDataSets.Initialize(conn, DateTime.Now.AddDays(-4));
		}

		private async IAsyncEnumerable<List<Dictionary<string, string>>> GetSTEP4Racearrs(SQLiteControl conn, string raceid)
		{
			if (!await conn.ExistsOrigAsync(raceid))
			{
				var arr = await NetkeibaGetter.GetRaceShutubas(raceid);

				if (arr.Any(x => x["回り"] != "障" && string.IsNullOrEmpty(x["ﾀｲﾑ指数"]))) yield break;

				yield return arr;
			}
		}

		public string RaceId { get; set; } = string.Empty;
		public string RaceHeader { get; set; } = string.Empty;
		public IEnumerable<RaceResultItem> Results { get; set; } = new List<RaceResultItem>();
		public string? ErrorMessage { get; set; }
	}

	public class RaceResultItem
	{
		public int Wakuban { get; set; }
		public int Umaban { get; set; }
		public string Name { get; set; } = string.Empty;
		public string Result { get; set; } = string.Empty;
		public float TotalScore { get; set; }
		public int TotalRank { get; set; }
		public float HorseScore { get; set; }
		public int HorseRank { get; set; }
		public float TotalMediumScore { get; set; }
		public int TotalMediumRank { get; set; }
		public float TotalSmallScore { get; set; }
		public int TotalSmallRank { get; set; }
		public float Vars2Score { get; set; }
		public int Vars2Rank { get; set; }
		public float Vars1Score { get; set; }
		public int Vars1Rank { get; set; }
	}
}