using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TBird.IO.Pdf
{
	internal interface IPdfUtil
	{
		/// <summary>
		/// 指定したPDFのﾍﾟｰｼﾞ数を取得します。
		/// </summary>
		/// <param name="pdffile">PDFﾌｧｲﾙﾊﾟｽ</param>
		/// <returns></returns>
		int GetPageSize(string pdffile);

		/// <summary>
		/// 指定したPDFをﾍﾟｰｼﾞ毎に画像化します。
		/// </summary>
		/// <param name="pdffile">PDFﾌｧｲﾙﾊﾟｽ</param>
		/// <param name="start">画像化する最初のﾍﾟｰｼﾞ番号</param>
		/// <param name="end">画像化する最後のﾍﾟｰｼﾞ番号</param>
		/// <param name="dpi">解像度</param>
		void Pdf2Jpg(string pdffile, int start, int end, int dpi);

		/// <summary>
		/// PDFﾌｧｲﾙのﾌｯﾀにﾍﾟｰｼﾞ番号を追加します。
		/// </summary>
		/// <param name="pdffile">PDFﾌｧｲﾙﾊﾟｽ</param>
		void PutPageNumber(string pdffile);
	}
}