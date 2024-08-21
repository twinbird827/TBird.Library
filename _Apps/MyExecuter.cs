using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TBird.Console;
using TBird.Core;
using TBird.IO.Img;

namespace ZIPConverter
{
	public class MyExecuter : ConsoleAsyncExecuter
	{
		protected override Dictionary<string, string> GetOptions(Dictionary<string, string> options)
		{
			SetOption(options, "O", AppSetting.Instance.Option,
				$"起動ｵﾌﾟｼｮﾝを選択してください。",
				$"0: 全て実行する。",
				$"1: 画像縮小をｽｷｯﾌﾟする。",
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
			var argdir = Path.GetDirectoryName(arg);
			var argfile = Path.GetFileName(arg);

			if (argdir is null || argfile is null)
			{
				return true;
			}

			// 作業用ﾃﾞｨﾚｸﾄﾘﾊﾟｽ
			var tmpdir = Path.GetTempPath();
			// 画像変換のための一時ﾃﾞｨﾚｸﾄﾘ
			var tmp1 = Path.Combine(tmpdir, Guid.NewGuid().ToString());
			// zip圧縮用ﾌｧｲﾙ名
			var tmp2 = Path.Combine(tmpdir, argfile);
			var zip = tmp2 + ".zip";

			try
			{
				MessageService.Info("***** 開始:" + arg);

				DirectoryUtil.Create(tmp1);

				MessageService.Info("終了(作業用ﾃﾞｨﾚｸﾄﾘ作成):" + arg);

				// 対象ﾌｧｲﾙを作業用ﾃﾞｨﾚｸﾄﾘにｺﾋﾟｰ
				var copyfiles = GetFiles(arg).Select(async (x, i) =>
				{
					var dst = Path.Combine(tmp1, i.ToString(8) + Path.GetExtension(x));
					await FileUtil.CopyAsync(x, dst);
				});
				await Task.WhenAll(copyfiles);

				MessageService.Info("終了(作業ﾌｧｲﾙｺﾋﾟｰ):" + arg);

				if (option == 0)
				{
					if (!await ResizeJPG(tmp1))
					{
						MessageService.Info("異常(ResizeJPG):" + arg);
						return false;
					}

					MessageService.Info("終了(ResizeJPG):" + arg);
				}

				// ﾌｧｲﾙ名を連番にする。
				DirectoryUtil.OrganizeNumber(tmp1);

				// 元のﾃﾞｨﾚｸﾄﾘ名に変更
				DirectoryUtil.Move(tmp1, tmp2);

				// zip圧縮
				ZipUtil.CreateFromDirectory(tmp2, zip);

				MessageService.Info("終了(ZIP):" + arg);

				// 元の場所に移動
				FileUtil.Move(zip, Path.Combine(argdir, Path.GetFileName(zip)));

				MessageService.Info("終了(移動):" + arg);

				// 作業用ﾃﾞｨﾚｸﾄﾘ削除
				DirectoryUtil.Delete(tmp1);
				DirectoryUtil.Delete(tmp2);

				// 元のﾃﾞｨﾚｸﾄﾘ削除
				DirectoryUtil.Delete(arg);

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
				// 作業用ﾃﾞｨﾚｸﾄﾘ削除
				DirectoryUtil.Delete(tmp1);
				DirectoryUtil.Delete(tmp2);
			}
		}

		private static IEnumerable<string> GetFiles(string dir)
		{
			var targets = OrderBy(DirectoryUtil.GetFiles(dir))
				.Select(OrganizeExtension)
				.Where(x => x != null)
				.OfType<string>()
				.Where(x => !AppSetting.Instance.IgnoreFiles.Contains(Path.GetExtension(x)));

			foreach (var f in targets)
			{
				yield return f;
			}

			var children = OrderBy(DirectoryUtil.GetDirectories(dir))
				.Where(x => !AppSetting.Instance.IgnoreDirectories.Contains(Path.GetFileName(x)))
				.SelectMany(GetFiles);

			foreach (var f in children)
			{
				yield return f;
			}
		}

		private static IEnumerable<string> OrderBy(string[] bases)
		{
			return bases.OrderBy(x =>
			{
				x = Regex.Replace(x, @"^[a-zA-Z]*cover[a-zA-Z]*", m => "!!!");
				x = Regex.Replace(x, @"[0-9]{1,8}", m => string.Format("{0,0:D8}", long.Parse(m.Value)));
				return x;
			});
		}

		private static string? OrganizeExtension(string file)
		{
			var src = Path.GetExtension(file);
			var dst = ImgUtil.GetEncodedExtension(file);

			if (dst == null)
			{
				return null;
			}

			if (src.ToLower() != dst.ToLower())
			{
				var tmp = $"{FileUtil.GetFullPathWithoutExtension(file)}{dst}";
				FileUtil.Move(file, tmp);
				return tmp;
			}
			else
			{
				return file;
			}

		}

		private static async Task<bool> ResizeJPG(string dir)
		{
			return await DirectoryUtil.GetFiles(dir).AsParallel().Select(async x =>
			{
				await _sematmp.WaitAsync();

				using (new Disposer<SemaphoreSlim>(_sematmp, arg => arg.Release()))
				{
					ImgUtil.ResizeUnder(x, AppSetting.Instance.Width, AppSetting.Instance.Height, AppSetting.Instance.Quality);
				}
			}).WhenAll().TryCatch();
		}

		public static SemaphoreSlim _sematmp = new SemaphoreSlim(AppSetting.Instance.ParallelCount, AppSetting.Instance.ParallelCount);

	}
}
