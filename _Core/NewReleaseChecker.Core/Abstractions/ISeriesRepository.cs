using NewReleaseChecker.Core.Models;

namespace NewReleaseChecker.Core.Abstractions;

public interface ISeriesRepository
{
    Task<IReadOnlyList<Series>> GetAllAsync();
    Task<Series?> GetAsync(int id);
    Task<int> InsertAsync(Series series);
    Task UpdateAsync(Series series);
    Task DeleteAsync(int id);

    /// <summary>チェック対象を取得。LastCheckedAt が NULL を最優先、次いで古い順。最大 max 件。</summary>
    Task<IReadOnlyList<Series>> GetCheckTargetsAsync(int max);

    /// <summary>LastCheckedAt を更新する。</summary>
    Task TouchLastCheckedAsync(int seriesId, string isoNow);
}
