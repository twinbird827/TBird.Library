using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.ExceptionServices;
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
			for (var i = 0; i < pis.Length; i++)
			{
				var pi = pis[i];
				pi.CreateNoWindow = true;
				pi.UseShellExecute = false;
				pi.RedirectStandardInput = process != null;
				// 最終ﾌﾟﾛｾｽの stdout は誰も読まないため redirect しない（redirect するとﾊﾟｲﾌﾟ満杯や
				// 孫ﾌﾟﾛｾｽの handle 継承で WaitForExit が返らなくなりうる）。
				pi.RedirectStandardOutput = i < pis.Length - 1;
				// Redirect* を false にする段は対の *Encoding も null にする
				// （encoding 設定済み＋redirect=false は Process.Start が InvalidOperationException で拒否するため）。
				if (!pi.RedirectStandardOutput) pi.StandardOutputEncoding = null;
				// stderr は誰も読まない経路のため強制 false（redirect すると子がﾊﾟｲﾌﾟ満杯で write ﾌﾞﾛｯｸしうる）。
				pi.RedirectStandardError = false;
				pi.StandardErrorEncoding = null;

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

		/// <summary>
		/// EOF 待ちの上限。子ﾌﾟﾛｾｽが stdout handle を継承した孫ﾌﾟﾛｾｽを残すと EOF が成立しないため、
		/// exit 後この時間で読み取りを打ち切り、受信済み分＋警告で続行する（無限ﾌﾞﾛｯｸ回避）。
		/// </summary>
		private static readonly TimeSpan EofGrace = TimeSpan.FromSeconds(15);

		public static async Task<int> ExecuteAsync(ProcessStartInfo info, Action<string>? action)
		{
			using (var process = new Process { StartInfo = info, EnableRaisingEvents = true })
			{
				// 引数なし WaitForExit() は BeginOutputReadLine 使用時に EOF まで内包待機するため使わない。
				// exit は Exited ｲﾍﾞﾝﾄ、EOF は OutputDataReceived の null 発火で分離して待つ。
				var exitTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				var eofTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
				process.Exited += (_, _) => exitTcs.TrySetResult(true);

				var redirect = info.RedirectStandardOutput;
				var drainError = info.RedirectStandardError;
				Exception? err = null;
				if (redirect)
				{
					process.OutputDataReceived += (_, e) =>
					{
						if (e.Data == null) { eofTcs.TrySetResult(true); return; }
						// action の例外は最初の 1 件のみ捕捉し try 末尾で再ｽﾛｰする
						// （threadpool ｺｰﾙﾊﾞｯｸ上の未処理例外はｱﾌﾟﾘごと落とすため）。
						if (err == null) try { action?.Invoke(e.Data); } catch (Exception ex) { err = ex; }
					};
				}

				if (!process.Start()) return -1;
				if (redirect) process.BeginOutputReadLine();
				// stderr が redirect されていると誰も読まず子がﾊﾟｲﾌﾟ満杯で write ﾌﾞﾛｯｸするため読み捨てる。
				if (drainError) process.BeginErrorReadLine();
				// 誰も書かない stdin を即時ｸﾛｰｽﾞして子へ EOF を通知し、stdin 読取での無限ﾌﾞﾛｯｸを防ぐ。
				if (info.RedirectStandardInput) process.StandardInput.Close();

				try
				{
					await exitTcs.Task.ConfigureAwait(false);

					if (redirect && await Task.WhenAny(eofTcs.Task, Task.Delay(EofGrace)).ConfigureAwait(false) != eofTcs.Task)
					{
						MessageService.Warn($"{info.FileName}: stdout の EOF 待ちを {EofGrace.TotalSeconds:0} 秒で打ち切りました。孫ﾌﾟﾛｾｽが stdout を保持している可能性があり、出力末尾が欠落しえます（受信済み分で続行）。");
					}
					if (err != null) ExceptionDispatchInfo.Capture(err).Throw();
					return process.ExitCode;
				}
				finally
				{
					if (redirect) process.CancelOutputRead();
					if (drainError) process.CancelErrorRead();
				}
			}
		}

		public static int Execute(ProcessStartInfo info, Action<string>? action)
		{
			return ExecuteAsync(info, action).GetAwaiter().GetResult();
		}

	}
}