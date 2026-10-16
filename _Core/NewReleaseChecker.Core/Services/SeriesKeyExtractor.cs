using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace NewReleaseChecker.Core.Services;

/// <summary>
/// タイトルからシリーズキー（追跡キー）を抽出する（要件 §3.2.1）。
/// 巻数・副題・付随注記を除いた語を返す。最終的にはユーザーが登録確認ダイアログで手修正する前提。
/// </summary>
public static class SeriesKeyExtractor
{
    // 角括弧/丸括弧などの注記（【完結】（電子版）等）
    private static readonly Regex Bracketed = new(@"[【\[(（《〈][^】\])）》〉]*[】\])）》〉]", RegexOptions.Compiled);

    // 巻数表現（第3巻 / 3巻 / (3) / Ⅲ / 上中下 / vol.3 など）。タイトル途中の最初の出現位置で切る。
    // 裸の数字は「空白で区切られた独立トークン」のみを巻数とみなす（先読み/後読みで前後の空白・末尾を要求）。
    // 句読点境界での誤発火（"No.6"→"No." / "GANTZ:E 360" のタイトル中数字）を避け、過剰切断を抑える。
    private static readonly Regex VolumeMarker = new(
        @"(第\s*[0-9]+\s*巻?|[0-9]+\s*巻|[ⅠⅡⅢⅣⅤⅥⅦⅧⅨⅩⅪⅫ]+|[（(]\s*[0-9]+\s*[)）]|\bvol\.?\s*[0-9]+|(?<=\s)[0-9]{1,3}(?=\s|$)|[上中下]巻)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static string Extract(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        var t = title.Normalize(NormalizationForm.FormKC).Trim();
        t = Bracketed.Replace(t, " ").Trim();

        var m = VolumeMarker.Match(t);
        if (m.Success && m.Index > 0)
        {
            t = t[..m.Index];
        }

        return t.Trim(' ', '　', '-', '―', '—', '~', '〜', '、', '。', ':', '：', '/', '／').Trim();
    }
}
