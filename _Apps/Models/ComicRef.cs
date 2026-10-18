namespace ComicViewer.Models;

public enum ComicKind
{
    Zip,
    Pdf,
    ImageFolder,
    DirectImages,
}

/// <summary>読み込み元の 1 エントリ。ModifiedAt は UTC Unix ミリ秒。</summary>
public sealed record FileEntry(string Name, bool IsDirectory, long Size, long ModifiedAt);

/// <summary>開く漫画の識別。DirectImages の Path は「{フォルダ}/*」。</summary>
public sealed record ComicRef(string SourceKey, string Path, ComicKind Kind, string Name, long Size, long ModifiedAt)
{
    /// <summary>DirectImages なら対象フォルダ、それ以外は Path 自身。</summary>
    public string TargetPath => Kind == ComicKind.DirectImages ? ParentPath : Path;

    /// <summary>この漫画が並ぶ一覧のフォルダ。</summary>
    public string ParentPath => Logic.EntryRules.Parent(Path);
}
