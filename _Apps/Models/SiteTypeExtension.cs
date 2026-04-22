namespace LanobeReader.Models;

public static class SiteTypeExtension
{
    public static string GetLabel(this SiteType siteType) => siteType switch
    {
        SiteType.Narou => "なろう",
        SiteType.Kakuyomu => "カクヨム",
        _ => siteType.ToString(),
    };
}
