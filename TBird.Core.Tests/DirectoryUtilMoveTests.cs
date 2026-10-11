using System.IO;
using NUnit.Framework;

namespace TBird.Core.Tests
{
	public class DirectoryUtilMoveTests
	{
		// #243: 移動に失敗しても移動先の既存ﾌｫﾙﾀﾞを失わず、退避ﾌｫﾙﾀﾞも残さないことを固定する。

		[Test]
		public void MoveOverwritesExistingDestination()
		{
			var (src, dst) = CreateWork("overwrite");

			DirectoryUtil.Move(src, dst);

			Assert.That(Directory.Exists(src), Is.False);
			Assert.That(Directory.GetFileSystemEntries(dst), Is.EqualTo(new[] { Path.Combine(dst, "src.txt") }));
			Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(dst)!), Is.EqualTo(new[] { dst }));
		}

		[Test]
		public void MoveRestoresDestinationOnFailure()
		{
			var (src, dst) = CreateWork("restore");
			Directory.Delete(src, true);

			Assert.That(() => DirectoryUtil.Move(src, dst), Throws.InstanceOf<IOException>());

			Assert.That(Directory.GetFileSystemEntries(dst), Is.EqualTo(new[] { Path.Combine(dst, "dst.txt") }));
			Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(dst)!), Is.EqualTo(new[] { dst }));
		}

		[Test]
		public void MoveWithoutOverwriteKeepsBoth()
		{
			var (src, dst) = CreateWork("nooverwrite");

			Assert.That(() => DirectoryUtil.Move(src, dst, false), Throws.InstanceOf<IOException>());

			Assert.That(Directory.GetFileSystemEntries(src), Is.EqualTo(new[] { Path.Combine(src, "src.txt") }));
			Assert.That(Directory.GetFileSystemEntries(dst), Is.EqualTo(new[] { Path.Combine(dst, "dst.txt") }));
			Assert.That(Directory.GetFileSystemEntries(Path.GetDirectoryName(dst)!), Is.EqualTo(new[] { dst }));
		}

		// 退避ﾌｫﾙﾀﾞの残りを確かめるため、移動先は専用の親ﾌｫﾙﾀﾞに置く。
		private static (string src, string dst) CreateWork(string name)
		{
			var work = Path.Combine(TestContext.CurrentContext.WorkDirectory, "move", name);
			if (Directory.Exists(work)) Directory.Delete(work, true);
			var src = Directory.CreateDirectory(Path.Combine(work, "src")).FullName;
			var dst = Directory.CreateDirectory(Path.Combine(work, "parent", "dst")).FullName;
			File.WriteAllText(Path.Combine(src, "src.txt"), "src");
			File.WriteAllText(Path.Combine(dst, "dst.txt"), "dst");
			return (src, dst);
		}
	}
}
