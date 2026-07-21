using System.Text;

namespace TradeAnalyzer.Worker.Claude;

/// <summary>
/// 注入済み実データ（<see cref="ClaudeFacts"/>）から <c>claude -p</c> の単一プロンプトを組む純粋関数。
/// 指示・入力・出力形式を1本にまとめる（stdin で単一プロンプトを受けるため）。核心は数値ハルシネーション対策の
/// 厳守ルール（新たな数値を作らせない）と、<c>result</c> に JSON のみを返させる出力形式指示。
/// </summary>
internal static class ClaudePromptBuilder
{
    public static string Build(ClaudeFacts facts)
    {
        var sb = new StringBuilder();
        sb.AppendLine("あなたは日本株の定性レビュー担当です。以下に提供された実データ「のみ」に基づき、");
        sb.AppendLine("投資候補の根拠とリスクを日本語で簡潔に述べてください。");
        sb.AppendLine();
        sb.AppendLine("# 厳守ルール（違反禁止）");
        sb.AppendLine("- 数値・比率・目標株価・将来予測を新たに生成・推測しない。提供された数値のみ引用できる。");
        sb.AppendLine("- 提供された派生指標（PER/PBR/時価総額近似）は計算済みの事実。再計算・改変しない。");
        sb.AppendLine("- 「データなし」の項目について数値を補完しない。");
        sb.AppendLine("- 投資助言（買い推奨・売り推奨）をしない。最終判断は人間が行う。");
        sb.AppendLine();
        sb.AppendLine("# 入力データ（実データ・改変不可）");
        sb.AppendLine($"銘柄コード: {Flatten(facts.Code)}");
        sb.AppendLine($"会社名: {Flatten(facts.CompanyName) ?? "データなし"}");
        foreach (var line in facts.Lines)
            sb.AppendLine($"{line.Label}: {Flatten(line.Value) ?? "データなし"}");
        sb.AppendLine();
        sb.AppendLine("# 出力形式（JSON のみ・前後に説明文やコードフェンスを付けない）");
        sb.AppendLine("{\"summary\":\"2〜3文の根拠要約\",\"risks\":[\"リスク2〜4項目\"],\"used_facts\":[\"引用した入力データのラベル\"]}");
        sb.AppendLine("- summary/risks には入力データに無い数値を含めない。");
        sb.AppendLine("- used_facts には根拠に使った入力データのラベルを列挙する（監査用）。");
        return sb.ToString();
    }

    /// <summary>
    /// 外部データ由来の埋め込み値（会社名＝J-Quants CoName、Sector/DocType 等の passthrough）を1行へ平坦化する
    /// プロンプトインジェクションガード。改行/制御文字を空白化し、細工された値が「# 厳守ルール」等の指示行に
    /// 化けるのを防ぐ（全埋め込み値が通る唯一の連結点＝ここに単一ガードを置く。正当な値は改行を含まず動作不変）。
    /// U+2028/U+2029（Zl/Zp）は char.IsControl（Cc のみ）に含まれないため明示列挙する — ソースには必ず
    /// エスケープ表記で書くこと（生の行区切り文字はエディタ/git で不可視・破損しやすい）。
    /// </summary>
    private static string? Flatten(string? value) =>
        value is null ? null : string.Concat(value.Select(c => char.IsControl(c) || c is '\u2028' or '\u2029' ? ' ' : c));
}
