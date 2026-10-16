using CommunityToolkit.Mvvm.ComponentModel;
using LanobeReader.Models;

namespace LanobeReader.ViewModels;

public partial class EpisodeViewModel : ObservableObject
{
    [ObservableProperty]
    public partial int Id { get; set; }

    [ObservableProperty]
    public partial int EpisodeNo { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? ChapterName { get; set; }

    [ObservableProperty]
    public partial bool IsRead { get; set; }

    [ObservableProperty]
    public partial bool IsFavorite { get; set; }

    [ObservableProperty]
    public partial bool IsCached { get; set; }

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
