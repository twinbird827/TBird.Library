using CommunityToolkit.Mvvm.ComponentModel;

namespace NewReleaseChecker.App.Models;

/// <summary>登録確認ダイアログの著者チェックボックス 1 項目。</summary>
public sealed partial class AuthorSelection : ObservableObject
{
    public AuthorSelection(string name, bool isSelected = true)
    {
        Name = name;
        IsSelected = isSelected;
    }

    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;
}
