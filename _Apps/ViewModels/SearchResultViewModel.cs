using CommunityToolkit.Mvvm.ComponentModel;
using LanobeReader.Models;

namespace LanobeReader.ViewModels;

public partial class SearchResultViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Author { get; set; } = string.Empty;

    [ObservableProperty]
    public partial int TotalEpisodes { get; set; }

    [ObservableProperty]
    public partial bool IsCompleted { get; set; }

    [ObservableProperty]
    public partial string SiteTypeLabel { get; set; } = string.Empty;

    [ObservableProperty]
    public partial SiteType SiteType { get; set; }

    [ObservableProperty]
    public partial string NovelId { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsRegistered { get; set; }

    [ObservableProperty]
    public partial bool IsRegistering { get; set; }

    public static SearchResultViewModel FromModel(SearchResult result, bool isRegistered)
    {
        return new SearchResultViewModel
        {
            Title = result.Title,
            Author = result.Author,
            TotalEpisodes = result.TotalEpisodes,
            IsCompleted = result.IsCompleted,
            SiteTypeLabel = result.SiteType.GetLabel(),
            SiteType = result.SiteType,
            NovelId = result.NovelId,
            IsRegistered = isRegistered,
        };
    }
}
