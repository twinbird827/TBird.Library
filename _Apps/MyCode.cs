using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TBird.Core;
using TBird.IO.Html;

namespace EBook2PDF
{
	internal class MyCode
	{
		public static async Task Execute(string[] args)
		{
			await Task.Delay(1);
			await HeadlessWebView2.Call(new Uri("https://learn.microsoft.com/ja-jp/microsoft-edge/webview2/how-to/print?tabs=dotnetcsharp"), webview2 =>
			{
				return webview2.CoreWebView2.PrintToPdfAsync(@"C:\work\temp\" + DateTime.Now.ToString("yyMMhh-HHmmss") + ".pdf");
			});
			//var executes = await args
			//	.SelectMany(GetFiles)
			//	.AsParallel()
			//	.Select(Execute)
			//	.WhenAll();

			//// 実行ﾊﾟﾗﾒｰﾀに対して処理実行
			//var results = executes;

			//if (results.Contains(false))
			//{
			//	// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
			//	Console.ReadLine();
			//}
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

				using (var process = Process.Start(info))
				{
					if (process == null) break;

					for (string? s; (s = await process.StandardOutput.ReadLineAsync()) != null;)
					{
						Console.WriteLine(s);
					}

					process.WaitForExit();
				}
			}

			// ZIPﾌｧｲﾙを解凍する。
			ZipUtil.ExtractToDirectory(htmlz);

			// ZIPﾌｧｲﾙは不要なので削除
			FileUtil.Delete(htmlz);

			// 以後はﾌｫﾙﾀﾞに対して処理する
			htmlz = FileUtil.GetFullPathWithoutExtension(htmlz);

			// ｽﾀｲﾙｼｰﾄの上書き
			await FileUtil.CopyAsync(Directories.GetAbsolutePath("style.css"), Path.Combine(htmlz, "style.css"));

			var srcpdf = Path.Combine(htmlz, "index.html");
			var dstpdf = Path.Combine(AppSetting.Instance.OutputDir, withoutextension + ".pdf");

			await HeadlessWebView2.Call(new Uri(srcpdf), webview2 =>
			{
				return webview2.CoreWebView2.PrintToPdfAsync(dstpdf);
			});

			return true;
		}

		private static IEnumerable<string> GetFiles(string dir)
		{
			var targets = new[] { "*.azw", "*.azw3" }
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
	}
}