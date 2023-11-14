using System.Diagnostics;
using TBird.Core;
using TBird.IO.Pdf;

namespace PDF2JPG
{
	internal class MyCode
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
			var dirtemp = FileUtil.GetFullPathWithoutExtension(pdftemp);
			// 処理後のﾃﾞｨﾚｸﾄﾘ
			var dircomp = FileUtil.GetFullPathWithoutExtension(pdfpath);

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
				await PDF2JPG(pdftemp, AppSetting.Instance.NumberOfParallel);

				// ﾌｧｲﾙ名を連番にする。
				DirectoryUtil.OrganizeNumber(dirtemp);

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

		public const string Key = "PDF2JPG.GENERATE";

		private static async Task PDF2JPG(string pdffile, int parallel)
		{
			var pagesize = (double)PdfUtil.GetPageSize(pdffile);
			var split = (int)Math.Ceiling(pagesize.Divide(parallel));

			await Enumerable.Range(0, split).AsParallel().Select(i => Task.Run(() =>
			{
				var min = i * parallel + 1;
				var max = Math.Min((i + 1) * parallel, pagesize);

				MessageService.Info($"処理中:{pdffile} {min} - {max} / {pagesize}");

				// 識別子付きで本ﾌﾟﾛｸﾞﾗﾑを実行し直すことで別ﾌﾟﾛｾｽで非同期的にPDFをJPGに変換する。
				CoreUtil.Execute(new ProcessStartInfo()
				{
					FileName = Process.GetCurrentProcess().MainModule?.FileName,
					CreateNoWindow = true,
					Arguments = $"{Key} \"{pdffile}\" {min} {max}",
					UseShellExecute = false
				});
			})).WhenAll();
		}

		public static void PDF2JPG(string pdffile, string min, string max)
		{
			// 別ﾌﾟﾛｾｽで実行される処理
			PdfUtil.PDF2JPG(pdffile, min.GetInt32(), max.GetInt32(), AppSetting.Instance.Dpi);
		}
	}
}