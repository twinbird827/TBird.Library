using CommunityToolkit.Mvvm.ComponentModel;
using ComicViewer.Logic;
using ComicViewer.Models;

namespace ComicViewer.ViewModels;

public enum FolderItemKind
{
    Folder,
    Comic,
}

public partial class FolderItemViewModel : ObservableObject
{
    public const string DirectImagesName = "（このフォルダの画像）";

    public FolderItemViewModel(FolderItemKind kind, string name, string path, ComicRef? comic)
    {
        ItemKind = kind;
        Name = name;
        (DisplayName, Author) = TitleParser.Parse(name);
        Path = path;
        Comic = comic;
    }

    public FolderItemKind ItemKind { get; }

    public string Name { get; }

    public string DisplayName { get; }

    public string Author { get; }

    public string KindBadge => EntryRules.KindBadge(Path);

    public string Path { get; }

    public ComicRef? Comic { get; }

    public bool IsFolder => ItemKind == FolderItemKind.Folder;

    public bool HasDetail => Author.Length > 0 || !IsFolder;

    [ObservableProperty]
    public partial string StatusText { get; set; } = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayImage))]
    public partial ImageSource? ThumbnailSource { get; set; }

    public ImageSource DisplayImage => ThumbnailSource ?? (IsFolder ? "folder.png" : "comic.png");

    public static string GetStatusText(ReadingState? state) => state switch
    {
        null => "未読",
        { IsRead: true } => "既読",
        { PageCount: > 0 } => $"{state.PageIndex + 1}/{state.PageCount}",
        _ => "未読",
    };

    /// <summary>F-010 の開始ページ規則。</summary>
    public static int GetStartPage(ReadingState? state) => state is null || state.IsRead ? 0 : state.PageIndex;
}
