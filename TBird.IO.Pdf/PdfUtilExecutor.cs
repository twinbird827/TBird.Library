using System;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.IO.Pdf
{
	internal class PdfUtilExecutor
	{
		public async Task<int> GetPageSize(string pdffile)
		{
			int result = 0;
			await PdfUtil.ExecuteAsync(s => result = s.GetInt32(), PdfUtil.KEY_DATA, nameof(GetPageSize), pdffile).ConfigureAwait(false);
			// 子が exit 0 でも stdout の受信が欠落しうる(CoreUtil.ExecuteAsync の EOF 待ち打ち切り)ため親側でも検査する。
			if (result <= 0) throw new InvalidOperationException($"ﾍﾟｰｼﾞ数を取得できませんでした(親側受信値: {result}): {pdffile}");
			return result;
		}

		public Task Pdf2Jpg(string pdffile, int start, int end, int dpi)
		{
			return PdfUtil.ExecuteAsync(Console.WriteLine, PdfUtil.KEY_DATA, nameof(Pdf2Jpg), pdffile, start, end, dpi);
		}

		public Task PutPageNumber(string pdffile)
		{
			return PdfUtil.ExecuteAsync(Console.WriteLine, PdfUtil.KEY_DATA, nameof(PutPageNumber), pdffile);
		}
	}
}