using ComicViewer.Models;

namespace ComicViewer.Sources;

public class LocalFileSource : IFileSource
{
    public LocalFileSource(string sourceKey, string displayName, string rootDirectory)
    {
        SourceKey = sourceKey;
        DisplayName = displayName;
        RootDirectory = rootDirectory;
    }

    public string SourceKey { get; }

    public string DisplayName { get; }

    public string RootDirectory { get; }

    public bool IsRemote => false;

    public string GetFullPath(string path) => path.Length == 0 ? RootDirectory : Path.Combine(RootDirectory, path);

    public Task<IReadOnlyList<FileEntry>> ListAsync(string path, CancellationToken ct) => Task.Run<IReadOnlyList<FileEntry>>(() =>
    {
        var dir = new DirectoryInfo(GetFullPath(path));
        if (!dir.Exists) throw new DirectoryNotFoundException(dir.FullName);
        var list = new List<FileEntry>();
        foreach (var info in dir.EnumerateFileSystemInfos())
        {
            ct.ThrowIfCancellationRequested();
            var isDir = info is DirectoryInfo;
            list.Add(new FileEntry(
                info.Name,
                isDir,
                isDir ? 0 : ((FileInfo)info).Length,
                new DateTimeOffset(info.LastWriteTimeUtc).ToUnixTimeMilliseconds()));
        }
        return list;
    }, ct);

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
        => Task.FromResult<Stream>(new FileStream(GetFullPath(path), FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024));
}
