using System.Diagnostics;
using System.Text.RegularExpressions;
using TBird.Core;

namespace EBook2PDF
{
	internal class Process
	{
		public static async Task Execute(string[] args)
		{
			var executes = args.AsParallel().Select(async arg =>
			{
				return await Execute(arg);
			});

			// 実行ﾊﾟﾗﾒｰﾀに対して処理実行
			var results = await Task.WhenAll(executes);

			if (results.Contains(false))
			{
				// ｴﾗｰがあったらｺﾝｿｰﾙを表示した状態で終了する。
				Console.ReadLine();
			}
		}

		private static async Task<bool> Execute(string arg)
		{
			var customcss = Path.Combine(Directories.RootDirectory, "custom.css");

			var psi = new[]
			{
				// epub形式に変換
				new ProcessStartInfo()
				{
					FileName = AppSetting.Instance.Calibre,
					CreateNoWindow = true,
					Arguments = $"\"{arg}\" \"{FileUtil.GetFullPathWithoutExtension(arg)}.epub\"",
					UseShellExecute = false
				},
				// htmlz形式に変換
				new ProcessStartInfo()
				{
					FileName = AppSetting.Instance.Calibre,
					CreateNoWindow = true,
					Arguments = $"\"{arg}\" \"{FileUtil.GetFullPathWithoutExtension(arg)}.htmlz\" --extra-css=\"{customcss}\"",
					UseShellExecute = false
				},
				// pdf形式に変換
				new ProcessStartInfo()
				{
					FileName = AppSetting.Instance.Calibre,
					CreateNoWindow = true,
					Arguments = $"\"{arg}\" \"{FileUtil.GetFullPathWithoutExtension(arg)}.pdf\" --extra-css=\"{customcss}\"",
					UseShellExecute = false
				}
			};

			await psi.AsParallel().Select(x => Task.Run(() =>
			{
				CoreUtil.Execute(x);
			})).WhenAll();

			return true;
		}
	}
}