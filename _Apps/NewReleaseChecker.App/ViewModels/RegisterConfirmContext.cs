using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NewReleaseChecker.App.Models;
using NewReleaseChecker.Core.Services;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>登録確認ダイアログ（SCR-004 の登録ダイアログ）のバインディングコンテキスト。</summary>
public sealed partial class RegisterConfirmContext : ObservableObject
{
    public RegisterConfirmContext(string seriesKey, IEnumerable<AuthorSelection> authors, int mediaTab)
    {
        SeriesKey = seriesKey;
        MediaTab = mediaTab;
        foreach (var a in authors) Authors.Add(a);
    }

    [ObservableProperty] private string _seriesKey;

    /// <summary>0=ラノベ, 1=コミック。</summary>
    [ObservableProperty] private int _mediaTab;

    public ObservableCollection<AuthorSelection> Authors { get; } = new();

    public SeriesRegistration Build() => new()
    {
        SeriesKey = (SeriesKey ?? string.Empty).Trim(),
        SelectedAuthors = Authors.Where(a => a.IsSelected).Select(a => a.Name).ToList(),
        MediaType = MediaTab == 1 ? Core.MediaType.Comic : Core.MediaType.Novel,
    };
}
