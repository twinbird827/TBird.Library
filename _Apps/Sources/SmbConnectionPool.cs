using ComicViewer.Data;
using TBird.Core;

namespace ComicViewer.Sources;

/// <summary>SourceKey ごとに SMB 接続を 1 本保持して使い回す。</summary>
public class SmbConnectionPool
{
    private readonly SmbSourceRepository _repository;
    private readonly Dictionary<string, SmbFileSource> _sources = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SmbConnectionPool(SmbSourceRepository repository) => _repository = repository;

    public static string PasswordKey(int id) => $"smb_pw_{id}";

    /// <summary>登録が無ければ null。</summary>
    public async Task<SmbFileSource?> GetAsync(string sourceKey)
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_sources.TryGetValue(sourceKey, out var cached)) return cached;
            if (!int.TryParse(sourceKey.AsSpan("smb:".Length), out var id)) return null;
            var row = await _repository.GetAsync(id).ConfigureAwait(false);
            if (row is null) return null;
            var source = new SmbFileSource(row, await GetPasswordAsync(id).ConfigureAwait(false));
            _sources[sourceKey] = source;
            return source;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>設定の変更・削除後に呼ぶ。次回使用時に新しい設定で接続し直す。</summary>
    public void Remove(string sourceKey)
    {
        _gate.Wait();
        try
        {
            if (_sources.Remove(sourceKey, out var source)) source.Dispose();
        }
        finally
        {
            _gate.Release();
        }
    }

    public static async Task<string> GetPasswordAsync(int id)
    {
        try
        {
            return await SecureStorage.GetAsync(PasswordKey(id)).ConfigureAwait(false) ?? "";
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
            return "";
        }
    }
}
