using GhostscriptSharp;
using GhostscriptSharp.Settings;
using System.Drawing;
using System.IO;
using TBird.Core;

namespace TBird.IO.Pdf
{
	internal class PdfUtilWrapper
	{
		public int GetPageSize(string pdffile)
		{
			return GhostscriptWrapper.GetPageSize(pdffile);
		}

		public void Pdf2Jpg(string pdffile, int start, int end, int dpi)
		{
			var jpgdir = FileUtil.GetFullPathWithoutExtension(pdffile);
			var jpgexp = $"{start}-{end}-%d.jpeg";

			GhostscriptWrapper.Pdf2Image(
				pdffile,
				// gs は OutputFile の % を書式指定として扱うため、ﾌｫﾙﾀﾞ側だけ %% にして jpgexp の %d は連番の書式として残す
				Path.Combine(jpgdir.Replace("%", "%%"), jpgexp),
				GhostscriptDevices.jpeg,
				new Size(dpi, dpi),
				GhostscriptPageSizes.a1,
				start,
				end
			);
		}

		public void PutPageNumber(string pdffile)
		{
			GhostscriptWrapper.PutPageNumber(pdffile);
		}
	}
}