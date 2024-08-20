using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using TBird.Core;
using TBird.IO.Html;
using TBird.IO.Pdf;

namespace EBook2PDF
{
	internal class MyCode
	{
		public static async Task Execute(string[] args)
		{
			Console.WriteLine("起動ｵﾌﾟｼｮﾝを選択してください。");
			Console.WriteLine($"0: HTMLﾌｧｲﾙを残す。");
			Console.WriteLine($"1: 処理が完了したらHTMLﾌｧｲﾙを削除する。");
			Console.WriteLine($"ﾃﾞﾌｫﾙﾄ: {AppSetting.Instance.Option}");

			AppSetting.Instance.Option = Math.Min(CoreUtil.Nvl(Console.ReadLine().NotNull(), AppSetting.Instance.Option).GetInt32(), 1);

			var executes = await args
				.SelectMany(GetFiles)
				.AsParallel()
				.Select(Execute)
				.WhenAll();

			// 実行ﾊﾟﾗﾒｰﾀに対して処理実行
			var results = executes;

			if (results.Contains(false))
			{
				// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
				Console.ReadLine();
			}
		}

		private static async Task<bool> Execute(string src)
		{
			if (IsTarget(src, ".azw", ".azw3"))
			{
				// epubﾌｧｲﾙ生成
				await CallCalibre(src, ".epub");

				// 対象ﾌｧｲﾙをepubに変更
				src = Path.Combine(AppSetting.Instance.OutputDir, FileUtil.GetFileNameWithoutExtension(src) + ".epub");
			}

			if (IsTarget(src, ".epub"))
			{
				// htmlzﾌｧｲﾙ生成
				await CallCalibre(src, ".htmlz");

				// 対象ﾌｧｲﾙをhtmlzに変更
				src = Path.Combine(AppSetting.Instance.OutputDir, FileUtil.GetFileNameWithoutExtension(src) + ".htmlz");
			}

			if (IsTarget(src, ".htmlz"))
			{
				// ZIPﾌｧｲﾙを解凍する。
				ZipUtil.ExtractToDirectory(src);

				// ZIPﾌｧｲﾙは不要なので削除
				FileUtil.Delete(src);

				// 以後はﾌｫﾙﾀﾞに対して処理する
				src = FileUtil.GetFullPathWithoutExtension(src);
			}

			// ｽﾀｲﾙｼｰﾄの上書き
			await FileUtil.CopyAsync(Directories.GetAbsolutePath("style.css"), Path.Combine(src, "style.css"));

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
			ChangeFilename(srcpdf, dstpdf, epub);

			// 同PC上にPDF2JPGが存在するなら
			if (await FileUtil.Exists(AppSetting.Instance.PDF2JPG))
			{
				await CoreUtil.ExecuteAsync(new ProcessStartInfo()
				{
					WorkingDirectory = Path.GetDirectoryName(AppSetting.Instance.PDF2JPG),
					FileName = AppSetting.Instance.PDF2JPG,
					Arguments = $"\"{dstpdf}\"",
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
				}, Console.WriteLine);

				// ｶﾊﾞｰを移動する
				await FileUtil.CopyAsync(Path.Combine(src, @"cover.jpg"), Path.Combine(FileUtil.GetFullPathWithoutExtension(dstpdf), @"000.jpg"));
			}

			// HTMLﾌｫﾙﾀﾞは不要なので削除
			if (AppSetting.Instance.Option == 1) DirectoryUtil.Delete(src);

			return true;
		}

		private static bool IsTarget(string src, params string[] extensions)
		{
			return extensions.Contains(Path.GetExtension(src).ToLower());
		}

		private static Task CallCalibre(string src, string dstextension)
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

			return CoreUtil.ExecuteAsync(info, Console.WriteLine);
		}

		private static IEnumerable<string> GetFiles(string dir)
		{
			if (Directory.Exists(dir) && DirectoryUtil.GetFiles(dir, "index.html").Any())
			{
				yield return dir;
				yield break;
			}

			var extensions = new[] { "*.azw", "*.azw3", "*.epub", "*.htmlz" };

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

		public static void ChangeFilename(string srchtml, string pdf, string epub)
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
					FileUtil.Move(target, Path.Combine(dir, title + Path.GetExtension(target)), false);
				}
			}
		}
	}
}