using CommunityToolkit.Mvvm.ComponentModel;
using LanobeReader.Models;

namespace LanobeReader.ViewModels;

public partial class SearchResultViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _author = string.Empty;

    [ObservableProperty]
    private int _totalEpisodes;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private string _siteTypeLabel = string.Empty;

    [ObservableProperty]
    private SiteType _siteType;

    [ObservableProperty]
    private string _novelId = string.Empty;

    [ObservableProperty]
    private bool _isRegistered;

    [ObservableProperty]
    private bool _isRegistering;

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
