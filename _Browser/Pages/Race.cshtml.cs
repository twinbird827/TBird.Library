using Browser.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Netkeiba;
using Netkeiba.Models;
using TBird.Core;

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

		// PreviousDataSets と RacePrediction の static 状態・同じ raceid の出馬表挿入をリクエスト間で交差させない
		private static readonly SemaphoreSlim _lock = new(1, 1);

		private static DateTime _initializedDate;

		private async Task ExecuteSTEP4(string raceid)
		{
			RacePrediction[] predictions;

			using (await _lock.LockAsync())
			{
				// 過去データの初期化は約 20 秒かかるため、基準日が変わったときだけ作り直す
				var date = DateTime.Now.AddDays(-4).Date;
				if (_initializedDate != date)
				{
					using (var conn = AppUtil.CreateSQLiteControl())
					{
						await PreviousDataSets.Initialize(conn, DateTime.Now.AddDays(-4));
					}
					_initializedDate = date;
				}

				predictions = await RacePrediction.PredictShutsubaAsync(raceid);
			}

			if (!predictions.Any()) return;

			var race = predictions[0].Detail.Race;

			RaceHeader = $"[{race.RaceId}] [{race.Place}] [R{race.RaceId.Right(2)}] [{race.Grade}] {race.CourseName}";

			// 結果を設定
			Results = predictions.Select(x =>
			{
				var probs = new[] { x.Vars1.WinProb, x.Vars2.WinProb, x.TotalMedium.WinProb, x.TotalSmall.WinProb, x.Total.WinProb, x.Horse.WinProb };
				Array.Sort(probs);
				var medianProb = (probs[2] + probs[3]) / 2f;

				return new RaceResultItem
				{
					Wakuban = x.Detail.Wakuban,
					Umaban = x.Detail.Umaban,
					Name = x.Name,
					Result = x.Result.Str(),
					TotalWinProb = x.Total.WinProb,
					TotalRank = x.Total.Rank,
					HorseWinProb = x.Horse.WinProb,
					HorseRank = x.Horse.Rank,
					TotalMediumWinProb = x.TotalMedium.WinProb,
					TotalMediumRank = x.TotalMedium.Rank,
					TotalSmallWinProb = x.TotalSmall.WinProb,
					TotalSmallRank = x.TotalSmall.Rank,
					Vars2WinProb = x.Vars2.WinProb,
					Vars2Rank = x.Vars2.Rank,
					Vars1WinProb = x.Vars1.WinProb,
					Vars1Rank = x.Vars1.Rank,
					Odds = x.Detail.Odds,
					EV = medianProb * x.Detail.Odds
				};
			}).ToArray();
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
		public float TotalWinProb { get; set; }
		public int TotalRank { get; set; }
		public float HorseWinProb { get; set; }
		public int HorseRank { get; set; }
		public float TotalMediumWinProb { get; set; }
		public int TotalMediumRank { get; set; }
		public float TotalSmallWinProb { get; set; }
		public int TotalSmallRank { get; set; }
		public float Vars2WinProb { get; set; }
		public int Vars2Rank { get; set; }
		public float Vars1WinProb { get; set; }
		public int Vars1Rank { get; set; }
		public float Odds { get; set; }
		public float EV { get; set; }
	}
}