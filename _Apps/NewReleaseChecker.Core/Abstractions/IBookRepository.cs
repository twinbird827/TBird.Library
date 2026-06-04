using NewReleaseChecker.Core.Models;

namespace NewReleaseChecker.Core.Abstractions;

public interface IBookRepository
{
    Task<Book?> GetAsync(int id);
    Task<Book?> GetByItemNumberAsync(string itemNumber);
    Task<IReadOnlyList<Book>> GetBySeriesAsync(int seriesId);
    Task<IReadOnlyList<Book>> GetFavoritesAsync();
    Task<IReadOnlyList<Book>> GetAllAsync();

    Task<int> InsertAsync(Book book);

    /// <summary>書誌列のみ上書き更新（ユーザーフラグ列 IsPurchased/IsFavorite/IsCalendarRegistered/IsNewDetected/DetectedAt は変更しない）。</summary>
    Task UpdateBibliographyAsync(Book book);

    /// <summary>ユーザーフラグ列を更新（トグル操作等）。</summary>
    Task UpdateFlagsAsync(Book book);

    /// <summary>SeriesId=NULL の単発巻に SeriesId を設定する。</summary>
    Task SetSeriesIdAsync(int bookId, int seriesId);

    /// <summary>指定シリーズに属する全巻を削除（明示カスケード）。</summary>
    Task DeleteBySeriesAsync(int seriesId);

    /// <summary>
    /// SeriesId=NULL かつ全ユーザーフラグ0 の孤児行のうち、DetectedAt が <paramref name="insertedBefore"/> より前
    /// （または NULL）の行のみを一括削除する（方式C: 遅延掃除）。削除件数を返す。
    /// 直近 INSERT 行を除外するのは、巻詳細・一括操作の「INSERT→フラグ更新」の途中行（フラグ未設定で一時的に孤児）を
    /// 誤削除してユーザー操作をサイレントに失わせないため。
    /// </summary>
    Task<int> DeleteOrphansAsync(DateTime insertedBefore);
}
