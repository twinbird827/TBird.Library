using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TBird.Console;
using TBird.Core;
using TBird.IO.Html;
using TBird.IO.Pdf;

namespace EBook2PDF
{
	public class MyExecuter : ConsoleAsyncExecuter
	{
		protected override Dictionary<string, string> GetOptions(Dictionary<string, string> options)
		{
			SetOption(options, "O", AppSetting.Instance.Option,
				$"起動ｵﾌﾟｼｮﾝを選択してください。",
				$"0: HTMLﾌｧｲﾙを残す。",
				$"1: 処理が完了したらHTMLﾌｧｲﾙを削除する。",
				$"ﾃﾞﾌｫﾙﾄ: {AppSetting.Instance.Option}"
			);

			return options;
		}

		protected override async Task ProcessAsync(Dictionary<string, string> options, string[] args)
		{
			var option = int.Parse(AppSetting.Instance.Option = options["O"]);

			var executes = await args
				.SelectMany(GetFiles)
				.AsParallel()
				.Select(Execute)
				.WhenAll();

			// 実行ﾊﾟﾗﾒｰﾀに対して処理実行
			var results = executes;

			AppSetting.Instance.Save();

			if (results.Contains(false))
			{
				// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
				Pause(options);
			}
		}

		private async Task<bool> Execute(string src)
		{
			// ******************************
			// Amazon kindle -> epub変換
			if (IsTarget(src, ".azw", ".azw3", ".prc"))
			{
				// epubﾌｧｲﾙ生成
				await CallCalibre(src, ".epub");

				// 対象ﾌｧｲﾙをepubに変更
				src = Path.Combine(AppSetting.Instance.OutputDir, FileUtil.GetFileNameWithoutExtension(src) + ".epub");
			}

			// ******************************
			// epub -> htmlz
			if (IsTarget(src, ".epub"))
			{
				// htmlzﾌｧｲﾙ生成
				await CallCalibre(src, ".htmlz");

				// 対象ﾌｧｲﾙをhtmlzに変更
				src = Path.Combine(AppSetting.Instance.OutputDir, FileUtil.GetFileNameWithoutExtension(src) + ".htmlz");
			}

			// ******************************
			// htmlz -> 展開したhtml
			if (IsTarget(src, ".htmlz"))
			{
				// ZIPﾌｧｲﾙを解凍する。
				ZipUtil.ExtractToDirectory(src);

				// ZIPﾌｧｲﾙは不要なので削除
				FileUtil.Delete(src);

				// 以後はﾌｫﾙﾀﾞに対して処理する
				src = FileUtil.GetFullPathWithoutExtension(src);
			}

			// ******************************
			// 展開したhtml -> pdf

			// ｽﾀｲﾙｼｰﾄの上書き
			await FileUtil.CopyAsync(Directories.GetAbsolutePath("style.css"), Path.Combine(src, "style.css"));

			// 縦書きに適した文字に置換
			FileUtil.Replace(Path.Combine(src, "index.html"), Encoding.UTF8, Enumerable.Range(0, 5)
				.Select(i => (int)Math.Pow(2, 4 - i))
				.SelectMany(i => Arr('…'.Repeat(i).Kvp("・・・"), '―'.Repeat(i).Kvp("|"), '─'.Repeat(i).Kvp("|")))
				.ToArray()
			);

			// HTMLをPDFに変換
			var withoutextension = FileUtil.GetFileNameWithoutExtension(src);
			var srcpdf = Path.Combine(src, "index.html");
			var dstpdf = Path.Combine(AppSetting.Instance.OutputDir, withoutextension + ".pdf");
			var epub = Path.Combine(AppSetting.Instance.OutputDir, withoutextension + ".epub");

			await HeadlessWebView2.Call(new Uri(srcpdf), webview2 =>
			{
				return webview2.CoreWebView2.PrintToPdfAsync(dstpdf);
			});

			// PDFにﾍﾟｰｼﾞ番号を追加
			PdfUtil.PutPageNumber(dstpdf);

			// ﾌｧｲﾙ名をﾀｲﾄﾙにする。
			ChangeFilename(srcpdf, ref dstpdf, epub);

			// ******************************
			// pdf -> jpg

			// 同PC上にPDF2JPGが存在するなら
			if (await FileUtil.Exists(AppSetting.Instance.PDF2JPG))
			{
				// PDF->JPG変換後のﾌｫﾙﾀﾞﾊﾟｽ
				var dstjpg = FileUtil.GetFullPathWithoutExtension(dstpdf);

				// HTMLﾌｫﾙﾀﾞとJPG変換後のﾌｫﾙﾀﾞが同名なら予めHTMLﾌｫﾙﾀﾞをﾘﾈｰﾑしておく
				DirectoryUtil.Move(src, src = src + "HTML", false);

				await CoreUtil.ExecuteAsync(new ProcessStartInfo()
				{
					WorkingDirectory = Path.GetDirectoryName(AppSetting.Instance.PDF2JPG),
					FileName = AppSetting.Instance.PDF2JPG,
					Arguments = $"/H /O={AppSetting.Instance.Option} \"{dstpdf}\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
				}, Console.WriteLine);

				// ｶﾊﾞｰを移動する
				await FileUtil.CopyAsync(Path.Combine(src, @"cover.jpg"), Path.Combine(dstjpg, @"000.jpg"));
			}

			// HTMLﾌｫﾙﾀﾞは不要なので削除
			if (AppSetting.Instance.Option == "1") DirectoryUtil.Delete(src);

			return true;
		}

