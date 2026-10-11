using System.IO;
using NUnit.Framework;
using SkiaSharp;

namespace TBird.IO.Img.Tests
{
	public class ImgUtilResizeTests
	{
		// 縮小が最近傍補間に落ちず、SkiaSharp 2.88.8 の SKFilterQuality.High と同じ mip 最寄り＋bilinear になることを固定する。

		[Test]
		public void ResizeUnderBlendsStripes()
		{
			var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, "resize");
			if (Directory.Exists(work)) Directory.Delete(work, true);
			Directory.CreateDirectory(work);
			var src = Path.Combine(work, "stripes.png");

			// 白黒 1px 交互の縦縞は、最近傍なら 0/255 のまま、補間すれば中間値になる。
			using (var bitmap = new SKBitmap(4000, 3000))
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

			ImgUtil.ResizeUnder(src, 2400, 1350, 100);

			using (var after = SKBitmap.Decode(Path.ChangeExtension(src, ".jpg")))
			{
				var y = after.Height / 2;
				var mid = 0;
				for (var x = 0; x < after.Width; x++)
				{
					var r = after.GetPixel(x, y).Red;
					if (64 <= r && r <= 191) mid++;
				}
				Assert.That((double)mid / after.Width, Is.GreaterThanOrEqualTo(0.9));
			}
		}
	}
}
