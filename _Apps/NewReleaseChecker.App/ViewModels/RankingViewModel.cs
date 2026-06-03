using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.App.Models;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>ランキング（SCR-009 / F-011）。売れ筋順に表示。</summary>
public sealed partial class RankingViewModel : ApiBrowseViewModel
{
    public RankingViewModel(IRakutenApiClient api) : base(api) { }

    protected override RakutenSearchQuery BuildQuery(string genreId) => new()
    {
        KoboGenreId = genreId,
        // 楽天Kobo電子書籍検索APIに「売れ筋(sales)」ソートは存在しない（standard / ±releaseDate / ±itemPrice /
        // reviewCount / reviewAverage のみ。公式仕様 2017-04-26 で確認済）。ランキングは API 既定の人気順
        // standard を採用する（ジャンル既定の並び＝最も売れ筋に近い順。要件 F-011）。
        Sort = "standard",
        Hits = 30,
    };

    protected override BookListItem ToItem(RakutenBook rb, int rank) => ToItemBase(rb, rank);
}
