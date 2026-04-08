namespace LanobeReader.Models;

public static class NarouGenres
{
    public static readonly IReadOnlyList<GenreInfo> BigGenres = new List<GenreInfo>
    {
        new("", "すべて"),
        new("1", "恋愛"),
        new("2", "ファンタジー"),
        new("3", "文芸"),
        new("4", "SF"),
        new("99", "その他"),
        new("98", "ノンジャンル"),
    };

    public static readonly IReadOnlyList<GenreInfo> SubGenres = new List<GenreInfo>
    {
        new("", "すべて"),
        new("101", "異世界恋愛"),
        new("102", "現実世界恋愛"),
        new("201", "ハイファンタジー"),
        new("202", "ローファンタジー"),
        new("301", "純文学"),
        new("302", "ヒューマンドラマ"),
        new("303", "歴史"),
        new("304", "推理"),
        new("305", "ホラー"),
        new("306", "アクション"),
        new("307", "コメディー"),
        new("401", "VRゲーム"),
        new("402", "宇宙"),
        new("403", "空想科学"),
        new("404", "パニック"),
        new("9901", "童話"),
        new("9902", "詩"),
        new("9903", "エッセイ"),
        new("9904", "リプレイ"),
        new("9999", "その他"),
        new("9801", "ノンジャンル"),
    };
}
