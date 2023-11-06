using System.Diagnostics;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TBird.Core;
using TBird.IO.Pdf;

namespace PDF2ZIP
{
	internal class Process
	{
		public static int GetOption(string? line)
		{
			switch (line)
			{
				case "0":
				case "1":
					return int.Parse(line);
				default:
					return AppSetting.Instance.Option;
			}
		}

		public static async Task Execute(int option, string[] args)
		{
			var executes = args.AsParallel().Select(async arg =>
			{
				return await Execute(option, arg);
			});

			// 実行ﾊﾟﾗﾒｰﾀに対して処理実行
			var results = await Task.WhenAll(executes);

			if (results.Contains(false))
			{
				// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
				Console.ReadLine();
			}
		}

		private static async Task<bool> Execute(int option, string arg)
		{
			// 対象PDFﾌｧｲﾙ
			var pdfpath = Directories.GetAbsolutePath(arg);

			if (!Path.GetExtension(pdfpath).ToLower().EndsWith("pdf"))
			{
				MessageService.Info("中断(非PDFﾌｧｲﾙ):" + arg);
				return false;
			}

			// 作業中のPDFﾌｧｲﾙﾊﾟｽ
			var pdftemp = Path.Combine(Directories.TemporaryDirectory, $"{Guid.NewGuid()}.pdf");
			// 作業中のﾃﾞｨﾚｸﾄﾘ
			var dirtemp = FileUtil.GetFileNameWithoutExtension(pdftemp);
			// 処理後のﾃﾞｨﾚｸﾄﾘ
			var dircomp = FileUtil.GetFileNameWithoutExtension(pdfpath);

			try
			{
				MessageService.Info("***** 開始:" + arg);

				// 作業用ﾃﾞｨﾚｸﾄﾘ作成
				DirectoryUtil.Create(dirtemp);

				MessageService.Info("終了(作業用ﾃﾞｨﾚｸﾄﾘ作成):" + arg);

				// PDFﾌｧｲﾙを一時ﾃﾞｨﾚｸﾄﾘにｺﾋﾟｰ(且つ、半角英数で構成されたﾌｧｲﾙ名にする)
				await FileUtil.CopyAsync(pdfpath, pdftemp);

				MessageService.Info("終了(作業ﾌｧｲﾙｺﾋﾟｰ):" + arg);

				// 単一ｲﾝｽﾀﾝｽで全ﾍﾟｰｼﾞJPG化すると遅いので設定ﾌｧｲﾙで指定したﾍﾟｰｼﾞ数で処理を分ける
				await PdfUtil.PDF2JPG2(pdftemp, AppSetting.Instance.NumberOfParallel, AppSetting.Instance.Dpi, AppSetting.Instance.Quality);

				//// ﾌｧｲﾙ名を連番にする。
				//var i = 0; foreach (var x in DirectoryUtil.GetFiles(dirtemp))
				//{
				//	FileUtil.Move(x, Path.Combine(dirtemp, string.Format("{0,0:D7}", i++) + Path.GetExtension(x)));
				//}

				// 処理後ﾃﾞｨﾚｸﾄﾘに移動
				DirectoryUtil.Move(dirtemp, dircomp);

				MessageService.Info("終了(移動):" + arg);

				// 作業用PDFﾌｧｲﾙを削除
				FileUtil.Delete(pdftemp);

				if (option == 1)
				{
					// 元のPDFﾌｧｲﾙを削除
					FileUtil.Delete(pdfpath);
					MessageService.Info("***** 元ﾌｧｲﾙ削除:" + arg);
				}

				MessageService.Info("***** 終了:" + arg);

				return true;
			}
			catch (Exception ex)
			{
				MessageService.Info(arg + ex.ToString());
				return false;
			}
			finally
			{
				// 作業用ﾃﾞｨﾚｸﾄﾘ、及びﾌｧｲﾙ削除
				FileUtil.Delete(pdftemp);
				DirectoryUtil.Delete(dirtemp);
			}
		}

		//private static IEnumerable<string> GetSettingIndexes(string pdffile)
		//{
		//	try
		//	{
		//		var pu = AppSetting.Instance.ProcessingUnit;
		//		var pages = PdfUtil.GetNumberOfPages(pdffile);
		//		return Enumerable.Range(0, (int)Math.Ceiling(pages / (double)pu)).Select(i =>
		//		{
		//			var min = i * pu + 1;
		//			var max = (i + 1) * pu;

		//			return $"{min}-{(max < pages ? max : pages)}";
		//		});
		//	}
		//	catch
		//	{
		//		return new[] { "0-0" };
		//	}
		//}
	}
}