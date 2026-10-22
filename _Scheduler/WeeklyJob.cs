using TBird.Core;

namespace Netkeiba.Scheduler
{
	public static class WeeklyJob
	{
		public static async Task<int> RunAsync()
		{
			var step = "STEP1";
			Models.RankingTrain[] trains;

			try
			{
				// 作り直しは全期間の取得になり無人では終わらないため、WPF からの手動に任せる
				if (!File.Exists(AppUtil.Sqlitepath)) throw new FileNotFoundException("データベースがありません。", AppUtil.Sqlitepath);

				await STEP1Updater.UpdateAsync(false, DateTime.Now.Year, new Progress<double>());

				step = "STEP2";
				await STEP2Generator.GenerateAsync(false);

				step = "STEP3";
				trains = await STEP3Trainer.TrainAsync();
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				await DiscordWebhook.SendAsync($"weekly {step} が失敗しました: {ex.Message}");
				return 1;
			}

			await DiscordWebhook.SendAsync("weekly が完了しました\n" + string.Join("\n", trains.Select(x => $"{x.Grade} NDCG@1 {x.NDCG1:F4}")));
			return 0;
		}
	}
}
