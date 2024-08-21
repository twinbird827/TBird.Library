using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Console;
using TBird.Core;
using TBird.IO.Pdf;

namespace PDF2JPG
{
	public class MyExecuter : ConsoleAsyncExecuter
	{
		protected override Dictionary<string, string> GetOptions(Dictionary<string, string> options)
		{
			SetOption(options, "O", AppSetting.Instance.Option,
				$"起動ｵﾌﾟｼｮﾝを選択してください。",
				$"0: 元となるPDFﾌｧｲﾙを残す。",
				$"1: 処理が完了したらPDFﾌｧｲﾙを削除する。",
				$"ﾃﾞﾌｫﾙﾄ: {AppSetting.Instance.Option}"
			);

			return options;
		}

		protected override async Task ProcessAsync(Dictionary<string, string> options, string[] args)
		{
			var option = int.Parse(options["O"]);

			var executes = args.AsParallel().Select(async arg =>
			{
				return await Execute(option, arg);
			});

			// 実行ﾊﾟﾗﾒｰﾀに対して処理実行
			var results = await Task.WhenAll(executes);

			if (results.Contains(false))
			{
				// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
				Pause(options);
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

				// PDFﾌｧｲﾙを画像ﾌｧｲﾙに変換
				await PdfUtil.Pdf2Jpg(pdftemp, AppSetting.Instance.NumberOfParallel, AppSetting.Instance.Dpi);

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

	}
}