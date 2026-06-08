namespace NewReleaseChecker.App.Models;

/// <summary>
/// 楽天Kobo のジャンルID。
/// LightNovel / Comic は 2026-06-08 にジャンル検索APIで実値検証済み
/// （101903=ライトノベル / 101904=漫画(コミック)）。値の正は <see cref="Core.MediaType"/>。
/// </summary>
public static class KoboGenres
{
    /// <summary>電子書籍ルート。</summary>
    public const string Root = "101";

    /// <summary>ライトノベル（検証済み: 101903）。</summary>
    public const string LightNovel = Core.MediaType.NovelKoboGenreId;

    /// <summary>漫画（コミック）（検証済み: 101904）。</summary>
    public const string Comic = Core.MediaType.ComicKoboGenreId;

    /// <summary>メディアタブ（0=ラノベ, 1=コミック）→ koboGenreId。</summary>
    public static string ForMedia(int tab) => tab == 1 ? Comic : LightNovel;
}
