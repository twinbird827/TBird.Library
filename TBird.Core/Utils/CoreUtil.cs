using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
		/// <exception cref="InvalidOperationException">いずれかの段が非ｾﾞﾛの exit code で終了した場合</exception>
		public static void Execute(params ProcessStartInfo[] pis)
		{
			var processes = new List<Process>();
			try
			{
				for (var i = 0; i < pis.Length; i++)
				{
					var pi = pis[i];
					pi.CreateNoWindow = true;
					pi.UseShellExecute = false;
					pi.RedirectStandardInput = 0 < i;
					// 最終ﾌﾟﾛｾｽの stdout は誰も読まないため redirect しない（redirect するとﾊﾟｲﾌﾟ満杯や
					// 孫ﾌﾟﾛｾｽの handle 継承で WaitForExit が返らなくなりうる）。
					pi.RedirectStandardOutput = i < pis.Length - 1;
					// Redirect* を false にする段は対の *Encoding も null にする
					// （encoding 設定済み＋redirect=false は Process.Start が InvalidOperationException で拒否するため）。
					if (!pi.RedirectStandardOutput) pi.StandardOutputEncoding = null;
					// stderr は誰も読まない経路のため強制 false（redirect すると子がﾊﾟｲﾌﾟ満杯で write ﾌﾞﾛｯｸしうる）。
					pi.RedirectStandardError = false;
					pi.StandardErrorEncoding = null;

					processes.Add(Process.Start(pi));

					if (0 < i)
					{
						// 段間はﾃﾞｺｰﾄﾞせずﾊﾞｲﾄ列のまま中継する。netstandard2.0 では stdin 側の符号化を指定できず
						// ｺﾝｿｰﾙ既定ｺｰﾄﾞﾍﾟｰｼﾞ（日本語 Windows では cp932）固定になるため、文字列を経由すると
						// 表現できない文字が '?' へ劣化する（issue #201）。
						// StandardInput(StreamWriter) ではなく BaseStream を閉じるのは、StreamWriter の Dispose が
						// encoding 次第で preamble(BOM) を書きうるため。
						// 3 段以上では中間段の stdout(Windows 匿名ﾊﾟｲﾌﾟ既定≒4KB)が先に満杯になり、親は次段へ
						// 進めず中間段は書けない相互ﾃﾞｯﾄﾞﾛｯｸになる（回復経路なし）。段ごとの直列中継は現行踏襲。
						// 前段 stdout は StreamReader 経由では読まない（ﾊﾞｯﾌｧに横取りさせない）が、
						// Process.Dispose は getter 取得済み(SyncMode)の StandardOutput を閉じないため using で受ける。
						using (var prev = processes[i - 1].StandardOutput)
						{
							// using(input) を try の内側に置くこと: BaseStream は 4096B ﾊﾞｯﾌｧ付き FileStream で、
							// 書いた末尾の端数は Dispose(flush) まで実際のﾊﾟｲﾌﾟへ出ない。次段が先に死んでいると
							// flush 側で IOException が出るため、try の外だと握り潰しをすり抜けて呼び出し元へ漏れる。
							try
							{
								using (var input = processes[i].StandardInput.BaseStream)
								{
									prev.BaseStream.CopyTo(input);
								}
							}
							catch (IOException)
							{
								// 次段が先に終了するとﾊﾟｲﾌﾟが壊れて write が失敗する。真因は下の exit code 検査で報告する。
								// 中断した前段 stdout は EOF まで捨て読みする。読み手が居ないと前段がﾊﾟｲﾌﾟ満杯で
								// write ﾌﾞﾛｯｸし続け、下の WaitForExit が永久に返らない。
								try
								{
									prev.BaseStream.CopyTo(Stream.Null);
								}
								catch (IOException)
								{
									// 前段側も既に壊れていれば捨て読みは不要。catch 内から例外を漏らさない。
								}
							}
						}
					}
				}

				// 中間段も含め全段の終了を待ち、非ｾﾞﾛ終了を握り潰さない（silent failure 防止, issue #201）。
				processes.ForEach(x => x.WaitForExit());

				var failed = processes.FindIndex(x => x.ExitCode != 0);
				if (0 <= failed) throw new InvalidOperationException(
					$"{pis[failed].FileName} が exit code {processes[failed].ExitCode} で失敗しました（{failed + 1}/{pis.Length} 段目）。");
			}
			finally
			{
				// 途中段の Process.Start が失敗しても起動済みの前段は kill しない（異常終了ﾊﾟｽのため現行踏襲）。
				// ここは Dispose のみで完全な後始末ではない。
				processes.ForEach(x => x.Dispose());
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