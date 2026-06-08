using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NewReleaseChecker.Core.Services;

/// <summary>
/// 著者文字列を正規化済みの「著者集合」へ分割するヘルパ（要件 §3.2.1）。
/// - 区切り文字で分割して集合化。
/// - 肩書きラベル（原作/作画/イラスト/著 等）は除去するが、人物名そのものは保持する
///   （イラストレーター/作画者を登録著者に含めればコミカライズを別シリーズとして弁別できる）。
/// - 同定は部分集合一致（登録著者集合 ⊆ 候補著者集合。<see cref="SeriesIdentifier"/>）。
///   正規化規則は表記揺れに敏感なため実レスポンスで要調整（§8）。
/// </summary>
public static class AuthorNormalizer
{
    // 著者の区切り文字（半角/全角の各種）
    private static readonly char[] Separators =
        { '/', '／', ',', '，', '、', '・', ';', '；', '|', '｜', '\n', '\t' };

    // 肩書きラベル（人物名ではない役割語）。長いものを先に並べて優先除去する。
    // 単一文字ラベル（絵/著/編/訳）と "company" は、人名の先頭/末尾に偶然一致して実在の人名を破壊する
    // （例:「千絵」→「千」）ため、ここでは除外する。これらは括弧内（RoleInParens）・コロン前置（"絵：X"）の
    // 文脈でのみ別途除去される。要件 §7.2「人物名は除去せず保持」を優先。
    private static readonly string[] RoleLabels =
    {
        "キャラクター原案", "キャラクターデザイン", "メカニックデザイン",
        "原作", "作画", "漫画", "まんが", "原案", "脚本", "構成", "監修",
        "イラスト", "插絵", "挿絵", "插画", "挿画", "著者",
    };

    private static readonly Regex RoleInParens = new(
        @"[(（【\[]\s*(原作|作画|漫画|まんが|原案|脚本|構成|監修|イラスト|挿絵|挿画|絵|著|編|訳)\s*[)）】\]]",
        RegexOptions.Compiled);

    /// <summary>著者文字列を正規化済み著者集合に変換する。</summary>
    public static IReadOnlySet<string> ToSet(string? author)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(author)) return set;

        foreach (var raw in author.Split(Separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var name = NormalizeName(raw);
            if (!string.IsNullOrEmpty(name)) set.Add(name);
        }
        return set;
    }

    /// <summary>個々の著者名を正規化する（人物名は保持、肩書きラベルのみ除去）。</summary>
    public static string NormalizeName(string rawName)
    {
        if (string.IsNullOrWhiteSpace(rawName)) return string.Empty;

        // 全角/半角・互換文字を統一（NFKC）
        var s = rawName.Normalize(NormalizationForm.FormKC);

        // 「原作:川原礫」のように "ラベル:名前" 形式なら名前部分を採用
        var colon = s.LastIndexOfAny(new[] { ':', '：' });
        if (colon >= 0 && colon < s.Length - 1)
        {
            s = s[(colon + 1)..];
        }

        // 括弧内の役割語を除去（（原作）等）
        s = RoleInParens.Replace(s, string.Empty);

        // 先頭/末尾に付くラベル語を除去
        s = s.Trim();
        bool removed;
        do
        {
            removed = false;
            foreach (var label in RoleLabels)
            {
                if (s.StartsWith(label, StringComparison.Ordinal) && s.Length > label.Length)
                {
                    s = s[label.Length..].Trim();
                    removed = true;
                }
                if (s.EndsWith(label, StringComparison.Ordinal) && s.Length > label.Length)
                {
                    s = s[..^label.Length].Trim();
                    removed = true;
                }
            }
        } while (removed);

        // 残った括弧・記号・空白を除去（人物名内の中黒「・」は保持する）
        s = s.Trim(' ', '　', '(', ')', '（', '）', '【', '】', '[', ']', ':', '：', '/', '／');

        // 内部の空白を除去（"川原 礫" → "川原礫"。表記揺れ吸収）
        s = Regex.Replace(s, @"\s+", string.Empty);

        return s;
    }

    /// <summary>保存済み AuthorSet 文字列（改行区切り）を集合へ復元する。</summary>
    public static IReadOnlySet<string> ParseStored(string? stored)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(stored)) return set;
        foreach (var line in stored.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            set.Add(line);
        }
        return set;
    }

    /// <summary>著者集合を保存用文字列（改行区切り）へ整形する。</summary>
    public static string ToStored(IEnumerable<string> set) => string.Join('\n', set);
}
