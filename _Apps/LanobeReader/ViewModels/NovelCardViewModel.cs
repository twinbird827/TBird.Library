using CommunityToolkit.Mvvm.ComponentModel;
using LanobeReader.Models;

namespace LanobeReader.ViewModels;

public partial class NovelCardViewModel : ObservableObject
{
    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Author { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SiteTypeLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int UnreadCount { get; set; }

    [ObservableProperty]
    public partial int ReadCount { get; set; }

    [ObservableProperty]
    public partial int EpisodeCount { get; set; }

    // ReadCount ≤ EpisodeCount は SQL の構造上保証される (両方とも同じ episodes 集計から派生)。
    public string ReadProgressLabel => $"{ReadCount}/{EpisodeCount}";

    partial void OnReadCountChanged(int value) => OnPropertyChanged(nameof(ReadProgressLabel));
    partial void OnEpisodeCountChanged(int value) => OnPropertyChanged(nameof(ReadProgressLabel));

    [ObservableProperty]
    public partial string LastUpdatedAt { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial bool HasUnconfirmedUpdate { get; set; }

    [ObservableProperty]
    public partial SiteType SiteType { get; set; }

    [ObservableProperty]
    public partial string NovelId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    public static NovelCardViewModel FromModel(Novel novel, int unreadCount, int readCount, int episodeCount)
    {
        return new NovelCardViewModel
        {
            Id = novel.Id,
            Title = novel.Title,
            Author = novel.Author,
            SiteTypeLabel = ((SiteType)novel.SiteType).GetLabel(),
            SiteType = (SiteType)novel.SiteType,
            NovelId = novel.NovelId,
            UnreadCount = unreadCount,
            ReadCount = readCount,
            EpisodeCount = episodeCount,
            LastUpdatedAt = DateTime.TryParse(novel.LastUpdatedAt, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
                ? dt.ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss")
                : novel.LastUpdatedAt ?? "",
            IsCompleted = novel.IsCompleted,
            HasUnconfirmedUpdate = novel.HasUnconfirmedUpdate,
            IsFavorite = novel.IsFavorite,
        };
    }
}
