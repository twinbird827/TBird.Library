using System.Globalization;

namespace LanobeReader.Helpers;

/// <summary>
/// Narou の最終更新時刻("yyyy-MM-dd HH:mm:ss"、JST・オフセット無し)を UTC ISO("o")へ正規化する共通処理。
///
/// 保存形式を UTC に統一することで、新着確定時フォールバック(<c>DateTime.UtcNow.ToString("o")</c>)と同形式に
/// 揃え、<c>UpdateCheckService.SameInstant</c> の TZ/形式差による誤判定(無駄なフル再取得)と、端末ローカル TZ
/// での誤表示を防ぐ。NarouApiService(取得時の正規化)と DatabaseService の移行(既存行の一括正規化)が同じ
/// 規則を共有する必要があるため、両者が参照する 1 箇所へ集約する(規則の乖離を防ぐ)。
/// </summary>
public static class NarouDateTime
{
    /// <summary>
    /// Narou の生 JST 文字列を UTC ISO("o")へ変換する。JST は DST が無いため固定 +9 時間で換算する。
    /// 解析不能・空の値は素のまま返す(best-effort)。既に ISO/UTC な値を渡しても、TryParse がオフセットを
    /// 解釈するため二重シフトはしない。
    /// ※ UpdateCheckService.SameInstant はオフセット無し値を UTC とみなす(保存済み正規化値の比較用)。本メソッドは
    /// Narou 生 JST 前提で +9 する点が異なるため、両者を 1 つのパーサへ統合しない(JST 生値と UTC 正規化値の混同防止)。
    /// </summary>
    public static string? ToUtcIso(string? narouJst)
    {
        if (string.IsNullOrEmpty(narouJst)) return narouJst;
        if (!DateTime.TryParse(narouJst, CultureInfo.InvariantCulture, DateTimeStyles.None, out var local))
        {
            return narouJst;
        }
        var utc = new DateTimeOffset(
            DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeSpan.FromHours(9)).UtcDateTime;
        return utc.ToString("o", CultureInfo.InvariantCulture);
    }
}
