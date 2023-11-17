using System.Diagnostics;
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

			AppSetting.Instance.Option = Math.Min(CoreUtil.Nvl(Console.ReadLine(), AppSetting.Instance.Option).GetInt32(), 1);

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
			var withoutextension = FileUtil.GetFileNameWithoutExtension(src);
			var epub = Path.Combine(AppSetting.Instance.OutputDir, withoutextension + ".epub");
			var htmlz = Path.Combine(AppSetting.Instance.OutputDir, withoutextension + ".htmlz");

			var argments = new[]
			{
				$"\"{src}\" \"{epub}\"",
				$"\"{src}\" \"{htmlz}\""
			};

			foreach (var argment in argments)
			{
				var info = new ProcessStartInfo()
				{
					WorkingDirectory = Path.GetDirectoryName(AppSetting.Instance.Calibre),
					FileName = AppSetting.Instance.Calibre,
					Arguments = argment,
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardOutput = true,
				};

				await CoreUtil.ExecuteAsync(info, Console.WriteLine);
			}

			// ZIPﾌｧｲﾙを解凍する。
			ZipUtil.ExtractToDirectory(htmlz);

			// ZIPﾌｧｲﾙは不要なので削除
			FileUtil.Delete(htmlz);

			// 以後はﾌｫﾙﾀﾞに対して処理する
			htmlz = FileUtil.GetFullPathWithoutExtension(htmlz);

			// ｽﾀｲﾙｼｰﾄの上書き
			await FileUtil.CopyAsync(Directories.GetAbsolutePath("style.css"), Path.Combine(htmlz, "style.css"));

			// HTMLをPDFに変換
			var srcpdf = Path.Combine(htmlz, "index.html");
			var dstpdf = Path.Combine(AppSetting.Instance.OutputDir, withoutextension + ".pdf");

			await HeadlessWebView2.Call(new Uri(srcpdf), webview2 =>
			{
				return webview2.CoreWebView2.PrintToPdfAsync(dstpdf);
			});

			// PDFにﾍﾟｰｼﾞ番号を追加
			PdfUtil.PutPageNumber(dstpdf);

			// ﾌｧｲﾙ名をﾀｲﾄﾙにする。
			ChangeFilename(srcpdf, dstpdf, epub);

			// HTMLﾌｫﾙﾀﾞは不要なので削除
			if (AppSetting.Instance.Option == 1) DirectoryUtil.Delete(htmlz);

			return true;
		}

		private static IEnumerable<string> GetFiles(string dir)
		{
			var extensions = new[] { "*.azw", "*.azw3" };

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
					var dir = Path.GetDirectoryName(target);
					if (dir == null) break;
					FileUtil.Move(target, Path.Combine(dir, title + Path.GetExtension(target)));
				}
			}
		}
	}
}