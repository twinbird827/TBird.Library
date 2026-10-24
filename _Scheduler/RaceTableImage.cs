using Netkeiba.Models;
using SkiaSharp;
using TBird.Core;

namespace Netkeiba.Scheduler
{
	public static class RaceTableImage
	{
		private const float FontSize = 18;
		private const float Padding = 8;

		// _Apps/App.xaml の S4CellItem / S4EVCellItem の DataTrigger と揃え、WPF と同じ見た目で上位馬を拾えるようにする
		private static readonly Dictionary<int, (SKColor Back, SKColor Fore)> RankColors = new()
		{
			{ 1, (SKColors.Red, SKColors.White) },
			{ 2, (SKColors.Blue, SKColors.White) },
			{ 3, (SKColors.Green, SKColors.White) },
			{ 4, (SKColors.MediumPurple, SKColors.White) },
			{ 5, (SKColors.Aqua, SKColors.White) },
			{ 6, (SKColors.Yellow, SKColors.Black) },
			{ 7, (SKColors.LightYellow, SKColors.Black) },
		};

		public static (string Header, byte[] Png) Create(RacePrediction[] predictions, DateTime? start)
		{
			var race = predictions[0].Detail.Race;
			var header = $"[{race.RaceId}] [{race.Place}] [R{race.RaceId.Right(2)}] [{race.Grade}] {race.CourseName}"
				+ (start != null ? $" 発走 {start:HH:mm}" : string.Empty)
				+ $" 予想 {DateTime.Now:HH:mm}";

			(string, int) Text(string x) => (x, 0);
			(string, int) Score(RaceScore x) => ($"{x.WinProb * 100:F1}%", x.Rank);
			(string, int) Num(float x, string format) => (float.IsNaN(x) ? "-" : x.ToString(format), 0);

			var rows = predictions
				.Select(x => new[]
				{
					Text(x.Detail.Wakuban.ToString()),
					Text(x.Detail.Umaban.ToString()),
					Text(x.Name),
					Score(x.Total),
					Score(x.Horse),
					Score(x.TotalMedium),
					Score(x.TotalSmall),
					Score(x.Vars2),
					Score(x.Vars1),
					Num(x.Detail.Odds, "F1"),
					float.IsNaN(x.EV.Score) ? Text("-") : ($"{x.EV.Score:F2}", x.EV.Rank),
				})
				.Prepend(new[] { "枠", "馬番", "馬名", "Total", "Horse", "Medium", "Small", "Vars2", "Vars1", "単勝", "EV" }.Select(Text).ToArray())
				.ToArray();

			using var typeface = SKTypeface.FromFamilyName("Meiryo");
			using var boldTypeface = SKTypeface.FromFamilyName("Meiryo", SKFontStyle.Bold);
			using var font = new SKFont(typeface, FontSize);
			using var boldFont = new SKFont(boldTypeface, FontSize);
			using var paint = new SKPaint { Color = SKColors.Black, IsAntialias = true };
			using var line = new SKPaint { Color = SKColors.LightGray, StrokeWidth = 1 };

			var widths = Enumerable.Range(0, rows[0].Length).Select(c => rows.Max(r => boldFont.MeasureText(r[c].Item1)) + Padding * 2).ToArray();
			var rowHeight = FontSize * 1.6f;
			var width = Math.Max(widths.Sum(), font.MeasureText(header) + Padding * 2);
			var height = rowHeight * (rows.Length + 1);

			using var surface = SKSurface.Create(new SKImageInfo((int)Math.Ceiling(width), (int)Math.Ceiling(height)));
			var canvas = surface.Canvas;
			canvas.Clear(SKColors.White);

			var baseline = rowHeight / 2 + FontSize / 2.5f;
			canvas.DrawText(header, Padding, baseline, SKTextAlign.Left, font, paint);

			for (var r = 0; r < rows.Length; r++)
			{
				var y = rowHeight * (r + 1);
				canvas.DrawLine(0, y, width, y, line);

				var x = 0f;
				for (var c = 0; c < rows[r].Length; c++)
				{
					var (text, rank) = rows[r][c];
					if (RankColors.TryGetValue(rank, out var color))
					{
						using var fill = new SKPaint { Color = color.Back };
						using var fore = new SKPaint { Color = color.Fore, IsAntialias = true };
						// 行の上端に引いた罫線を塗り潰さないよう 1px 下げる
					canvas.DrawRect(x, y + 1, widths[c], rowHeight - 1, fill);
						canvas.DrawText(text, x + Padding, y + baseline, SKTextAlign.Left, boldFont, fore);
					}
					else
					{
						canvas.DrawText(text, x + Padding, y + baseline, SKTextAlign.Left, font, paint);
					}
					x += widths[c];
				}
			}

			using var image = surface.Snapshot();
			using var data = image.Encode(SKEncodedImageFormat.Png, 100);
			return (header, data.ToArray());
		}
	}
}
