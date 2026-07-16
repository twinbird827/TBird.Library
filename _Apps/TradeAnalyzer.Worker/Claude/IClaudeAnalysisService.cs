namespace TradeAnalyzer.Worker.Claude;

/// <summary>
/// 当日 Top-K 候補に実データを注入して定性根拠文／リスクを生成する定性層。
/// <para>
/// フォールバック契約: 失敗（認証切れ・クレジット枯渇・CLI 不在/起動失敗・timeout・パース不能）は例外でなく
/// <c>null</c> を返す＝呼び手は当該銘柄をスキップし ML のみで継続する（run-today の fail-fast と対照。Claude は
/// 非必須層）。<c>Route</c> による cli/sdk 差替の DI スイッチ点でもある（README「二経路ヘッジ」）。
/// </para>
/// </summary>
public interface IClaudeAnalysisService
{
    Task<ClaudeAnalysisResult?> AnalyzeAsync(ClaudeFacts facts, CancellationToken ct = default);
}
