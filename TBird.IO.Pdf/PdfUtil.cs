using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.IO.Pdf
{
	public static class PdfUtil
	{
		internal const string KEY_DATA = "TBird.IO.Pdf.PdfUtil";

		private static PdfUtilExecutor _executor = new PdfUtilExecutor();

		private static PdfUtilWrapper _wrapper = new PdfUtilWrapper();

		internal static void Execute(Action<string> action, params object[] args)
		{
			var path = Assembly.GetExecutingAssembly().Location;

			CoreUtil.Execute(new ProcessStartInfo()
			{
				WorkingDirectory = Path.GetDirectoryName(path),
				FileName = FileUtil.GetFullPathWithoutExtension(path) + ".exe",
				Arguments = "\"" + args.GetString("\" \"") + "\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
			}, action);
		}

		internal static void Execute(string[] args)
		{
			if (0 == args.Length || args[0] != KEY_DATA) return;

			switch (args[1])
			{
				case nameof(_executor.GetPageSize):
					Console.Write(_wrapper.GetPageSize(args[2]));
					return;
				case nameof(_executor.Pdf2Jpg):
					_wrapper.Pdf2Jpg(args[2], args[3].GetInt32(), args[4].GetInt32(), args[5].GetInt32());
					return;
				case nameof(_executor.PutPageNumber):
					_wrapper.PutPageNumber(args[2]);
					return;
			}
		}

		/// <summary>
		/// 指定したPDFのﾍﾟｰｼﾞ数を取得します。
		/// </summary>
		/// <param name="pdffile">PDFﾌｧｲﾙﾊﾟｽ</param>
		/// <returns></returns>
		public static int GetPageSize(string pdffile)
		{
			return _executor.GetPageSize(pdffile);
		}

		/// <summary>
		/// 指定したPDFをﾍﾟｰｼﾞ毎に画像化します。
		/// </summary>
		/// <param name="pdffile">PDFﾌｧｲﾙﾊﾟｽ</param>
		/// <param name="parallel">一度に処理するﾍﾟｰｼﾞ数</param>
		/// <param name="dpi">解像度</param>
		public static async Task Pdf2Jpg(string pdffile, int parallel, int dpi)
		{
			var pagesize = GetPageSize(pdffile);

			DirectoryUtil.Create(FileUtil.GetFullPathWithoutExtension(pdffile));

			await Enumerable.Range(0, (int)Math.Ceiling((double)pagesize / parallel)).AsParallel().Select(i => Task.Run(() =>
			{
				var min = i * parallel + 1;
				var max = Math.Min((i + 1) * parallel, pagesize);

				_executor.Pdf2Jpg(pdffile, min, max, dpi);
			})).WhenAll();

			DirectoryUtil.OrganizeNumber(FileUtil.GetFullPathWithoutExtension(pdffile));
		}

		/// <summary>
		/// PDFﾌｧｲﾙのﾌｯﾀにﾍﾟｰｼﾞ番号を追加します。
		/// </summary>
		/// <param name="pdffile">PDFﾌｧｲﾙﾊﾟｽ</param>
		public static void PutPageNumber(string pdffile)
		{
			_executor.PutPageNumber(pdffile);
		}
	}
}