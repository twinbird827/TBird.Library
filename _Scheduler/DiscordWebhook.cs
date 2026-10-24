using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TBird.Core;

namespace Netkeiba.Scheduler
{
	public static class DiscordWebhook
	{
		private static readonly HttpClient _client = new();

		public static async Task SendAsync(string content, params (string Name, byte[] Png)[] files)
		{
			for (var retried = false; ; retried = true)
			{
				using var form = new MultipartFormDataContent();
				// 本文は 2,000 字を超えると 400 で弾かれる
				form.Add(new StringContent(JsonSerializer.Serialize(new { content = content.Left(2000) }), Encoding.UTF8, "application/json"), "payload_json");
				for (var i = 0; i < files.Length; i++)
				{
					var file = new ByteArrayContent(files[i].Png);
					file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
					form.Add(file, $"files[{i}]", files[i].Name);
				}

				using var res = await _client.PostAsync(AppSetting.Instance.DiscordWebhookUrl, form);
				var body = await res.Content.ReadAsStringAsync();

				if (res.StatusCode == HttpStatusCode.TooManyRequests && !retried)
				{
					var seconds = JsonDocument.Parse(body).RootElement.GetProperty("retry_after").GetDouble();
					await Task.Delay(TimeSpan.FromSeconds(seconds));
					continue;
				}

				if (!res.IsSuccessStatusCode)
				{
					throw new HttpRequestException($"Discord webhook の送信に失敗しました: {(int)res.StatusCode} {body}");
				}
				return;
			}
		}
	}
}
