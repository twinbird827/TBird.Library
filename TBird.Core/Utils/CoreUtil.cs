using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace TBird.Core
{
	public static class CoreUtil
	{
		public static T[] Arr<T>(params T[] arr)
		{
			return arr;
		}

		/// <summary>
		/// 対象文字配列のうち最初の空文字以外の文字を取得します。
		/// </summary>
		/// <param name="args">対象文字配列</param>
		public static string Nvl(params string[] args)
		{
			return args.FirstOrDefault(s => !string.IsNullOrWhiteSpace(s)) ?? string.Empty;
		}

		/// <summary>
		/// 対象文字配列のうち最初のｾﾞﾛ以外の数値を取得します。
		/// </summary>
		/// <param name="args">対象文字配列</param>
		public static double Nvl(params double[] args)
		{
			return args.FirstOrDefault(s => s != 0);
		}

		public static string Nvl(params object[] args)
		{
			return Nvl(args.Select(x => x is string s ? s : x.ToString()).ToArray());
		}

		/// <summary>
		/// ﾌﾟﾛｾｽを実行します。実行するﾌﾟﾛｾｽが複数存在する場合ﾊﾟｲﾌﾟします。
		/// </summary>
		/// <param name="pis">ﾌﾟﾛｾｽ実行情報</param>
		public static void Execute(params ProcessStartInfo[] pis)
		{
			Process? process = null;
			foreach (var pi in pis)
			{
				pi.CreateNoWindow = true;
				pi.UseShellExecute = false;
				pi.RedirectStandardInput = process != null;
				pi.RedirectStandardOutput = true;

				var now = Process.Start(pi);

				if (process != null)
				{
					using (process)
					using (var reader = process.StandardOutput)
					using (var writer = now.StandardInput)
					{
						writer.AutoFlush = true;
						string line = reader.ReadToEnd();

						writer.Write(line);
					}
				}

				process = now;
			}

			if (process != null)
			{
				using (process)
				{
					process.WaitForExit();
				}
			}
		}

		public static async Task<int> ExecuteAsync(ProcessStartInfo info, Action<string> action)
		{
			using (var process = Process.Start(info))
			{
				if (process == null) return -1;

				if (action != null) for (string s; (s = await process.StandardOutput.ReadLineAsync()) != null;)
					{
						action(s);
					}

				process.WaitForExit();

				return process.ExitCode;
			}
		}

		public static int Execute(ProcessStartInfo info, Action<string> action)
		{
			using (var process = Process.Start(info))
			{
				if (process == null) return -1;

				if (action != null) for (string s; (s = process.StandardOutput.ReadLine()) != null;)
					{
						action(s);
					}

				process.WaitForExit();

				return process.ExitCode;
			}
		}

	}
}