namespace LanobeReader.Models;

public static class KakuyomuGenres
{
    public static readonly IReadOnlyList<GenreInfo> Genres = new List<GenreInfo>
    {
        new("all", "総合"),
        new("fantasy", "異世界ファンタジー"),
        new("action", "現代ファンタジー"),
        new("sf", "SF"),
        new("love_story", "恋愛"),
        new("romance", "ラブコメ"),
        new("drama", "現代ドラマ"),
        new("horror", "ホラー"),
        new("mystery", "ミステリー"),
        new("nonfiction", "エッセイ・ノンフィクション"),
        new("history", "歴史・時代・伝奇"),
        new("criticism", "創作論・評論"),
        new("others", "詩・童話・その他"),
    };

    public static readonly IReadOnlyList<GenreInfo> Periods = new List<GenreInfo>
    {
        new("daily", "日間"),
        new("weekly", "週間"),
        new("monthly", "月間"),
        new("yearly", "年間"),
        new("entire", "累計"),
    };
}
