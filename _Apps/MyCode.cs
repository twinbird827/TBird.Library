using System.Diagnostics;
using System.Text.RegularExpressions;
using TBird.Core;
using TBird.IO.Html;
using TBird.IO.Pdf;

namespace DojinRename
{
	internal class MyCode
	{
		public static async Task Execute(string[] args)
		{
			if (args.Length != 1)
			{
				return;
			}

			var target = args[0];

			if (!await DirectoryUtil.Exists(target))
			{
				return;
			}

			DirectoryUtil.GetFiles(target).ForEach(Execute);
		}

		private static void Execute(string full)
		{
			var removes = new[]
			{
				@"\[[^\]]+\]",
				@"\([^\)]+\)",
				@"[ 　]+$",
				@"^[ 　]+"
			};

			var dir = Path.GetDirectoryName(full).NotNull();
			var src = FileUtil.GetFileNameWithoutExtension(full).NotNull();
			var ext = Path.GetExtension(full).NotNull();
			var dst = removes.Aggregate(src, (val, pattern) => Regex.Replace(val, pattern, ""));

			FileUtil.Move(Path.Combine(dir, src) + ext, Path.Combine(dir, dst) + ext, false);
		}

	}
}