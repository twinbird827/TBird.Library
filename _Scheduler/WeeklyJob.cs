using TBird.Core;

namespace Netkeiba.Scheduler
{
	public static class WeeklyJob
	{
		public static async Task<int> RunAsync()
		{
			var step = "STEP1";
			Models.RankingTrain[] trains;
			int failed;

			try
			{
				// 作り直しは全期間の取得になり無人では終わらないため、WPF からの手動に任せる
				if (!File.Exists(AppUtil.Sqlitepath)) throw new FileNotFoundException("データベースがありません。", AppUtil.Sqlitepath);

				await STEP1Updater.UpdateAsync(false, DateTime.Now.Year, new Progress<double>());

				step = "STEP2";
				failed = await STEP2Generator.GenerateAsync(false);

				step = "STEP3";
				trains = await STEP3Trainer.TrainAsync();
			}
			catch (Exception ex)
			{
				MessageService.Exception(ex);
				await DiscordWebhook.SendAsync($"weekly {step} が失敗しました: {ex.Message}");
				return 1;
			}

			await DiscordWebhook.SendAsync("weekly が完了しました\n"
				+ (failed > 0 ? $"STEP2 失敗 {failed} 件\n" : string.Empty)
				+ string.Join("\n", trains.Select(x => $"{x.Grade} NDCG@1 {x.NDCG1:F4}")));
			return 0;
		}
	}
}
