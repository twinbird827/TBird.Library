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
		/// EOF 待ちの上限。子ﾌﾟﾛｾｽが stdout handle を継承した孫ﾌﾟﾛｾｽを残すと EOF が成立しないため、
		/// exit 後この時間で読み取りを打ち切り、受信済み分＋警告で続行する（無限ﾌﾞﾛｯｸ回避）。
		/// </summary>
		private static readonly TimeSpan EofGrace = TimeSpan.FromSeconds(15);

		/// <summary>
		/// ﾌﾟﾛｾｽを実行し、exit code を返します。
		/// </summary>
		/// <param name="info">ﾌﾟﾛｾｽ実行情報</param>
		/// <param name="action">stdout を redirect した場合に 1 行ずつ渡す処理</param>
		/// <returns>exit code</returns>
		/// <remarks>
		/// 非ｾﾞﾛ exit code では throw せず、exit code をそのまま返す。検査は呼び出し元の義務。
		/// <paramref name="action"/> の中で出た例外は最初の 1 件だけを捕捉し、終了後に再ｽﾛｰする。それ以降の出力行は <paramref name="action"/> に渡らない。
		/// 起動失敗（実行ﾌｧｲﾙが無い等）は <see cref="System.ComponentModel.Win32Exception"/> が伝播する。
		/// -1 は UseShellExecute=true で既存ﾌﾟﾛｾｽが再利用され、待つﾌﾟﾛｾｽが無いときだけ返り、実際の exit code -1 と区別できない。
		/// </remarks>
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
	}
}