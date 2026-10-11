using System.IO;
using NUnit.Framework;
using SkiaSharp;

namespace TBird.IO.Img.Tests
{
	public class ImgUtilResizeTests
	{
		// 縮小・拡大が最近傍補間に落ちず、SkiaSharp 2.88.8 の SKFilterQuality.High と同じ（縮小は mip 最寄り＋bilinear、拡大は bicubic（Mitchell））になることを固定する。

		[TestCase(4000, 3000, false, 2400, 1350, 0.9)]
		// Mitchell の 2 倍拡大は 1px 縞を約 196 / 59 に写して中間値の帯に入らないため 3 倍にする。
		[TestCase(400, 300, true, 1200, 900, 0.5)]
		public void ResizeBlendsStripes(int srcWidth, int srcHeight, bool over, int width, int height, double threshold)
		{
			var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, "resize");
			if (Directory.Exists(work)) Directory.Delete(work, true);
			Directory.CreateDirectory(work);
			var src = Path.Combine(work, "stripes.png");

			// 白黒 1px 交互の縦縞は、最近傍なら 0/255 のまま、補間すれば中間値になる。
			using (var bitmap = new SKBitmap(srcWidth, srcHeight))
			using (var canvas = new SKCanvas(bitmap))
			using (var white = new SKPaint { Color = SKColors.White })
			{
				canvas.Clear(SKColors.Black);
				for (var x = 0; x < bitmap.Width; x += 2) canvas.DrawRect(x, 0, 1, bitmap.Height, white);
				using (var stream = File.OpenWrite(src))
				{
					bitmap.Encode(stream, SKEncodedImageFormat.Png, 100);
				}
			}

			if (over) ImgUtil.ResizeOver(src, width, height, 100);
			else ImgUtil.ResizeUnder(src, width, height, 100);

			using (var after = SKBitmap.Decode(Path.ChangeExtension(src, ".jpg")))
			{
				var y = after.Height / 2;
				var mid = 0;
				for (var x = 0; x < after.Width; x++)
				{
					var r = after.GetPixel(x, y).Red;
					if (64 <= r && r <= 191) mid++;
				}
				Assert.That((double)mid / after.Width, Is.GreaterThanOrEqualTo(threshold));
			}
		}
	}
}
