using System.Security.Cryptography;
using System.Text;
using ComicViewer.Data;
using ComicViewer.Logic;
using ComicViewer.Models;
using ComicViewer.Sources;
using TBird.Core;

namespace ComicViewer.Services;

public class PdfCacheService
{
    private readonly PdfCacheRepository _repository;

    public PdfCacheService(PdfCacheRepository repository) => _repository = repository;

    public static string ThumbDirectory => Path.Combine(FileSystem.CacheDirectory, "thumbs");

    public static string PdfDirectory => Path.Combine(FileSystem.CacheDirectory, "pdf");

    public static string Hash(string sourceKey, string path, long size, long modifiedAt)
        => Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes($"{sourceKey}|{path}|{size}|{modifiedAt}")));

    /// <summary>サイズと更新日時が一致するキャッシュがあればそのパス。不一致の古いキャッシュは消す。</summary>
    public async Task<string?> FindAsync(ComicRef comic)
    {
        var row = await _repository.GetAsync(comic.SourceKey, comic.Path).ConfigureAwait(false);
        if (row is null) return null;
        var file = Path.Combine(PdfDirectory, row.LocalFile);
        if (row.Size == comic.Size && row.ModifiedAt == comic.ModifiedAt && File.Exists(file))
        {
            await _repository.TouchAsync(row.Id).ConfigureAwait(false);
            return file;
        }
        TryDelete(file);
        await _repository.DeleteAsync(row.Id).ConfigureAwait(false);
        return null;
    }

    public async Task<string> DownloadAsync(IFileSource source, ComicRef comic, IProgress<long> progress, CancellationToken ct)
    {
        Directory.CreateDirectory(PdfDirectory);
        var name = Hash(comic.SourceKey, comic.Path, comic.Size, comic.ModifiedAt) + ".pdf";
        var file = Path.Combine(PdfDirectory, name);
        var part = file + ".part";
        try
        {
            await using (var input = await source.OpenReadAsync(comic.Path, ct).ConfigureAwait(false))
            await using (var output = new FileStream(part, FileMode.Create, FileAccess.Write))
            {
                var buffer = new byte[64 * 1024];
                long total = 0;
                int n;
                while ((n = await Task.Run(() => input.Read(buffer, 0, buffer.Length), ct).ConfigureAwait(false)) > 0)
                {
                    await output.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    total += n;
                    progress.Report(total);
                }
            }
            File.Move(part, file, overwrite: true);
        }
        catch
        {
            TryDelete(part);
            throw;
        }

        await _repository.UpsertAsync(new PdfCache
        {
            SourceKey = comic.SourceKey,
            Path = comic.Path,
            Size = comic.Size,
            ModifiedAt = comic.ModifiedAt,
            LocalFile = name,
            LastAccessedAt = ComicViewerDatabase.Now,
        }).ConfigureAwait(false);
        await EvictAsync(file).ConfigureAwait(false);
        return file;
    }

    /// <summary>上限を超えた分を古い順に消す。openFile（開いている pdf）は残す。</summary>
    public async Task EvictAsync(string? openFile)
    {
        try
        {
            var rows = await _repository.GetAllAsync().ConfigureAwait(false);
            var entries = rows.Select(x =>
            {
                var info = new FileInfo(Path.Combine(PdfDirectory, x.LocalFile));
                return new CacheEntry(info.FullName, info.Exists ? info.Length : 0, x.LastAccessedAt);
            });
            var openKey = openFile is null ? null : Path.GetFullPath(openFile);
            var targets = CacheEvictionPlanner.Plan(entries, AppSettings.CacheLimitMb * 1024L * 1024L, openKey).ToHashSet();
            foreach (var row in rows.Where(x => targets.Contains(Path.GetFullPath(Path.Combine(PdfDirectory, x.LocalFile)))))
            {
                TryDelete(Path.Combine(PdfDirectory, row.LocalFile));
                await _repository.DeleteAsync(row.Id).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
    }

    /// <summary>SMB 削除時に、その読み込み元の pdf キャッシュファイルを消す（行は SmbSourceRepository が消す）。</summary>
    public async Task DeleteFilesAsync(string sourceKey)
    {
        foreach (var row in (await _repository.GetAllAsync().ConfigureAwait(false)).Where(x => x.SourceKey == sourceKey))
        {
            TryDelete(Path.Combine(PdfDirectory, row.LocalFile));
        }
    }

    /// <summary>サムネイルと pdf キャッシュを全削除。消せないファイルがあれば false。</summary>
    public async Task<bool> ClearAllAsync()
    {
        var ok = true;
        foreach (var dir in new[] { ThumbDirectory, PdfDirectory })
        {
            if (!Directory.Exists(dir)) continue;
            foreach (var file in Directory.EnumerateFiles(dir))
            {
                ok &= TryDelete(file);
            }
        }
        await _repository.DeleteAllAsync().ConfigureAwait(false);
        return ok;
    }

    public static long GetUsageBytes() => new[] { ThumbDirectory, PdfDirectory }
        .Where(Directory.Exists)
        .SelectMany(x => new DirectoryInfo(x).EnumerateFiles())
        .Sum(x => x.Length);

    public static bool IsDiskFull(Exception ex)
        => ex is IOException && (ex.Message.Contains("ENOSPC", StringComparison.Ordinal) || ex.Message.Contains("No space left", StringComparison.OrdinalIgnoreCase));

    private static bool TryDelete(string file)
    {
        try
        {
            File.Delete(file);
            return true;
        }
        catch (IOException ex)
        {
            MessageService.Warn($"Delete failed: {file} {ex.Message}");
            return false;
        }
    }
}
