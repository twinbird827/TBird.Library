namespace ComicViewer.Logic;

public readonly record struct CacheEntry(string Key, long Size, long LastAccessedAt);

public static class CacheEvictionPlanner
{
    /// <summary>上限以下に収めるために削除するキー。openKey は削除しない。</summary>
    public static List<string> Plan(IEnumerable<CacheEntry> entries, long limitBytes, string? openKey)
    {
        var list = entries.ToList();
        var total = list.Sum(x => x.Size);
        var result = new List<string>();
        foreach (var e in list.Where(x => x.Key != openKey).OrderBy(x => x.LastAccessedAt))
        {
            if (total <= limitBytes) break;
            result.Add(e.Key);
            total -= e.Size;
        }
        return result;
    }
}
