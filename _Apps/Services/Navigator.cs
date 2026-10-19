using ComicViewer.Logic;
using ComicViewer.Models;

namespace ComicViewer.Services;

public static class Navigator
{
    public const string Folder = "folder";
    public const string Viewer = "viewer";
    public const string SmbEdit = "smbedit";
    public const string Settings = "settings";

    /// <summary>
    /// フォルダ一覧を開く。fromTop は S01 を根にスタックを積み直す（ドロワーからの遷移）。
    /// openPath を渡すと読込後にその漫画を開く（履歴から開くとき）。
    /// </summary>
    public static Task OpenFolderAsync(string sourceKey, string path, bool fromTop = false, string? openPath = null)
    {
        var parameters = new ShellNavigationQueryParameters
        {
            ["source"] = sourceKey,
            ["path"] = path,
        };
        if (openPath is not null) parameters["open"] = openPath;
        return Shell.Current.GoToAsync(fromTop ? $"//top/{Folder}" : Folder, parameters);
    }

    // 親フォルダを読み込んだ S02 を経由して開く（前後の漫画の決定と戻り先のため）。
    public static Task OpenComicFromTopAsync(string sourceKey, string path)
        => OpenFolderAsync(sourceKey, EntryRules.Parent(path), fromTop: true, openPath: path);

    /// <summary>start = -1 は最終単位から開く（前の漫画へ移動したとき）。</summary>
    public static Task OpenViewerAsync(ComicRef comic, int start)
        => Shell.Current.GoToAsync(Viewer, new ShellNavigationQueryParameters { ["comic"] = comic, ["start"] = start });

    public static Task OpenSmbEditAsync(int? id)
        => Shell.Current.GoToAsync(SmbEdit, new ShellNavigationQueryParameters { ["id"] = id ?? 0 });

    public static Task BackAsync() => Shell.Current.GoToAsync("..");
}
