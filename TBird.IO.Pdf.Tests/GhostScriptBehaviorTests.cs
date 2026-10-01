using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;

namespace TBird.IO.Pdf.Tests
{
	/// <summary>
	/// 同梱 gsdll64.dll の挙動を固定する特性ﾃｽﾄ(issue #227)。
	/// 期待値は GhostScript 10.08.0 の実測値で、正しさではなく DLL 差し替え時の変化を検出する。
	/// 中間 /Pages の /Count 破損でﾍﾟｰｼﾞが落ちる既知の制限(TBird.IO.Pdf/CLAUDE.md)も現行の値として固定する。
	/// </summary>
	public class GhostScriptBehaviorTests
	{
		// 引数: ﾌｨｸｽﾁｬ名 / GetPageSize / Pdf2Jpg の成否 / 出力 JPEG 枚数 / PutPageNumberAsync の成否(成功時のみ原本を置換) / 置換後の GetPageSize
		// null は GetPageSize が InvalidOperationException、JPEG 枚数では出力ﾌｫﾙﾀﾞ無しを表す。

		// gs pdfwrite 出力の 7 ﾍﾟｰｼﾞ単層
		[TestCase("src7", 7, true, 7, true, 7)]
		// 根 /Count 7、子 A 2 葉 /Count 2、子 B 5 葉 /Count 5
		[TestCase("ok7", 7, true, 7, true, 7)]
		// src7 の根 /Count 7 → /Count 5(同ﾊﾞｲﾄ長置換)
		[TestCase("b_under", 7, true, 7, true, 7)]
		// src7 の根 /Count 7 → /Count 9
		[TestCase("b_over", 7, true, 7, true, 7)]
		// src7 の根 /Count 7 → /Count 0
		[TestCase("b_zero", 7, true, 7, true, 7)]
		// src7 の根 /Count 7 → /Xount 7(/Count 欠落)
		[TestCase("b_missing", null, false, null, false, null)]
		// ok7 の子 B を /Count 3
		[TestCase("mid_under", 5, true, 5, true, 5)]
		// ok7 の子 B を /Count 8
		[TestCase("mid_over", 10, false, 7, true, 7)]
		// ok7 の子 A を /Count 1
		[TestCase("a_under", 6, true, 6, true, 6)]
		public async Task FixtureBehavior(string fixture, int? pagesize, bool pdf2jpg, int? jpegs, bool put, int? pagesizeAfterPut)
		{
			var src = Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", fixture + ".pdf");
			var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, fixture);

			// Pdf2Jpg は前回の JPEG が残ると枚数が増え、PutPageNumber は原本を置換するため、毎回作り直して別ｺﾋﾟｰで処理する。
			if (Directory.Exists(work)) Directory.Delete(work, true);
			var jpgpdf = CopyTo(src, Path.Combine(work, "jpg"));
			var putpdf = CopyTo(src, Path.Combine(work, "put"));

			Assert.That(await GetPageSizeOrNull(jpgpdf), Is.EqualTo(pagesize));

			Assert.That(await Succeeds(() => PdfUtil.Pdf2Jpg(jpgpdf, 3, 36)), Is.EqualTo(pdf2jpg));
			var jpgdir = Path.Combine(Path.GetDirectoryName(jpgpdf)!, fixture);
			Assert.That(Directory.Exists(jpgdir) ? Directory.GetFiles(jpgdir, "*.jpeg").Length : (int?)null, Is.EqualTo(jpegs));

			var original = File.ReadAllBytes(putpdf);
			Assert.That(await Succeeds(() => PdfUtil.PutPageNumberAsync(putpdf)), Is.EqualTo(put));
			Assert.That(File.ReadAllBytes(putpdf), put ? Is.Not.EqualTo(original) : Is.EqualTo(original));
			Assert.That(await GetPageSizeOrNull(putpdf), Is.EqualTo(pagesizeAfterPut));
		}

		private static string CopyTo(string src, string dir)
		{
			var dst = Path.Combine(Directory.CreateDirectory(dir).FullName, Path.GetFileName(src));
			File.Copy(src, dst);
			return dst;
		}

		// 子ﾌﾟﾛｾｽ経由(利用ｱﾌﾟﾘと同じ経路)のﾍﾟｰｼﾞ数。InvalidOperationException は null に畳み、他の例外はﾃｽﾄを落とす。
		private static async Task<int?> GetPageSizeOrNull(string pdffile)
		{
			try
			{
				return await new PdfUtilExecutor().GetPageSize(pdffile);
			}
			catch (InvalidOperationException)
			{
				return null;
			}
		}

		private static async Task<bool> Succeeds(Func<Task> action)
		{
			try
			{
				await action();
				return true;
			}
			catch (InvalidOperationException)
			{
				return false;
			}
		}
	}
}
