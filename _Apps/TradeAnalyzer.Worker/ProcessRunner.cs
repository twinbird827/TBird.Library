using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TradeAnalyzer.Worker;

/// <summary>プロセスが正常完了したときの結果。ExitCode≠0 も正常完了の一種として含む（致命判断は呼び手）。</summary>
internal readonly record struct ProcessResult(int ExitCode, string Stdout, string Stderr);

/// <summary>
/// 外部プロセスを起動し stdout/stderr を捕捉、stdin 供給・timeout kill・非同期リーダ EOF flush を一元化する
/// 共有起動点。ExitCode≠0 は throw せず <see cref="ProcessResult"/> で返す（致命/非致命ポリシーは各消費者に置く）。
/// throw するのは「プロセスがそもそも完了しなかった」場合のみ＝起動失敗（<see cref="InvalidOperationException"/>）・
/// timeout（<see cref="TimeoutException"/>）・外部 ct キャンセル（<see cref="OperationCanceledException"/> 再スロー）。
/// いずれも子プロセスを停止させてから throw する（孤児化回避）。run-today(uv run python) と 3b(claude -p) が共有する。
/// Redirect*/UseShellExecute は本クラスが強制し、FileName/ArgumentList/WorkingDirectory/Encoding/Environment は
/// 呼び手が psi に設定する（例: Python 経路の PYTHONUTF8=1、3b の StandardInputEncoding=UTF-8）。
/// </summary>
internal static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(
        ProcessStartInfo psi,
        ILogger logger,
        int timeoutMinutes,
        string? stdin = null,
        string stdoutLogPrefix = "[proc]",
        string stderrLogPrefix = "[proc:err]",
        string displayName = "プロセス",
        CancellationToken ct = default)
    {
        // redirect 設定は呼び手が忘れないようここで強制する。
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = stdin != null;

        // 実効タイムアウト。≤0 は 10 分にフォールバックし、CancelAfter と TimeoutException メッセージの双方で
        // 同じ値を使う（生の timeoutMinutes を表示すると ≤0 渡し時に「0 分でタイムアウト」等と誤報するため）。
        int effMin = timeoutMinutes > 0 ? timeoutMinutes : 10;

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        // StringBuilder(非スレッドセーフ) を *DataReceived の append と全読取（timeout/正常 両経路）で保護する。
        // StringBuilder インスタンス自身を lock 対象にする公開ロックを避け、専用オブジェクトを用意する。
        // stdout も stderr と同様に lock 保護する（3b の JSON エンベロープを torn-read させない）。
        object stdoutLock = new();
        object stderrLock = new();
        using var proc = new Process { StartInfo = psi };
        // ログレベルは stdout=Information / stderr=Warning で固定（現行 RunPythonAsync のレベルを保存）。
        // prefix は必ずテンプレ引数として渡す（文字列連結でメッセージテンプレートを動的化しない）。
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (stdoutLock) { stdout.AppendLine(e.Data); }
            logger.LogInformation("{Prefix} {Line}", stdoutLogPrefix, e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            if (e.Data == null) return;
            lock (stderrLock) { stderr.AppendLine(e.Data); }
            logger.LogWarning("{Prefix} {Line}", stderrLogPrefix, e.Data);
        };

        // 実行ファイル＋全引数＋cwd を保全する（診断情報の維持）。
        logger.LogInformation("{Name} 起動: {File} {Args} (cwd={Cwd})",
            displayName, psi.FileName, string.Join(' ', psi.ArgumentList), psi.WorkingDirectory);

        // UseShellExecute=false の Process.Start() は実行ファイル不在時 false を返さず Win32Exception を
        // throw する（false 判定は dead-code）。例外を捕捉し原因明示で包む。
        try
        {
            proc.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"プロセスを起動できません: {psi.FileName}（{displayName} の実行ファイルパスを確認）。", ex);
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(TimeSpan.FromMinutes(effMin));
        // BeginOutput/ErrorReadLine と CancelOutput/ErrorRead をペア化し購読寿命を明示する（finally）。
        // 正常終了経路では下の引数なし WaitForExit() の flush 完了までにハンドラが発火し切る。
        // timeout の bounded-drain false 経路（孫プロセス未終了で引数なし WaitForExit() を意図的にスキップ）では
        // 読取がアクティブなまま本 finally の Cancel で明示停止する。post-EOF/Kill 後でも _output/_error は
        // using Dispose まで非 null ゆえ Cancel は throw せず、伝播中の例外を握り潰さない。
        // ハンドラが参照する logger は DI 管理で本メソッドより寿命が長い。
        try
        {
            try
            {
                // stdin write は WaitForExitAsync と同じ try・同じ timeout スコープに置く: パイプ満杯で write が
                // ハングしても timeout で解除され、下の catch(OCE) が兼ねて kill＋TimeoutException 化まで到達する
                //（outer try 内に置くことで finally の Cancel*Read も必ず通る）。プロンプトは通常 < 64KB パイプ
                // バッファなので実際は即完了する。
                if (stdin != null)
                {
                    try
                    {
                        await proc.StandardInput.WriteAsync(stdin.AsMemory(), timeoutCts.Token).ConfigureAwait(false);
                        proc.StandardInput.Close(); // EOF を明示（閉じないと子が stdin 読み待ちで永久ブロックしうる）。
                    }
                    catch (IOException ex)
                    {
                        // broken pipe: 子が stdin を読まず先に終了したケース。write を放棄して WaitForExit へ進み
                        // ExitCode を取得する（プロセスは完了済みゆえここでは throw しない）。
                        logger.LogWarning(ex, "{Name}: stdin 書込みに失敗（子プロセスが先に終了した可能性）。", displayName);
                    }
                }
                await proc.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // フィルタなしで捕捉し、原因（timeout/外部 ct/理論上の spurious OCE）を問わずまず子を確実に停止する
                //（孤児化回避）。kill/flush の例外は握り潰さず innerException に連鎖させる（no-swallow）。
                Exception? killEx = null;
                try
                {
                    proc.Kill(entireProcessTree: true);
                    // WaitForExit(TimeSpan) は非同期 ErrorDataReceived の flush を保証しない（MS docs: WaitForExit(Int32)
                    // の Remarks。true を受けた後に引数なし WaitForExit() を呼べ）。5 秒内に終了済(true)のときだけ続けて
                    // 引数なし WaitForExit() で EOF flush を完了させる（終了済みなので即 return＝bounded 意図と両立）。
                    // false（孫プロセス wedge で 5 秒内に未終了）なら無限ブロック回避のため引数なし呼出を避け、
                    // 受信済み分のみ添付する。
                    if (proc.WaitForExit(TimeSpan.FromSeconds(5)))
                        proc.WaitForExit();
                }
                catch (Exception ex) { killEx = ex; logger.LogWarning(ex, "{Name}: 子プロセス kill/flush 失敗（既に終了済みの可能性）。", displayName); }

                if (timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    // timeout 由来。受信済み stderr を診断として例外へ添付する（トレースバック等の保持）。
                    string tail;
                    lock (stderrLock) { tail = stderr.ToString(); }
                    throw new TimeoutException(
                        $"{displayName} が {effMin} 分でタイムアウトしました\nstderr(タイムアウトまで):\n{tail}", killEx);
                }
                // それ以外＝実運用では外部 ct 由来（timeoutCts は linked ゆえ ct キャンセルでも発火する）。子は kill 済み。
                // kill 失敗は innerException 連鎖で顕在化させ、成功時は元 OCE を再スローしキャンセルを honor する。
                // この経路が必ず送出して終わることで、kill 済みプロセスの ProcessResult を返す事故を防ぐ。
                if (killEx != null)
                    throw new OperationCanceledException("キャンセル中の子プロセス kill に失敗", killEx, ct);
                throw;
            }

            // WaitForExitAsync は非同期 stdout/stderr リーダの EOF flush を保証しない（stderr 末尾＝トレースバック
            // 最重要行が欠落しうる）。引数なし同期 WaitForExit() を1回呼び、非同期バッファを完全 flush してから
            // ExitCode/出力を参照する（正常終了済みなので即時 return）。
            proc.WaitForExit();

            // 直前の引数なし WaitForExit() が非同期イベント処理の完了を保証するため並行 writer は不在だが、
            // 読取作法を timeout 経路と統一して locked snapshot で読む。
            string stdoutSnap, stderrSnap;
            lock (stdoutLock) { stdoutSnap = stdout.ToString(); }
            lock (stderrLock) { stderrSnap = stderr.ToString(); }
            return new ProcessResult(proc.ExitCode, stdoutSnap, stderrSnap);
        }
        finally
        {
            // Begin/Cancel をペア化し購読寿命を明示。post-EOF/Kill 後でも _output/_error は using Dispose まで
            // 非 null（Begin* は Start 後・proc.Start の Win32Exception catch は Begin 前に throw）ゆえ
            // Cancel は throw せず、伝播中の例外を握り潰さない。
            proc.CancelOutputRead();
            proc.CancelErrorRead();
        }
    }
}
