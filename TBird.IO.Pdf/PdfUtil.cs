using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using TBird.Core;
using TBird.Wpf;
using Windows.Data.Pdf;

namespace TBird.IO.Pdf

{
	public static class PdfUtil
	{
		public static async Task PDF2JPG2(string pdffile, int parallel, double dpi, int quality)
		{
			var jpgdir = FileUtil.GetFileNameWithoutExtension(pdffile);

			using (var pdfStream = File.OpenRead(pdffile))
			using (var winrtStream = pdfStream.AsRandomAccessStream())
			{
				var doc = await PdfDocument.LoadFromStreamAsync(winrtStream);
				for (var i = 0u; i < doc.PageCount; i++)
				{
					using (var page = doc.GetPage(i))
					using (var memStream = new WrappingStream(new MemoryStream()))
					using (var outStream = memStream.AsRandomAccessStream())
					{
						var jpgfile = Path.Combine(jpgdir, string.Format("{0,0:D7}", i) + ".bmp");
						await page.RenderToStreamAsync(outStream);

						using (var image = System.Drawing.Image.FromStream(memStream))
						{
							image.Save(jpgfile, ImageFormat.Bmp);
						}
					}
				}
			}
		}

		public static async Task PDF2JPG(string pdffile, int parallel, double dpi, int quality)
		{
			var file = await Windows.Storage.StorageFile.GetFileFromPathAsync(pdffile);
			using (var stream = await file.OpenReadAsync())
			{
				var pdf = await PdfDocument.LoadFromStreamAsync(stream);

				var jpgdir = FileUtil.GetFileNameWithoutExtension(pdffile);
				var semaphore = new SemaphoreSlim(parallel, parallel);

				await Enumerable.Range(0, (int)pdf.PageCount).AsParallel().WithDegreeOfParallelism(parallel).Select(async i =>
				{
					using (var page = pdf.GetPage((uint)i))
					{
						var jpgfile = Path.Combine(jpgdir, string.Format("{0,0:D7}", i) + ".jpg");

						await PDF2JPG(page, jpgfile, dpi, quality);
					}
				}).WhenAll().TryCatch();
			}
		}

		private static async Task PDF2JPG(PdfPage page, string jpgfile, double dpi, int quality)
		{
			var options = new PdfPageRenderOptions();
			options.DestinationHeight = (uint)Math.Round(page.Size.Height * (dpi / 96.0), MidpointRounding.AwayFromZero);

			using (var access = new Windows.Storage.Streams.InMemoryRandomAccessStream())
			{
				await page.RenderToStreamAsync(access, options);

				var image = ControlUtil.GetImage(access.AsStream());

				JpegBitmapEncoder encoder = new JpegBitmapEncoder();
				encoder.QualityLevel = quality;
				encoder.Frames.Add(BitmapFrame.Create(image));

				FileUtil.BeforeCreate(jpgfile);

				using (var fileStream = new FileStream(jpgfile, FileMode.Create, FileAccess.Write))
				{
					encoder.Save(fileStream);
				}
			}
		}
	}
}