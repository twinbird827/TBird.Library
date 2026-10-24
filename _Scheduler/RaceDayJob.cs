using Netkeiba.Models;
using TBird.Core;

namespace Netkeiba.Scheduler
{
	public static class RaceDayJob
	{
		public static async Task<int> RunAsync()
		{
			var today = DateTime.Today;
			var schedules = (await NetkeibaGetter.GetRaceSchedules(today)).OrderBy(x => x.StartTime).ToArray();
			if (!schedules.Any())
			{
				MessageService.Debug("非開催日のため終了します。");
				return 0;
			}

			// weekly の STEP3 が失敗するとモデルが消えており、全レースが失敗する
			var missing = STEP3Trainer.ModelParams.Keys
				.Select(x => AppSetting.Instance.GetRankingTrain(x.GetLabel()).Run(t => (grade: x.GetLabel(), t)))
				.Where(x => x.t == RankingTrain.Default || !File.Exists(x.t.Path))
				.Select(x => x.grade)
				.ToArray();
			if (missing.Any())
			{
				var message = $"raceday: モデルがありません: {string.Join(", ", missing)}";
				MessageService.Error(message);
				await DiscordWebhook.SendAsync(message);
				return 1;
			}

			await InitializeAsync(today);

			foreach (var s in schedules)
			{
				await WaitUntilAsync(s.StartTime.AddMinutes(-10));
				if (s.StartTime <= DateTime.Now) continue;

				await TryAsync(s.RaceId, () => SendRaceAsync(s.RaceId, s.StartTime));
			}

			return 0;
		}

		public static async Task<int> PredictAsync(string[] raceids)
		{
			var today = DateTime.Today;
			var schedules = await NetkeibaGetter.GetRaceSchedules(today);

			await InitializeAsync(today);

			foreach (var raceid in raceids)
			{
				var start = schedules.Where(x => x.RaceId == raceid).Select(x => (DateTime?)x.StartTime).FirstOrDefault();
				await TryAsync(raceid, () => SendRaceAsync(raceid, start));
			}
			return 0;
		}

		private static async Task InitializeAsync(DateTime today)
		{
			using (var conn = AppUtil.CreateSQLiteControl())
			{
				await PreviousDataSets.Initialize(conn, today.AddDays(-3));
			}
		}

		private static async Task SendRaceAsync(string raceid, DateTime? start)
		{
			var predictions = await RacePrediction.PredictShutsubaAsync(raceid);
			if (!predictions.Any()) throw new InvalidOperationException("出馬表が取得できません。");
			var image = RaceTableImage.Create(predictions, start);
			await DiscordWebhook.SendAsync(image.Header, ($"{raceid}.png", image.Png));
		}

		// 1 件の失敗で残りのレースを止めない
		private static async Task TryAsync(string target, Func<Task> action)
		{
			try
			{
				await action();
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				try
				{
					await DiscordWebhook.SendAsync($"{target} が失敗しました: {ex.Message}");
				}
				catch (Exception ex2)
				{
					MessageService.Exception(ex2);
				}
			}
		}

		// 長い 1 回の Task.Delay はスリープ復帰後に予定時刻を過ぎても発火しないため、1 分以下ずつ時刻を見直す
		private static async Task WaitUntilAsync(DateTime at)
		{
			for (var rest = at - DateTime.Now; TimeSpan.Zero < rest; rest = at - DateTime.Now)
			{
				await Task.Delay(rest < TimeSpan.FromMinutes(1) ? rest : TimeSpan.FromMinutes(1));
			}
		}
	}
}