		private bool IsTarget(string src, params string[] extensions)
		{
			return extensions.Contains(Path.GetExtension(src).ToLower());
		}

		private Task CallCalibre(string src, string dstextension)
		{
			var withoutextension = FileUtil.GetFileNameWithoutExtension(src);
			var dst = Path.Combine(AppSetting.Instance.OutputDir, withoutextension + dstextension);

			var info = new ProcessStartInfo()
			{
				WorkingDirectory = Path.GetDirectoryName(AppSetting.Instance.Calibre),
				FileName = AppSetting.Instance.Calibre,
				Arguments = $"\"{src}\" \"{dst}\"",
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardOutput = true,
			};

			var tmp = Console.OutputEncoding;
			Console.OutputEncoding = Encoding.UTF8;
			try
			{
				return CoreUtil.ExecuteAsync(info, Console.WriteLine);
			}
			finally
			{
				Console.OutputEncoding = tmp;
			}
		}

		private IEnumerable<string> GetFiles(string dir)
		{
			if (Directory.Exists(dir) && DirectoryUtil.GetFiles(dir, "index.html").Any())
			{
				yield return dir;
				yield break;
			}

			var extensions = new[] { "*.azw", "*.azw3", "*.prc", "*.epub", "*.htmlz" };

			if (File.Exists(dir) && extensions.Contains("*" + Path.GetExtension(dir)))
			{
				yield return dir;
				yield break;
			}

			var targets = extensions
				.SelectMany(filter => DirectoryUtil.GetFiles(dir, filter));

			foreach (var x in targets)
			{
				yield return x;
			}

			var children = DirectoryUtil.GetDirectories(dir)
				.SelectMany(GetFiles);

			foreach (var x in children)
			{
				yield return x;
			}
		}

		public void ChangeFilename(string srchtml, ref string pdf, string epub)
		{
			var src = File.ReadAllText(srchtml);

			var match = Regex.Match(src, @"<title>(?<title>[^<]+)</title>");
			if (match.Success)
			{
				var title = match.Groups["title"].Value;

				foreach (var target in new[] { pdf, epub })
				{
					if (!File.Exists(target)) break;
					var dir = Path.GetDirectoryName(target);
					if (dir == null) break;
					var dst = Path.Combine(dir, title + Path.GetExtension(target));
					FileUtil.Move(target, dst, false);
					if (target == pdf) pdf = dst;
				}
			}
		}

	}
}