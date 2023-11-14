using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using TBird.Core;

namespace EBook2PDF
{
	internal class MyCode
	{
		public static async Task Execute(string[] args)
		{
			await Task.Delay(1);

			var executes = args.SelectMany(GetFiles).AsParallel().Select(Execute);

			// 実行ﾊﾟﾗﾒｰﾀに対して処理実行
			var results = executes;

			if (results.Contains(false))
			{
				// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
				Console.ReadLine();
			}
		}

		private static bool Execute(string file)
		{
			//var builder = new StringBuilder();
			//var format = $"\"{AppSetting.Instance.Calibre}\" \"{file}\" \"{{0}}\"";
			var epub = FileUtil.GetFullPathWithoutExtension(file) + ".epub";
			var htmlz = FileUtil.GetFullPathWithoutExtension(file) + ".htmlz";

			//builder.AppendLine(string.Format(format, epub));
			//builder.AppendLine(string.Format(format, htmlz));

			//var tmpfile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".bat");

			//File.AppendAllText(tmpfile, builder.ToString());

			//CoreUtil.Execute(new ProcessStartInfo()
			//{
			//	FileName = tmpfile
			//});

			//FileUtil.Delete(tmpfile);

			var pis = new[]
			{
				new ProcessStartInfo()
				{
					Arguments = $"\"{file}\" \"{epub}\"",
				},
				new ProcessStartInfo()
				{
					Arguments = $"\"{file}\" \"{htmlz}\"",
				}
			};

			pis.ForEach(x =>
			{
				x.WorkingDirectory = Path.GetDirectoryName(AppSetting.Instance.Calibre);
				x.FileName = AppSetting.Instance.Calibre;
				x.UseShellExecute = false;
				x.CreateNoWindow = true;
				x.RedirectStandardOutput = true;

				using (var process = Process.Start(x)) if (process != null)
				{
					Console.WriteLine(process.StandardOutput.ReadToEnd());
					process.WaitForExit();
				}

			});

			ZipUtil.ExtractToDirectory(htmlz);

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