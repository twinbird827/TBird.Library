using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NewReleaseChecker.Core.Abstractions;

namespace NewReleaseChecker.App.ViewModels;

/// <summary>除外キーワード編集（SCR-011 / F-008）。</summary>
public partial class ExcludeKeywordsViewModel : ObservableObject
{
    private readonly IPreferencesService _prefs;

    public ExcludeKeywordsViewModel(IPreferencesService prefs)
    {
        _prefs = prefs;
        Reload();
    }

    public ObservableCollection<string> Keywords { get; } = new();

    [ObservableProperty] private string _newKeyword = string.Empty;

    private void Reload()
    {
        Keywords.Clear();
        foreach (var k in _prefs.ExcludeKeywords) Keywords.Add(k);
    }

    private void Persist() => _prefs.ExcludeKeywords = Keywords.ToList();

    [RelayCommand]
    private void Add()
    {
        var kw = NewKeyword?.Trim();
        if (string.IsNullOrEmpty(kw) || Keywords.Contains(kw)) { NewKeyword = string.Empty; return; }
        Keywords.Add(kw);
        NewKeyword = string.Empty;
        Persist();
    }

    [RelayCommand]
    private void Remove(string? keyword)
    {
        if (keyword is null) return;
        Keywords.Remove(keyword);
        Persist();
    }

    [RelayCommand]
    private void Reset()
    {
        _prefs.ResetExcludeKeywords();
        Reload();
    }
}
