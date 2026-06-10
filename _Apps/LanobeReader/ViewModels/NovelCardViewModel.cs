using CommunityToolkit.Mvvm.ComponentModel;
using LanobeReader.Models;

namespace LanobeReader.ViewModels;

public partial class NovelCardViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _author = string.Empty;

    [ObservableProperty]
    private string _siteTypeLabel = string.Empty;

    [ObservableProperty]
    private int _unreadCount;

    [ObservableProperty]
    private int _readCount;

    [ObservableProperty]
    private int _episodeCount;

    // ReadCount ≤ EpisodeCount は SQL の構造上保証される (両方とも同じ episodes 集計から派生)。
    public string ReadProgressLabel => $"{ReadCount}/{EpisodeCount}";

    partial void OnReadCountChanged(int value) => OnPropertyChanged(nameof(ReadProgressLabel));
    partial void OnEpisodeCountChanged(int value) => OnPropertyChanged(nameof(ReadProgressLabel));

    [ObservableProperty]
    private string _lastUpdatedAt = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _hasUnconfirmedUpdate;

    [ObservableProperty]
    private SiteType _siteType;

    [ObservableProperty]
    private string _novelId = string.Empty;

    [ObservableProperty]
    private bool _isFavorite;

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
