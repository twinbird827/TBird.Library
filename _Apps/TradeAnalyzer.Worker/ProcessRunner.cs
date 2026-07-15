using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;

namespace TradeAnalyzer.Worker;

/// <summary>プロセスが正常完了したときの結果。ExitCode≠0 も正常完了の一種として含む（致命判断は呼び手）。</summary>
internal readonly record struct ProcessResult(int ExitCode, string Stdout, string Stderr);

/// <summary>
/// 外部プロセスを起動し stdout/stderr を捕捉、stdin 供給・timeout kill・bounded EOF drain（grace 超過時は
/// 受信済み分＋警告で続行＝出力末尾が欠落しうる）を一元化する共有起動点。ExitCode≠0 は throw せず
/// <see cref="ProcessResult"/> で返す（致命/非致命ポリシーは各消費者に置く）。
/// throw するのは次の場合のみ: 不正 timeout 引数（<see cref="ArgumentOutOfRangeException"/>＝プロセス起動前の
/// fail-fast、停止すべき子プロセスなし）・stdin なしでの StandardInputEncoding 設定（<see cref="ArgumentException"/>＝
/// 同じく起動前 fail-fast）・起動失敗（<see cref="InvalidOperationException"/>）・
/// timeout（<see cref="TimeoutException"/>）・外部 ct キャンセル（<see cref="OperationCanceledException"/> 再スロー。
/// ただし子プロセス正常終了後の grace 窓での ct 発火は OCE を投げず完了済み ProcessResult を返し、timeout/ct の
/// catch 到達時に子が既に終了済み（HasExited）の場合も kill 経路へ入らず完了済み ProcessResult を返す＝kill 経路のみ再スロー）。
/// 起動後の throw はいずれも子プロセスを停止させてから行う（孤児化回避）。run-today(uv run python) と 3b(claude -p) が共有する。
/// Redirect*/UseShellExecute は本クラスが強制し、stdin 供給時の StandardInputEncoding は本クラスが BOM なし UTF-8 を
/// 既定適用する（呼び手明示があれば尊重）。FileName/ArgumentList/WorkingDirectory/stdout・stderr の Encoding/
/// Environment は呼び手が psi に設定する（例: Python 経路の PYTHONUTF8=1）。
/// </summary>
internal static class ProcessRunner
{
    // EOF drain の上限。magic number の散在はドリフト源のため名前付き定数に集約する。
    // 正常終了後: 子が handle を継承した孫プロセスを残すと EOF が来ず、成功した実行が timeout まで停滞して
    // 誤失敗化するのを防ぐ上限。internal は SelfTest がケース (7)(8) の時間境界を導出参照するため
    //（生リテラル再エンコードによる grace 調整時のドリフト防止）。
    internal static readonly TimeSpan NormalExitEofGrace = TimeSpan.FromSeconds(15);
    // kill 後: Kill(entireProcessTree) を逃れた（double-fork/再親付けの）孫が EOF を握るケースの上限
    //（旧実装の WaitForExit(5秒) を踏襲）。
    private static readonly TimeSpan KilledEofGrace = TimeSpan.FromSeconds(5);
    // timeout の上限: CancelAfter が受理する最大遅延（net10 の Timer.MaxSupportedTimeout=0xFFFFFFFE ms ≈ 49.7 日）。
    // 超過は冒頭ガードで起動前に fail-fast する（超過値が CancelAfter まで届くと起動後・try/finally 外の
    // AOORE で子プロセスが孤児化する）。internal は Commands の設定境界検証が上限導出に参照するため。
    internal static readonly TimeSpan MaxTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

    public static async Task<ProcessResult> RunAsync(
        ProcessStartInfo psi,
        ILogger logger,
        TimeSpan timeout,
        string? stdin = null,
        string stdoutLogPrefix = "[proc]",
        string stderrLogPrefix = "[proc:err]",
        string displayName = "プロセス",
        bool captureStdout = false,
        string? startErrorHint = null,
        CancellationToken ct = default)
    {
        // 非正/上限超過 timeout は「0=無制限」等の誤解や設定ミスを黙って既定値へ書き換えず fail-fast で顕在化する
        //（silent-fallback 禁止方針）。psi の変異より前に検証し、引数不正時は psi を無傷のまま throw する。
        if (timeout <= TimeSpan.Zero || timeout > MaxTimeout)
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout,
                $"タイムアウトは正の値かつ {MaxTimeout} 以下を指定してください（範囲外の黙示フォールバックはしない）。");

