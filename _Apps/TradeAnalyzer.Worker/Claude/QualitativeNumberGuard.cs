using System.Text.RegularExpressions;

namespace TradeAnalyzer.Worker.Claude;

/// <summary>
/// Claude 出力（summary＋risks）に「注入していない数値」が混入していないかを検査するヒューリスティック。
/// <para>
/// 注入した事実（<see cref="ClaudeFacts.Lines"/> の値）から数値集合を作り、出力側の数値がそこに照合しなければ
/// <c>true</c>（＝注入外数値の疑い）を返す。ドロップはせず可視化フラグに使う（§数値ガード）。
/// <b>正直な限界</b>: 丸め・単位・％・言い換えで完全一致は難しく誤検知/見逃しが残る＝airtight ではない緩和策。
/// 数値安全性がクリティカルなら SDK 経路（<c>OutputConfig.Format</c> スキーマ拘束）へ切替える。
/// </para>
/// </summary>
internal static class QualitativeNumberGuard
{
    // 数値トークン: 先頭数字＋(数字/カンマ/ピリオド)＋任意の末尾 %。
    private static readonly Regex NumberToken = new(@"\d[\d,\.]*%?", RegexOptions.Compiled);

    /// <summary>出力に注入外の数値が含まれる疑いがあれば true。</summary>
    public static bool HasUnverifiedNumbers(string summary, IEnumerable<string> risks, ClaudeFacts facts)
    {
        var allowed = new HashSet<string>();
        foreach (var line in facts.Lines)
            if (line.Value != null)
                foreach (Match m in NumberToken.Matches(line.Value))
                    allowed.Add(Normalize(m.Value));

        foreach (var text in risks.Prepend(summary))
            foreach (Match m in NumberToken.Matches(text))
            {
                var norm = Normalize(m.Value);
                // 素の1桁整数（0-9）は散文の個数表現（「2つのリスク」等）で頻出＝誤検知源のため除外する。
                if (norm.Length <= 1 && !norm.Contains('.')) continue;
                if (!allowed.Contains(norm)) return true;
            }
        return false;
    }

    // カンマと末尾 % を除去して比較キーにする（表記揺れの吸収。丸め/単位差までは吸収しない＝既知の限界）。
    private static string Normalize(string token) => token.Replace(",", "").TrimEnd('%');
}
