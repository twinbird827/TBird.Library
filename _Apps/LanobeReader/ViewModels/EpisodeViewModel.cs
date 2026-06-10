using CommunityToolkit.Mvvm.ComponentModel;
using LanobeReader.Models;

namespace LanobeReader.ViewModels;

public partial class EpisodeViewModel : ObservableObject
{
    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private int _episodeNo;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string? _chapterName;

    [ObservableProperty]
    private bool _isRead;

    [ObservableProperty]
    private bool _isFavorite;

    [ObservableProperty]
    private bool _isCached;

    public static EpisodeViewModel FromModel(Episode episode, bool isCached = false)
    {
        return new EpisodeViewModel
        {
            Id = episode.Id,
            EpisodeNo = episode.EpisodeNo,
            Title = episode.Title,
            ChapterName = episode.ChapterName,
            IsRead = episode.IsRead,
            IsFavorite = episode.IsFavorite,
            IsCached = isCached,
        };
    }
}
