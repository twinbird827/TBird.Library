using Netkeiba;
using Netkeiba.Scheduler;
using TBird.Core;

MessageService.SetService(new SchedulerMessageService());

if (string.IsNullOrEmpty(AppSetting.Instance.DiscordWebhookUrl))
{
	MessageService.Error("app-setting.json の DiscordWebhookUrl が未設定です。");
	return 1;
}

try
{
	return args.FirstOrDefault() switch
	{
		"weekly" => await WeeklyJob.RunAsync(),
		"raceday" => await RaceDayJob.RunAsync(),
		"predict" when 1 < args.Length => await RaceDayJob.PredictAsync(args.Skip(1).ToArray()),
		_ => throw new ArgumentException($"引数は weekly / raceday / predict <raceid...> のいずれかです: {string.Join(" ", args)}"),
	};
}
catch (Exception ex)
{
	MessageService.Exception(ex);
	try
	{
		await DiscordWebhook.SendAsync($"scheduler {string.Join(" ", args)} が失敗しました: {ex.Message}");
	}
	catch (Exception ex2)
	{
		MessageService.Exception(ex2);
	}
	return 1;
}
