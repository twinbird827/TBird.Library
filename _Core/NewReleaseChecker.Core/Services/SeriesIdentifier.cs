namespace NewReleaseChecker.Core.Services;

/// <summary>
/// シリーズ同定（著者集合の包含判定）。要件 §3.2.1 / §7.2。
/// 正規化後の「登録時著者集合 ⊆ 検索結果側著者集合」（部分集合一致）。
/// 登録時に選択した著者が候補側に全員含まれていれば同定成立とし、候補側に
/// 未選択の著者（作画/イラスト等）が追加で含まれていても対象とする。
/// （例:「魔術師クノンは見えている」を著者「南野海風」のみで登録しても、作画者を含む各巻にヒットさせる）。
/// </summary>
public static class SeriesIdentifier
{
    /// <summary>候補巻の著者集合が登録時著者集合を包含するか（登録著者が全員含まれるか）。</summary>
    public static bool IsSameSeries(IReadOnlySet<string> registeredAuthorSet, string? candidateAuthor)
    {
        // 登録著者が空なら同定不能（空集合は全候補の部分集合になり全ヒットしてしまうため明示的に弾く）。
        if (registeredAuthorSet.Count == 0) return false;

        var candidateSet = AuthorNormalizer.ToSet(candidateAuthor);
        return registeredAuthorSet.IsSubsetOf(candidateSet);
    }
}
