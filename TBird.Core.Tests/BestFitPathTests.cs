using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace TBird.Core.Tests
{
	public class BestFitPathTests
	{
		[SetUp]
		public void SetUp()
		{
			Assume.That(GetACP(), Is.EqualTo(932), "ACP 932 でのみ再現する");
		}

		// #231 / #234: ﾊﾟｽが best-fit で é→e に置き換わると、cafe 側の別ﾌｧｲﾙ・別ﾃﾞｨﾚｸﾄﾘを黙って消すため、cafe 側が残ることを固定する。

		[Test]
		public void FileDeleteKeepsBestFitTwin()
		{
			var work = CreateWork("file");
			var cafe1 = Path.Combine(work, "café.pdf");
			var cafe2 = Path.Combine(work, "cafe.pdf");
			File.WriteAllText(cafe1, "1");
			File.WriteAllText(cafe2, "2");

			FileUtil.Delete(cafe1);

			Assert.That(File.Exists(cafe1), Is.False);
			Assert.That(File.Exists(cafe2) ? File.ReadAllText(cafe2) : null, Is.EqualTo("2"));
		}

		[Test]
		public void DirectoryDeleteKeepsBestFitTwin()
		{
			var work = CreateWork("directory");
			var cafe1 = Directory.CreateDirectory(Path.Combine(work, "café")).FullName;
			var cafe2 = Directory.CreateDirectory(Path.Combine(work, "cafe")).FullName;
			var inner = Path.Combine(cafe2, "inner.txt");
			File.WriteAllText(inner, "2");

			DirectoryUtil.Delete(cafe1);

			Assert.That(Directory.Exists(cafe1), Is.False);
			Assert.That(File.Exists(inner), Is.True);
		}

		private static string CreateWork(string name)
		{
			var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, "bestfit", name);
			if (Directory.Exists(work)) Directory.Delete(work, true);
			return Directory.CreateDirectory(work).FullName;
		}

		[DllImport("kernel32.dll")]
		private static extern int GetACP();
	}
}
