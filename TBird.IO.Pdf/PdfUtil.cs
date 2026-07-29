using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using TBird.Core;

namespace TBird.IO.Pdf
{
	public static class PdfUtil
	{
		internal const string KEY_DATA = "TBird.IO.Pdf.PdfUtil";

		private static PdfUtilExecutor _executor = new PdfUtilExecutor();

		private static PdfUtilWrapper _wrapper = new PdfUtilWrapper();

		// async 化で全ﾊﾞｯﾁが一斉起動するため、GhostScript ﾌﾟﾛｾｽの同時数を CPU ｺｱ数で制限する。
		// ﾌﾟﾛｾｽ全体で共有する static ｷｬｯﾌﾟ(複数 PDF 同時変換でも fan-out 分の上限を維持する)。
		// ponytail: ｷｬｯﾌﾟは ProcessorCount 固定。調整が要る実例が出たら引数化する。
		private static readonly SemaphoreSlim _limiter = new SemaphoreSlim(Environment.ProcessorCount);

		internal static async Task<int> ExecuteAsync(Action<string> action, params object[] args)
		{
			var path = Assembly.GetExecutingAssembly().Location;

			var exitcode = await CoreUtil.ExecuteAsync(new ProcessStartInfo()
			{
				WorkingDirectory = Path.GetDirectoryName(path),
				FileName = FileUtil.GetFullPathWithoutExtension(path) + ".exe",
				Arguments = "\"" + args.GetString("\" \"") + "\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
			}, action).ConfigureAwait(false);

			// 子ﾌﾟﾛｾｽの失敗を握り潰さない。子は正常時 0 / 失敗時 1 固定(Program.cs)のため非ｾﾞﾛ＝真の失敗。
			if (exitcode != 0) throw new InvalidOperationException($"PDF 子ﾌﾟﾛｾｽが exit code {exitcode} で失敗しました: {args.GetString(" ")}");

			return exitcode;
		}

		internal static void Execute(Action<string> action, params object[] args)
		{
			ExecuteAsync(action, args).GetAwaiter().GetResult();
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
					_wrapper.Pdf2Jpg(args[2], args[3].GetInt32(), args[4].GetInt32(), args[5].GetInt32()).GetAwaiter().GetResult();
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
		/// <exception cref="InvalidOperationException">ﾍﾟｰｼﾞ数を取得できなかった場合(従来は 0 を返却)</exception>
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
		/// <exception cref="InvalidOperationException">ﾍﾟｰｼﾞ数取得または画像化に失敗した場合(従来は空ﾌｫﾙﾀﾞで正常終了)。失敗時、変換済みJPEGは出力ﾌｫﾙﾀﾞに残るため再試行前に空にすること</exception>
		public static async Task Pdf2Jpg(string pdffile, int parallel, int dpi)
		{
			var pagesize = GetPageSize(pdffile);

			DirectoryUtil.Create(FileUtil.GetFullPathWithoutExtension(pdffile));

			await Enumerable.Range(0, (int)Math.Ceiling((double)pagesize / parallel)).Select(async i =>
			{
				using (await _limiter.LockAsync().ConfigureAwait(false))
				{
					var min = i * parallel + 1;
					var max = Math.Min((i + 1) * parallel, pagesize);

					await _executor.Pdf2Jpg(pdffile, min, max, dpi).ConfigureAwait(false);
				}
			}).WhenAll().ConfigureAwait(false);

			DirectoryUtil.OrganizeNumber(FileUtil.GetFullPathWithoutExtension(pdffile));
		}

		/// <summary>
		/// PDFﾌｧｲﾙのﾌｯﾀにﾍﾟｰｼﾞ番号を追加します。
		/// </summary>
		/// <param name="pdffile">PDFﾌｧｲﾙﾊﾟｽ</param>
		/// <exception cref="InvalidOperationException">ﾍﾟｰｼﾞ数取得に失敗した場合(従来はﾌｯﾀ「1/0」で原本を置換)</exception>
		public static void PutPageNumber(string pdffile)
		{
			_executor.PutPageNumber(pdffile);
		}
	}
}