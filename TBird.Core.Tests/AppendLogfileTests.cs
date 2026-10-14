using System;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace TBird.Core.Tests
{
	public class AppendLogfileTests
	{
		// #279: ﾛｸﾞを exe のﾌｫﾙﾀﾞでなく PathSetting.RootDirectory 配下の log へ書くことを固定する。

		[Test]
		public void WritesUnderRootDirectory()
		{
			var root = Path.Combine(TestContext.CurrentContext.WorkDirectory, "appendlogfile");
			if (Directory.Exists(root)) Directory.Delete(root, true);
			var marker = Guid.NewGuid().ToString();
			var old = PathSetting.Instance.RootDirectory;

			try
			{
				PathSetting.Instance.RootDirectory = root;
				MessageService.AppendLogfile(marker);
			}
			finally
			{
				PathSetting.Instance.RootDirectory = old;
			}

			var logs = Directory.Exists(Path.Combine(root, "log"))
				? Directory.GetFiles(Path.Combine(root, "log"), "*.log")
				: Array.Empty<string>();
			Assert.That(logs.Any(x => File.ReadAllText(x).Contains(marker)), Is.True);
		}
	}
}
