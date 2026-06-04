using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using NewReleaseChecker.Core.Services;
using NewReleaseChecker.App.Models;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>発売予定表（SCR-008 / F-009）。近刊・予約を発売日順に表示。</summary>
public sealed partial class UpcomingViewModel : ApiBrowseViewModel
{
    public UpcomingViewModel(IRakutenApiClient api, IBookRepository book, BookActionService actions, IUserNotifier notifier)
        : base(api, book, actions, notifier) { }

    protected override RakutenSearchQuery BuildQuery(string genreId) => new()
    {
        KoboGenreId = genreId,
        Sort = "+releaseDate",
        SalesType = 1, // 予約販売（近刊・予約発掘）
        Hits = 30,
    };

    protected override BookListItem ToItem(RakutenBook rb, int rank) => ToItemBase(rb, null);
}
