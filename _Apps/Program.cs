using System.Text;
using System.Text.RegularExpressions;
using ImageMagick;

namespace WhiteCopy;

internal static class Program
{
	/// <summary>処理対象とする拡張子（小文字・ドット付き）</summary>
	private static readonly HashSet<string> TargetExtensions = new(StringComparer.OrdinalIgnoreCase)
	{
		".jpg", ".jpeg", ".png", ".webp", ".bmp", ".gif", ".tif", ".tiff"
	};

	/// <summary>「... - copy」「... - copy (2)」を検出して二重処理を防ぐ</summary>
	private static readonly Regex BackupNamePattern =
		new(@" - copy( \(\d+\))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

	private enum Result
	{ Ok, Skipped, Failed }

	[STAThread]
	private static int Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;

		if (args.Length == 0)
		{
			Console.WriteLine("画像ファイル（またはフォルダ）をこの exe にドラッグ＆ドロップしてください。");
			Console.WriteLine("対象拡張子: " + string.Join(", ", TargetExtensions));
			Pause();
			return 1;
		}

		var files = ExpandArguments(args);
		int ok = 0, skip = 0, ng = 0;

		foreach (var path in files)
		{
			switch (ProcessOne(path))
			{
				case Result.Ok: ok++; break;
				case Result.Skipped: skip++; break;
				default: ng++; break;
			}
		}

		Console.WriteLine();
		Console.WriteLine($"完了: 成功 {ok} 件 / スキップ {skip} 件 / 失敗 {ng} 件");
		//Pause();
		return ng == 0 ? 0 : 2;
	}

	private static Result ProcessOne(string path)
	{
		var name = Path.GetFileName(path);

		if (!File.Exists(path))
		{
			Console.WriteLine($"[スキップ] ファイルが見つかりません: {name}");
			return Result.Skipped;
		}

		var ext = Path.GetExtension(path);
		if (!TargetExtensions.Contains(ext))
		{
			Console.WriteLine($"[スキップ] 対象外の拡張子: {name}");
			return Result.Skipped;
		}

		var stem = Path.GetFileNameWithoutExtension(path);
		if (BackupNamePattern.IsMatch(stem))
		{
			Console.WriteLine($"[スキップ] バックアップと思われるファイル: {name}");
			return Result.Skipped;
		}

		var dir = Path.GetDirectoryName(Path.GetFullPath(path))!;

		// --- 1) 先にバックアップを作る（失敗したら元ファイルには一切触れない） ---
		string backup = UniquePath(dir, $"{stem} - copy", ext);
		try
		{
			File.Copy(path, backup, overwrite: false);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"[失敗] コピーできませんでした: {name}（元ファイルは変更していません） / {ex.Message}");
			return Result.Failed;
		}

		// --- 2) 元ファイルを白一色で上書き ---
		// 一時ファイルへ書いてから置換。途中で落ちても元ファイルは無傷。
		string temp = Path.Combine(dir, $"{Guid.NewGuid():N}.whiteout.tmp");
		try
		{
			using (var image = new MagickImage(path))
			{
				image.BackgroundColor = MagickColors.White;
				image.Alpha(AlphaOption.Remove);          // 透過部分を白で埋める
				image.Alpha(AlphaOption.Off);             // アルファチャンネル自体を破棄
				image.Colorize(MagickColors.White, new Percentage(100)); // 全画素を白へ
				image.Write(temp, image.Format);          // 元と同じフォーマットで書き出す
			}

			File.Move(temp, path, overwrite: true);
			Console.WriteLine($"[OK]   {name} → 白塗り完了（バックアップ: {Path.GetFileName(backup)}）");
			return Result.Ok;
		}
		catch (Exception ex)
		{
			TryDelete(temp);
			Console.WriteLine($"[失敗] {name}: {ex.Message}");
			return Result.Failed;
		}
	}

	/// <summary>"name - copy.ext" が既にあれば "name - copy (2).ext" … と連番を振る</summary>
	private static string UniquePath(string dir, string stem, string ext)
	{
		var candidate = Path.Combine(dir, stem + ext);
		int i = 2;
		while (File.Exists(candidate))
		{
			candidate = Path.Combine(dir, $"{stem} ({i}){ext}");
			i++;
		}
		return candidate;
	}

	/// <summary>フォルダが渡された場合は直下の対象画像に展開する</summary>
	private static List<string> ExpandArguments(IEnumerable<string> args)
	{
		var list = new List<string>();
		foreach (var a in args)
		{
			if (Directory.Exists(a))
			{
				list.AddRange(Directory
					.EnumerateFiles(a, "*", SearchOption.TopDirectoryOnly)   // 再帰したければ AllDirectories
					.Where(f => TargetExtensions.Contains(Path.GetExtension(f)))
					.OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
			}
			else
			{
				list.Add(a);
			}
		}
		return list;
	}

	private static void TryDelete(string path)
	{
		try { if (File.Exists(path)) File.Delete(path); } catch { /* 無視 */ }
	}

	private static void Pause()
	{
		if (Console.IsInputRedirected) return;
		Console.WriteLine("何かキーを押すと終了します...");
		try { Console.ReadKey(true); } catch { /* コンソールが無い環境 */ }
	}
}