        // stdin なしでの StandardInputEncoding 設定は stdin の渡し忘れの可能性が高く、silent に null リセットすると
        // 呼び手バグを隠蔽するため fail-fast で顕在化する（放置すると下の RedirectStandardInput=false 強制と
        // ProcessStartInfo の整合検査が衝突し、Start() が startErrorHint 包装外の生 InvalidOperationException を投げる）。
        // timeout ガードと同じく psi の変異より前・起動前に検証する。
        if (stdin == null && psi.StandardInputEncoding != null)
            throw new ArgumentException(
                "stdin なしで StandardInputEncoding が設定されています（stdin の渡し忘れの可能性。黙示リセットはしない）。",
                nameof(stdin));

        // redirect 設定は呼び手が忘れないようここで強制する。
        psi.RedirectStandardOutput = true;
        psi.RedirectStandardError = true;
        psi.UseShellExecute = false;
        psi.RedirectStandardInput = stdin != null;
        // stdin 供給時の StandardInputEncoding は未指定なら BOM なし UTF-8 を既定適用する（呼び手明示があれば尊重。
        // Redirect* 強制と同型パターン）。既定化しないと Windows 実装は GetConsoleCP()（日本語環境で cp932）を使い、
        // 非 ASCII プロンプトが silent に文字化けする。Encoding.UTF8（BOM 付き）は不可: 明示指定は
        // ConsoleEncoding（preamble 抑止）包装を経ず StreamWriter へ素通しされ、非シーク pipe への初回書込みで
        // BOM（EF BB BF）が子プロセス stdin の先頭に silent 混入する。
        if (stdin != null)
            psi.StandardInputEncoding ??= new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        // StringBuilder(非スレッドセーフ) を *DataReceived の append と全読取（timeout/正常 両経路）で保護する。
        // StringBuilder インスタンス自身を lock 対象にする公開ロックを避け、専用オブジェクトを用意する。
        // stdout も stderr と同様に lock 保護する（3b の JSON エンベロープを torn-read させない）。
        object stdoutLock = new();
        object stderrLock = new();
        using var proc = new Process { StartInfo = psi };

        // exit 待ちと EOF 待ちを分離する: net10 の WaitForExitAsync はプロセス終了の await 後に redirected 出力の
        // EOF を ct bound で内包待機するため（WaitUntilOutputEOF）、孫プロセスがパイプを握ると成功した実行が
        // full-timeout 停滞→TimeoutException に化ける。exit は Exited イベント→TCS で待ち、EOF は *DataReceived の
        // e.Data==null（最終発火）→TCS を grace 上限付きで待つ。Start() より前に購読・設定することで取りこぼし
        // レースを排除する。完了は TrySetResult（実務上各 1 回発火だが、二重完了で SetResult が throw しない防御の定石）。
        var exitTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stdoutEofTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stderrEofTcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        proc.EnableRaisingEvents = true;
        proc.Exited += (_, _) => exitTcs.TrySetResult();

