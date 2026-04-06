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
    private string _lastUpdatedAt = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private bool _hasUnconfirmedUpdate;

    [ObservableProperty]
    private SiteType _siteType;

    [ObservableProperty]
    private string _novelId = string.Empty;

    public static NovelCardViewModel FromModel(Novel novel, int unreadCount)
    {
        return new NovelCardViewModel
        {
            Id = novel.Id,
            Title = novel.Title,
            Author = novel.Author,
            SiteTypeLabel = ((SiteType)novel.SiteType) == SiteType.Narou ? "なろう" : "カクヨム",
            SiteType = (SiteType)novel.SiteType,
            NovelId = novel.NovelId,
            UnreadCount = unreadCount,
            LastUpdatedAt = novel.LastUpdatedAt ?? "",
            IsCompleted = novel.IsCompleted == 1,
            HasUnconfirmedUpdate = novel.HasUnconfirmedUpdate == 1,
        };
    }
}
