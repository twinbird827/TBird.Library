using SkiaSharp;
using System;
using System.IO;
using TBird.Core;

namespace TBird.IO.Img
{
	public static class ImgUtil
	{
		public static string GetEncodedExtension(string value)
		{
			try
			{
				using (var stream = File.OpenRead(value))
				using (var codec = SKCodec.Create(stream))
				{
					return "." + Enum.GetName(typeof(SKEncodedImageFormat), codec.EncodedFormat).ToLower();
				}
			}
			catch
			{

			}
			return null;
		}

		/// <summary>
		/// 指定した幅及び高さを下回るようにﾘｻｲｽﾞします。
		/// </summary>
		/// <param name="src">画像ﾌｧｲﾙﾊﾟｽ</param>
		/// <param name="width">幅</param>
		/// <param name="height">高さ</param>
		/// <param name="quality">ﾘｻｲｽﾞ時の品質</param>
		public static void ResizeUnder(string src, double width, double height, int quality)
		{
			Resize(src, width, height, quality, (a, b) => Math.Min(a, b));
		}

		/// <summary>
		/// 指定した幅及び高さを超えるようにﾘｻｲｽﾞします。
		/// </summary>
		/// <param name="src">画像ﾌｧｲﾙﾊﾟｽ</param>
		/// <param name="width">幅</param>
		/// <param name="height">高さ</param>
		/// <param name="quality">ﾘｻｲｽﾞ時の品質</param>
		public static void ResizeOver(string src, double width, double height, int quality)
		{
			Resize(src, width, height, quality, (a, b) => Math.Max(a, b));
		}

		/// <summary>
		/// 指定した幅及び高さへﾘｻｲｽﾞします。
		/// </summary>
		/// <param name="src">画像ﾌｧｲﾙﾊﾟｽ</param>
		/// <param name="width">幅</param>
		/// <param name="height">高さ</param>
		/// <param name="quality">ﾘｻｲｽﾞ時の品質</param>
		/// <param name="func">指定ｻｲｽﾞを下回った／超えた場合の対処</param>
		private static void Resize(string src, double width, double height, int quality, Func<double, double, double> func)
		{
			using (var beforereader = File.OpenRead(src))
			using (var before = SKBitmap.Decode(beforereader))
			{
				// 新しいｻｲｽﾞのｽｹｰﾙを計算
				var scale = func(func(width / before.Width, height / before.Height), 1);

				if (scale == 1)
				{
					using (var codecreader = File.OpenRead(src))
					using (var codec = SKCodec.Create(codecreader))
					{
						// ﾘｻｲｽﾞが必要なく、画像ﾌｫｰﾏｯﾄがjpegだった場合、中断する
						if (codec.EncodedFormat == SKEncodedImageFormat.Jpeg) return;
					}
				}

				// 旧ﾌｧｲﾙを削除
				FileUtil.Delete(src);

				var info = before.Info.WithSize(
					(int)scale.Multiply(before.Width),
					(int)scale.Multiply(before.Height)
				);

				using (var afterwriter = File.OpenWrite(FileUtil.GetFullPathWithoutExtension(src) + ".jpg"))
				using (var after = before.Resize(info, SKFilterQuality.High))
				{
					after.Encode(afterwriter, SKEncodedImageFormat.Jpeg, quality);
				}
			}
		}
	}
}