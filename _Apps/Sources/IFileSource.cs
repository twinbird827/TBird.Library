using ComicViewer.Models;

namespace ComicViewer.Sources;

/// <summary>Path は読み込み元ルートからの相対パス（区切り「/」、先頭「/」なし、ルートは空文字）。</summary>
public interface IFileSource
{
    string SourceKey { get; }

    string DisplayName { get; }

    bool IsRemote { get; }

    /// <summary>直下のエントリ。フォルダが無ければ DirectoryNotFoundException。</summary>
    Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct);

    /// <summary>シーク可能な読み取りストリーム。</summary>
    Task<Stream> OpenReadAsync(string path, CancellationToken ct);
}
