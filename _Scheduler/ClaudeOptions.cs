namespace TradeAnalyzer.Worker;

/// <summary>
/// 段階3b の Claude 定性層プロセス起動設定（既定 <c>claude -p --output-format json</c>）。
/// <see cref="PythonOptions"/> と対の型付き設定で、Worker 固有（Core/Data は Claude を知らない）。
/// <para>
/// 経路は claude CLI 直結の1実装のみ（公式 Anthropic SDK 経路が要件化した時点で
/// 経路スイッチを再導入。旧 <c>Claude:Route</c> キーが設定に残っていても未知キーとして無視される）。
/// <see cref="TimeoutMinutes"/> は非正値・上限（<see cref="ProcessRunner.MaxTimeout"/>）
/// 超過だと ProcessRunner.RunAsync が ArgumentOutOfRangeException（fail-fast・黙示フォールバックはしない）。
/// explain-today は起動直後に ValidateClaudeConfig が config キー名（Claude:TimeoutMinutes 等）つきで事前検証する。
/// </para>
/// </summary>
public class ClaudeOptions
{
    public const string SectionName = "Claude";

    /// <summary>claude 実行ファイル。Windows 既定は npm 導入の <c>claude.cmd</c> シム
    /// （<c>UseShellExecute=false</c> 下は CreateProcess が <c>.cmd</c> 拡張子を要求する）。
    /// 非 Windows は <c>claude</c>、PATH 非依存なら絶対パス指定可。</summary>
    public string ExecutablePath { get; set; } = "claude.cmd";

    /// <summary>モデル（既定はエイリアス <c>opus</c>＝CLI の版が知る最新 Opus に解決。完全 ID も指定可）。</summary>
    public string Model { get; set; } = "opus";

    /// <summary>Claude 実行のタイムアウト（分）。0 以下・上限（<see cref="ProcessRunner.MaxTimeout"/>）超過は
    /// ProcessRunner.RunAsync が ArgumentOutOfRangeException（fail-fast）。起動直後に ValidateClaudeConfig が
    /// config キー名つきで事前検証する。</summary>
    public int TimeoutMinutes { get; set; } = 5;

    /// <summary>設定時は <c>--bare</c>＋<c>ANTHROPIC_API_KEY</c> で API 課金、未設定は OAuth（置き場は Secrets.json の <c>Claude:ApiKey</c>）。</summary>
    public string? ApiKey { get; set; }
}
