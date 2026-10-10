using System.Runtime.InteropServices;
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

	private static int Main(string[] args)
	{
		Console.OutputEncoding = Encoding.UTF8;

		if (args.Length == 0 || (args.Length > 1 && args.Any(a => !Directory.Exists(a))))
		{
			Console.WriteLine("画像ファイル 1 つ、またはフォルダ（複数可）をこの exe にドラッグ＆ドロップしてください。");
			Console.WriteLine("ファイルの複数指定、ファイルとフォルダの混在はできません。");
			Console.WriteLine("フォルダは直下の対象画像のうち、名前順で 2 番目だけを白塗りします。");
			Console.WriteLine("対象拡張子: " + string.Join(", ", TargetExtensions));
			Pause();
			return 1;
		}

		int ok = 0, skip = 0, ng = 0;

		foreach (var arg in args)
		{
			switch (ProcessArgument(arg))
			{
				case Result.Ok: ok++; break;
				case Result.Skipped: skip++; break;
				default: ng++; break;
			}
		}

		Console.WriteLine();
		Console.WriteLine($"完了: 成功 {ok} 件 / スキップ {skip} 件 / 失敗 {ng} 件");
		if (ng > 0) Pause();   // 失敗理由を読ませたいときだけ止める。正常終了は即閉じ
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
				image.Strip();                            // EXIF 等のプロファイルを除去（サムネイル・GPS・撮影日時）
				image.Write(temp, image.Format);          // 元と同じフォーマットで書き出す
			}

			File.Move(temp, path, overwrite: true);
		}
		catch (Exception ex)
		{
			TryDelete(temp);
			TryDelete(backup);
			Console.WriteLine($"[失敗] {name}: {ex.Message}");
			return Result.Failed;
		}

		// 成功時処理は try の外（backup が残る ⇔ 白塗り成功、の不変条件を保つ）
		Console.WriteLine($"[OK]   {name} → 白塗り完了（バックアップ: {Path.GetFileName(backup)}）");
		return Result.Ok;
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

	/// <summary>フォルダは表紙と裏表紙の見開きを崩さないよう、エクスプローラー順で 2 番目の画像だけを処理する</summary>
	private static Result ProcessArgument(string arg)
	{
		if (!Directory.Exists(arg)) return ProcessOne(arg);

		// バックアップ名も母集団に含める: 再実行時は 2 番目がバックアップになり ProcessOne でスキップされる
		var second = Directory
			.EnumerateFiles(arg, "*", SearchOption.TopDirectoryOnly)
			.Where(f => TargetExtensions.Contains(Path.GetExtension(f)))
			.Order(Comparer<string>.Create(StrCmpLogicalW))
			.ElementAtOrDefault(1);

		if (second is null)
		{
			Console.WriteLine($"[スキップ] 対象画像が 2 枚未満のフォルダ: {Path.GetFileName(Path.TrimEndingDirectorySeparator(arg))}");
			return Result.Skipped;
		}
		return ProcessOne(second);
	}

	[DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
	private static extern int StrCmpLogicalW(string x, string y);

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