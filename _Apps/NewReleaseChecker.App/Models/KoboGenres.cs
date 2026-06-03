namespace NewReleaseChecker.App.Models;

/// <summary>
/// 楽天Kobo のジャンルID。
/// ⚠️ 実装時の検証事項（要件 §8 / CLAUDE.md §9）: LightNovel / Comic の koboGenreId 体系は
/// 楽天Koboジャンル検索 API（applicationId 設定後）で実値を確認して修正すること。
/// </summary>
public static class KoboGenres
{
    /// <summary>電子書籍ルート。</summary>
    public const string Root = "101";

    /// <summary>ライトノベル（要検証）。</summary>
    public const string LightNovel = "101904";

    /// <summary>コミック（要検証）。</summary>
    public const string Comic = "101901";

    /// <summary>メディアタブ（0=ラノベ, 1=コミック）→ koboGenreId。</summary>
    public static string ForMedia(int tab) => tab == 1 ? Comic : LightNovel;
}
