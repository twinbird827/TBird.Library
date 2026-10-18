using Android.Content;
using Android.OS.Storage;
using TBird.Core;

namespace ComicViewer.Sources;

/// <summary>SourceKey から読み込み元を引く。ローカルは端末にマウント中のボリュームから毎回検出する。</summary>
public class FileSourceProvider
{
    public const string PrimaryKey = "local:primary";

    private readonly SmbConnectionPool _smb;

    public FileSourceProvider(SmbConnectionPool smb)
    {
        _smb = smb;
    }

    public static bool IsLocal(string sourceKey) => sourceKey.StartsWith("local:", StringComparison.Ordinal);

    public IReadOnlyList<LocalFileSource> GetLocalVolumes()
    {
        var result = new List<LocalFileSource>();
        try
        {
            var context = Platform.AppContext;
            if (context.GetSystemService(Context.StorageService) is not StorageManager manager) return result;
            foreach (var volume in manager.StorageVolumes)
            {
                if (volume.State != Android.OS.Environment.MediaMounted || volume.Directory is null) continue;
                var key = volume.IsPrimary ? PrimaryKey : $"local:{volume.Uuid}";
                var name = volume.IsPrimary ? "内部ストレージ" : volume.GetDescription(context) ?? volume.Uuid ?? "SD カード";
                var source = new LocalFileSource(key, name, volume.Directory.AbsolutePath);
                if (volume.IsPrimary) result.Insert(0, source);
                else result.Add(source);
            }
        }
        catch (Exception ex)
        {
            MessageService.Exception(ex);
        }
        return result;
    }

    /// <summary>読み込み元が存在しなければ null（SD 抜去・SMB 削除済み）。</summary>
    public async Task<IFileSource?> GetAsync(string sourceKey)
    {
        if (IsLocal(sourceKey)) return GetLocalVolumes().FirstOrDefault(x => x.SourceKey == sourceKey);
        return await _smb.GetAsync(sourceKey).ConfigureAwait(false);
    }
}
