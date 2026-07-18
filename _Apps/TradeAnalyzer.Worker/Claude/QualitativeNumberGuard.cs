using System.Text.RegularExpressions;

namespace TradeAnalyzer.Worker.Claude;

/// <summary>
/// Claude 出力（summary＋risks）に「注入していない数値」が混入していないかを検査するヒューリスティック。
/// <para>
/// 注入した事実（<see cref="ClaudeFacts.Lines"/> の値）から数値集合を作り、出力側の数値がそこに照合しなければ
/// <c>true</c>（＝注入外数値の疑い）を返す。ドロップはせず可視化フラグに使う（§数値ガード）。
/// <b>正直な限界</b>: 丸め・単位・％・言い換えで完全一致は難しく誤検知/見逃しが残る＝airtight ではない緩和策。
/// 日付値（"2026-05-14"）はトークンが 2026/05/14 に割れて許可集合を汚染するため「2026年に増益」型の捏造年が
/// 素通りする（日付トークン汚染による見逃し）。
/// 数値安全性がクリティカルなら SDK 経路（<c>OutputConfig.Format</c> スキーマ拘束）へ切替える。
/// </para>
/// </summary>
internal static class QualitativeNumberGuard
{
    // 数値トークン: 先頭数字＋(数字/カンマ/ピリオド)＋任意の末尾 %（半角/全角）。
    private static readonly Regex NumberToken = new(@"\d[\d,\.]*[%％]?", RegexOptions.Compiled);

    private static readonly char[] PercentChars = { '%', '％' };

    /// <summary>出力に注入外の数値が含まれる疑いがあれば true。</summary>
    public static bool HasUnverifiedNumbers(string summary, IEnumerable<string> risks, ClaudeFacts facts)
    {
        var allowed = new HashSet<string>();
        // 許可集合は「実際にプロンプトへ注入したテキスト」に一致させる: Lines の値に加え Code（銘柄コード）と
        // 会社名も注入済み＝引用は正当（欠落させると「銘柄7203」の引用だけで誤発火する）。Label は追加しない —
        // "17業種コード" の 17 等を白リスト化すると捏造「17倍」（比率数値クラス）に見逃し穴が開くため。
        foreach (var src in facts.Lines.Select(l => l.Value).Append(facts.Code).Append(facts.CompanyName))
            if (src != null)
                foreach (Match m in NumberToken.Matches(src))
                    allowed.Add(Normalize(m.Value));

        foreach (var text in risks.Prepend(summary))
            foreach (Match m in NumberToken.Matches(text))
            {
                var norm = Normalize(m.Value);
                // 素の1桁整数（0-9）は散文の個数表現（「2つのリスク」等）で頻出＝誤検知源のため除外する。
                // ただし %/％ 付き（「5%改善」等の比率主張）は捏造検出対象なので、生トークン基準で判定して
                // スキップさせない（Normalize が % を剥がした後の norm だけ見ると "5%" が素通りする）。
                if (norm.Length <= 1 && !norm.Contains('.') && m.Value.IndexOfAny(PercentChars) < 0) continue;
                if (!allowed.Contains(norm)) return true;
            }
        return false;
    }

    // カンマと末尾 %/％ を除去して比較キーにする（表記揺れの吸収。丸め/単位差までは吸収しない＝既知の限界）。
    private static string Normalize(string token) => token.Replace(",", "").TrimEnd('%', '％');
}
