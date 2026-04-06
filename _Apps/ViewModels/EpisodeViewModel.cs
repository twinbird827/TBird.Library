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

    public static EpisodeViewModel FromModel(Episode episode)
    {
        return new EpisodeViewModel
        {
            Id = episode.Id,
            EpisodeNo = episode.EpisodeNo,
            Title = episode.Title,
            ChapterName = episode.ChapterName,
            IsRead = episode.IsRead == 1,
        };
    }
}
