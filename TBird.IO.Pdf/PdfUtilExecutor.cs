using System;
using TBird.Core;

namespace TBird.IO.Pdf
{
	internal class PdfUtilExecutor : IPdfUtil
	{
		public int GetPageSize(string pdffile)
		{
			int result = 0;
			PdfUtil.Execute(s => result = s.GetInt32(), PdfUtil.KEY_DATA, nameof(GetPageSize), pdffile);
			return result;
		}

		public void Pdf2Jpg(string pdffile, int start, int end, int dpi)
		{
			PdfUtil.Execute(Console.WriteLine, PdfUtil.KEY_DATA, nameof(Pdf2Jpg), pdffile, start, end, dpi);
		}

		public void PutPageNumber(string pdffile)
		{
			PdfUtil.Execute(Console.WriteLine, PdfUtil.KEY_DATA, nameof(PutPageNumber), pdffile);
		}
	}
}