using System.IO.Compression;
using System.Text;
using ComicViewer.Logic;

namespace ComicViewer.Comics;

public class ZipComicReader : ImageComicReader
{
    private readonly ZipArchive _archive;
    private readonly Dictionary<string, ZipArchiveEntry> _entries;
    // ZipArchive はスレッド安全でないため、エントリの読み出しを 1 本ずつにする。
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ZipComicReader(ZipArchive archive, Dictionary<string, ZipArchiveEntry> entries)
        : base(entries.Keys.OrderBy(x => x, NaturalComparer.Instance).ToList())
    {
        _archive = archive;
        _entries = entries;
    }

    public static Task<ZipComicReader> OpenAsync(Stream stream, CancellationToken ct) => Task.Run(() =>
    {
        try
        {
            // UTF-8 フラグの無いエントリ名は日本の zip ツールの慣習に合わせて CP932 として読む。
            var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false, Encoding.GetEncoding(932));
            var entries = new Dictionary<string, ZipArchiveEntry>();
            foreach (var entry in archive.Entries)
            {
                var name = entry.FullName.Replace('\\', '/');
                if (entry.Name.Length == 0 || !EntryRules.IsImage(name) || EntryRules.IsIgnoredPath(name)) continue;
                entries.TryAdd(name, entry);
            }
            return new ZipComicReader(archive, entries);
        }
        catch
        {
            stream.Dispose();
            throw;
        }
    }, ct);

    protected override async Task<T> ReadPageAsync<T>(int index, Func<Stream, T> read, CancellationToken ct)
    {
        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() =>
            {
                using var stream = _entries[Pages[index]].Open();
                return read(stream);
            }, ct).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    protected override void DisposeManagedResource()
    {
        _gate.Wait();
        try
        {
            _archive.Dispose();
        }
        finally
        {
            _gate.Release();
        }
        base.DisposeManagedResource();
    }
}
