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
        // ⚠️ 売れ筋順の sort 値は実装時に要検証（要件 §8 / F-011）。standard は既定の人気順。
        Sort = "standard",
        Hits = 30,
    };

    protected override BookListItem ToItem(RakutenBook rb, int rank) => ToItemBase(rb, rank);
}