        // ログレベルは stdout=Information / stderr=Warning で固定（現行 RunPythonAsync のレベルを保存）。
        // prefix は必ずテンプレ引数として渡す（文字列連結でメッセージテンプレートを動的化しない）。
        // EOF シグナル（e.Data==null → TCS 完了）と逐次ログは captureStdout に関わらず常時実行し、条件化するのは
        // 蓄積（AppendLine）のみ（EOF シグナルまで条件化すると captureStdout=false で毎回 grace 満了まで待つ退行になる）。
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data == null) { stdoutEofTcs.TrySetResult(); return; }
            if (captureStdout) { lock (stdoutLock) { stdout.AppendLine(e.Data); } }
            logger.LogInformation("{Prefix} {Line}", stdoutLogPrefix, e.Data);
        };
        proc.ErrorDataReceived += (_, e) =>
        {
            // stderr は例外添付（timeout 時の診断）に必要なため captureStdout に関わらず常時蓄積する。
            if (e.Data == null) { stderrEofTcs.TrySetResult(); return; }
            lock (stderrLock) { stderr.AppendLine(e.Data); }
            logger.LogWarning("{Prefix} {Line}", stderrLogPrefix, e.Data);
        };

        // 実行ファイル＋全引数＋cwd を保全する（診断情報の維持）。{Name} は付けない — 起動行は File/Args/cwd で
        // 自己記述的で、Args に script パスを含む消費者では二重表示＋動詞衝突になる（displayName は例外/警告の主語専用）。
        logger.LogInformation("起動: {File} {Args} (cwd={Cwd})",
            psi.FileName, string.Join(' ', psi.ArgumentList), psi.WorkingDirectory);

        // UseShellExecute=false の Process.Start() は実行ファイル不在時 false を返さず Win32Exception を
        // throw する（false 判定は dead-code）。例外を捕捉し原因明示で包む。修正手がかりは呼び手が
        // startErrorHint で注入する（例: "Python:UvPath を確認"）。未指定時は displayName ベースの汎用ヒントへ
        // フォールバックし、hint を渡し忘れた消費者でも診断が劣化しないようにする。
        try
        {
            proc.Start();
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"プロセスを起動できません: {psi.FileName}（{startErrorHint ?? $"{displayName} の実行ファイルパスを確認"}）。", ex);
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        // stdout/stderr の EOF（*DataReceived の最終発火）を grace 上限付きで待つ共通ヘルパ（正常経路/kill 経路で共用）。
        // EOF 未達のまま grace 満了（TimeoutException）または打ち切りトークン発火（TaskCanceledException＝即打ち切り扱い）
        // なら受信済み分で続行し、黙殺せず警告する。打ち切りトークンは呼び手が経路ごとに渡す — 正常経路は外部 ct
        //（grace 窓のキャンセル即応答）、timeout kill 経路は None（bounded 保証で末尾 stderr 診断を保全）、純粋外部 ct
        // kill 経路は ct（即打ち切り）。TCS は TrySetResult のみで fault しないため WhenAll は成功完了のみ＝
        // 下の catch フィルタが予期せぬ例外を握り潰すことはない。
        async Task DrainOutputsAsync(TimeSpan grace, CancellationToken drainCt)
        {
            try
            {
                // timeoutCts は使わない — timeout kill 経路では catch 到達時点で既発火のため drain が 0 秒に潰れ、
                // TimeoutException へ添付する末尾 stderr 診断が失われる（呼び手が None/ct を明示的に呼び分ける）。
                await Task.WhenAll(stdoutEofTcs.Task, stderrEofTcs.Task).WaitAsync(grace, drainCt).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is TimeoutException or OperationCanceledException)
            {
                // grace 満了 or 打ち切りトークン発火（cancel 起因と孫プロセス保持起因の区別は必須でないため文言は共通）。
                // 文言中の "EOF" トークンは SelfTest (7)(8) が警告発火の照合に使う — 言い換え時はテストも更新すること。
                logger.LogWarning(
                    "{Name}: stdout/stderr の EOF 待ちを {Grace:0.#} 秒で打ち切りました。孫プロセスが stdout/stderr を保持している可能性があり、出力末尾が欠落しえます（受信済み分で続行）。",
                    displayName, grace.TotalSeconds);
            }
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);
        // BeginOutput/ErrorReadLine と CancelOutput/ErrorRead をペア化し購読寿命を明示する（finally）。
        // 正常経路/kill 経路とも bounded EOF drain の完了（EOF 先着 or grace 満了/ct 打ち切り）後に Cancel が走る。
        // grace 打ち切り時は読取がアクティブなまま本 finally の Cancel で明示停止する。post-EOF/Kill 後でも
        // _output/_error は using Dispose まで非 null ゆえ Cancel は throw せず、伝播中の例外を握り潰さない。
        // ハンドラが参照する logger は DI 管理で本メソッドより寿命が長い。
        try
        {
            try
            {
                // stdin write は exit-only 待ち（Exited TCS）と同じ try・同じ timeout スコープに置く: パイプ満杯で
                // write がハングしても timeout で解除され、下の catch(OCE) が兼ねて kill＋TimeoutException 化まで
                // 到達する（outer try 内に置くことで finally の Cancel*Read も必ず通る）。プロンプトは通常 < 64KB
                // パイプバッファなので実際は即完了する。
                if (stdin != null)
                {
                    try
                    {
                        await proc.StandardInput.WriteAsync(stdin.AsMemory(), timeoutCts.Token).ConfigureAwait(false);
                        proc.StandardInput.Close(); // EOF を明示（閉じないと子が stdin 読み待ちで永久ブロックしうる）。
                    }
                    catch (IOException ex)
                    {
                        // broken pipe: 子が stdin を読まず先に終了したケース。write を放棄して exit-only 待ちへ進み
                        // ExitCode を取得する（プロセスは完了済みゆえここでは throw しない）。
                        logger.LogWarning(ex, "{Name}: stdin 書込みに失敗（子プロセスが先に終了した可能性）。", displayName);
                    }
                }
                // exit のみを待つ（EOF drain は含めない）。timeout/外部 ct は WaitAsync の OCE として下の catch へ
                //（旧 WaitForExitAsync 呼出と同じ例外セマンティクス）。ExitCode は exit 確定後に読む。
                await exitTcs.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // catch 到達時に子が既に終了済みなら第 3 の早期離脱カテゴリ: timeout/外部 ct の発火と正常終了の
                // マイクロ秒競合で WaitAsync のキャンセル側が勝ったケース。kill/throw をスキップして catch を素通りし、
                // 外側 try の正常経路（EOF drain → ProcessResult 返却）へ合流する（成功した実行＝DB 書戻し済みの
                // 誤失敗化を防ぐ）。判定は Exited イベント（exitTcs）でなく OS プロセス状態を直接見る HasExited —
                // 物理終了済みだがイベント未配送の残余窓も同一コストで閉じる。チェックは Kill より前が必須
                //（Kill 後は kill 起因の終了と「timeout 前に終了」を判別できなくなる）。
                if (!proc.HasExited)
                {
                    // 分類（timeout か外部 ct か）は早期離脱チェック直後＝throw 伝播直後に固定する。Kill＋最大 5 秒の
                    // drain の後に評価すると、真正 timeout の直後にホスト shutdown 等で外部 ct が発火した場合に素の OCE へ
                    // 誤分類され TimeoutException＋stderr 診断が失われる（旧 exception filter の throw 時点評価を復元）。
                    bool isTimeout = timeoutCts.IsCancellationRequested && !ct.IsCancellationRequested;
                    // フィルタなしで捕捉し、原因（timeout/外部 ct/理論上の spurious OCE）を問わずまず子を確実に停止する
                    //（孤児化回避）。kill の例外は握り潰さず innerException に連鎖させる（no-swallow）。
                    Exception? killEx = null;
                    try
                    {
                        proc.Kill(entireProcessTree: true);
                    }
                    catch (Exception ex) { killEx = ex; logger.LogWarning(ex, "{Name}: 子プロセス kill 失敗（既に終了済みの可能性）。", displayName); }

                    // kill で子ツリーのパイプは通常すぐ閉じ EOF が来る。Kill(entireProcessTree) を逃れた孫が write handle を
                    // 握るケースに備え KilledEofGrace で bounded に drain し、無限待ちを排除する。打ち切りトークンは呼び分ける:
                    // timeout 経路は None＝ct 非連動で KilledEofGrace の bounded 保証を確保し、分類確定後に外部 ct が発火しても
                    // TimeoutException へ添付する末尾 stderr 診断を保全する（旧同期 WaitForExit(5秒) 相当の保証。代償:
                    // ホスト shutdown が最大 5 秒待たされうる）。純粋外部 ct 経路は ct＝発火済みのため drain は即 0 秒で
                    // キャンセル即応答を優先する意図的仕様（代償: 末尾 stderr 診断が失われうる）。
                    await DrainOutputsAsync(KilledEofGrace, isTimeout ? CancellationToken.None : ct).ConfigureAwait(false);

                    if (isTimeout)
                    {
                        // timeout 由来。受信済み stderr を診断として例外へ添付する（トレースバック等の保持）。
                        string tail;
                        lock (stderrLock) { tail = stderr.ToString(); }
                        throw new TimeoutException(
                            $"{displayName} が {timeout.TotalMinutes:0.##} 分でタイムアウトしました\nstderr(タイムアウトまで):\n{tail}", killEx);
                    }
                    // それ以外＝実運用では外部 ct 由来（timeoutCts は linked ゆえ ct キャンセルでも発火する）。子は kill 済み。
                    // kill 失敗は innerException 連鎖で顕在化させ、成功時は元 OCE を再スローしキャンセルを honor する。
                    // この経路が必ず送出して終わることで、kill 済みプロセスの ProcessResult を返す事故を防ぐ。
                    if (killEx != null)
                        throw new OperationCanceledException("キャンセル中の子プロセス kill に失敗", killEx, ct);
                    throw;
                }
            }

            // 子は正常終了済み。WaitForExitAsync は EOF drain を timeout まで内包してしまうため exit-only 待ちに分離し、
            // EOF はここで NormalExitEofGrace を上限に bounded に待つ。grace 超過（または外部 ct 発火）でも完了済みの
            // 実行結果は破棄せず、警告のうえ受信済み分の ProcessResult を返す（新規の OCE は投げない契約）。
            await DrainOutputsAsync(NormalExitEofGrace, ct).ConfigureAwait(false);

            // EOF 到達済みなら並行 writer は不在だが、grace 打ち切り時はハンドラが遅延発火しうるため
            // 読取作法を kill 経路と統一して locked snapshot で読む。captureStdout=false は蓄積自体を
            // していないため Stdout は空文字列を返す契約（ToString の materialize もスキップ）。
            string stdoutSnap = "";
            if (captureStdout) { lock (stdoutLock) { stdoutSnap = stdout.ToString(); } }
            string stderrSnap;
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
