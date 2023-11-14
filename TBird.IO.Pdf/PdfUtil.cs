using GhostscriptSharp;
using GhostscriptSharp.Settings;
using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.IO.Pdf

{
	public static class PdfUtil
	{
		public static int GetPageSize(string pdffile)
		{
			return GhostscriptWrapper.GetPageSize(pdffile);
		}

		public static async Task PDF2JPG(string pdffile, int parallel, int dpi)
		{
			var pagesize = GetPageSize(pdffile);

			await Enumerable.Range(0, parallel).AsParallel().Select(i => Task.Run(() =>
			{
				var min = i * parallel + 1;
				var max = Math.Min((i + 1) * parallel, pagesize);

				PDF2JPG(pdffile, min, max, dpi);
			})).WhenAll();
		}

		public static void PDF2JPG(string pdffile, int begin, int end, int dpi)
		{
			var jpgdir = FileUtil.GetFullPathWithoutExtension(pdffile);
			var jpgexp = $"{begin}-{end}-%d.jpeg";

			var setting = new GhostscriptSettings();

			setting.Device = GhostscriptDevices.jpeg;
			setting.Page.AllPages = false;
			setting.Page.Start = begin;
			setting.Page.End = end;
			setting.Resolution = new Size(dpi, dpi);
			setting.Size = new GhostscriptPageSize()
			{
				Native = GhostscriptPageSizes.a1
			};

			GhostscriptWrapper.GenerateOutput(pdffile, Path.Combine(jpgdir, jpgexp), setting);
		}
	}
}