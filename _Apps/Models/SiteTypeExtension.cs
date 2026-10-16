namespace LanobeReader.Models;

public static class SiteTypeExtension
{
    public static string GetLabel(this SiteType siteType) => siteType switch
    {
        SiteType.Narou => "なろう",
        SiteType.Kakuyomu => "カクヨム",
        _ => siteType.ToString(),
    };

    /// <summary>
    /// SiteRateLimiter / NetworkPolicyService 内部で使うサイト識別キー。
    /// この拡張メソッドが「唯一のソース」。SiteType への enum 値追加時は switch にケースを
    /// 追加すること（追加忘れは MauiProgram の SiteRateLimiter ファクトリ呼出時点で
    /// ArgumentOutOfRangeException として早期検出される）。
    /// </summary>
    public static string GetApiKey(this SiteType s) => s switch
    {
        SiteType.Narou => "narou",
        SiteType.Kakuyomu => "kakuyomu",
        _ => throw new ArgumentOutOfRangeException(nameof(s), s, "Unknown SiteType"),
    };
}
