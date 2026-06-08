namespace NewReleaseChecker.Core;

/// <summary>メディア種別の定数。Series.MediaType に格納する値。</summary>
public static class MediaType
{
    public const string Novel = "novel";
    public const string Comic = "comic";

    // 楽天Kobo のジャンルID（2026-06-08 にジャンル検索APIで実値検証済み）。
    // 101903=ライトノベル / 101904=漫画（コミック）。シリーズ同定の検索で本編の種別に絞り込み、
    // コミカライズ等の別ジャンルを候補から除外する（要件 §3.2.1 / §7.2）。
    /// <summary>ライトノベルの koboGenreId。</summary>
    public const string NovelKoboGenreId = "101903";

    /// <summary>漫画（コミック）の koboGenreId。</summary>
    public const string ComicKoboGenreId = "101904";

    /// <summary>MediaType（"novel"/"comic"）→ 検索で絞り込む koboGenreId。</summary>
    public static string ToKoboGenreId(string mediaType) =>
        mediaType == Comic ? ComicKoboGenreId : NovelKoboGenreId;
}
