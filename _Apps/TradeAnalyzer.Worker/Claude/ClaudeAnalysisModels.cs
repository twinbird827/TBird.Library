namespace TradeAnalyzer.Worker.Claude;

/// <summary>注入する「事実」1件（ラベルと表示値）。<see cref="Value"/>=null は「データなし」。
/// PromptBuilder がこれをプロンプトへ整形し、QualitativeNumberGuard がここから許可数値集合を作る
/// （注入数値の単一ソース）。</summary>
public sealed record FactLine(string Label, string? Value);

/// <summary>
/// C# が DB 実数＋派生指標（C# 計算）から組む「事実」束。Claude はこれを引用・要約・リスク言語化するだけで、
/// 新たな数値は作らない（設計§2「数値は生成させない」）。<see cref="Lines"/> が注入内容の正典。
/// </summary>
public sealed record ClaudeFacts(string Code, string? CompanyName, IReadOnlyList<FactLine> Lines);

/// <summary>
/// Claude 定性分析の結果。<see cref="NumericUnverified"/> は注入外数値の混入をヒューリスティックで検出した旗
/// （ドロップせず可視化）。失敗時はサービスが結果でなく <c>null</c> を返す（ML のみで継続＝フォールバック契約）。
/// </summary>
public sealed record ClaudeAnalysisResult(
    string Summary,
    IReadOnlyList<string> Risks,
    IReadOnlyList<string> UsedFacts,
    string Model,
    bool NumericUnverified);
