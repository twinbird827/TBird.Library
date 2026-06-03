namespace NewReleaseChecker.Core.Services;

/// <summary>
/// シリーズ同定（著者集合の一致判定）。要件 §3.2.1 / §7.2。
/// 正規化後の「登録時著者集合 = 検索結果側著者集合」（集合一致。部分集合 ⊆ ではない）。
/// </summary>
public static class SeriesIdentifier
{
    /// <summary>候補巻の著者が登録時著者集合と集合一致するか。</summary>
    public static bool IsSameSeries(IReadOnlySet<string> registeredAuthorSet, string? candidateAuthor)
    {
        var candidateSet = AuthorNormalizer.ToSet(candidateAuthor);
        return registeredAuthorSet.SetEquals(candidateSet);
    }
}
