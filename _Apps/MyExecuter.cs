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

			// ｴﾗｰがあったら異常終了する。(ｺﾝｿｰﾙ表示とexit codeは基底の例外処理に任せる)
			var failed = results.Count(x => !x);
			if (0 < failed)
			{
				throw new InvalidOperationException($"{failed} 件のPDFﾌｧｲﾙの変換に失敗しました。");
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
			var pdftemp = FileUtil.GetTempFilePath(".pdf");
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

				// 処理後ﾃﾞｨﾚｸﾄﾘに移動
				DirectoryUtil.Move(dirtemp, dircomp);

				MessageService.Info("終了(移動):" + arg);

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