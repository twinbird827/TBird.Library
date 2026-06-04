using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;

namespace NewReleaseChecker.Data.Database;

/// <summary>
/// Book テーブルの Repository。
/// 書誌更新（UpdateBibliographyAsync）はユーザーフラグ列を絶対に触らないよう明示 SQL を用いる。
/// </summary>
public sealed class BookRepository : IBookRepository
{
    private readonly NewReleaseDatabase _db;

    public BookRepository(NewReleaseDatabase db) => _db = db;

    public async Task<Book?> GetAsync(int id)
    {
        await _db.EnsureInitializedAsync();
        return await _db.Connection.FindAsync<Book>(id);
    }

    public async Task<Book?> GetByItemNumberAsync(string itemNumber)
    {
        await _db.EnsureInitializedAsync();
        return await _db.Connection.Table<Book>().Where(b => b.ItemNumber == itemNumber).FirstOrDefaultAsync();
    }

    public async Task<IReadOnlyList<Book>> GetBySeriesAsync(int seriesId)
    {
        await _db.EnsureInitializedAsync();
        // 発売日順。NULL は末尾。
        return await _db.Connection.QueryAsync<Book>(
            "SELECT * FROM Book WHERE SeriesId = ? ORDER BY (ReleaseDate IS NULL), ReleaseDate ASC", seriesId);
    }

    public async Task<IReadOnlyList<Book>> GetFavoritesAsync()
    {
        await _db.EnsureInitializedAsync();
        return await _db.Connection.Table<Book>().Where(b => b.IsFavorite == 1).ToListAsync();
    }

    public async Task<IReadOnlyList<Book>> GetAllAsync()
    {
        await _db.EnsureInitializedAsync();
        return await _db.Connection.Table<Book>().ToListAsync();
    }

    public async Task<int> InsertAsync(Book book)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.InsertAsync(book);
        return book.Id;
    }

    public async Task UpdateBibliographyAsync(Book book)
    {
        await _db.EnsureInitializedAsync();
        // ユーザーフラグ列（IsPurchased/IsFavorite/IsCalendarRegistered/IsNewDetected/DetectedAt）は除外
        await _db.Connection.ExecuteAsync(
            "UPDATE Book SET Isbn=?, Title=?, Author=?, Publisher=?, ReleaseDate=?, ImageUrl=?, ItemUrl=?, Caption=? WHERE Id=?",
            book.Isbn, book.Title, book.Author, book.Publisher, book.ReleaseDate, book.ImageUrl, book.ItemUrl, book.Caption, book.Id);
    }

    public async Task UpdateFlagsAsync(Book book)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.ExecuteAsync(
            "UPDATE Book SET IsPurchased=?, IsFavorite=?, IsCalendarRegistered=?, IsNewDetected=?, DetectedAt=? WHERE Id=?",
            book.IsPurchased, book.IsFavorite, book.IsCalendarRegistered, book.IsNewDetected, book.DetectedAt, book.Id);
    }

    public async Task SetSeriesIdAsync(int bookId, int seriesId)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.ExecuteAsync("UPDATE Book SET SeriesId=? WHERE Id=?", seriesId, bookId);
    }

    public async Task DeleteBySeriesAsync(int seriesId)
    {
        await _db.EnsureInitializedAsync();
        await _db.Connection.ExecuteAsync("DELETE FROM Book WHERE SeriesId=?", seriesId);
    }

    public async Task<int> DeleteOrphansAsync(DateTime insertedBefore)
    {
        await _db.EnsureInitializedAsync();
        // SeriesId=NULL かつ全ユーザーフラグ0 の単発巻（発掘導線で一括お気に入り→解除した残骸等）を掃除する。
        // ただし DetectedAt が insertedBefore 以降（＝ごく最近 INSERT された行）は、巻詳細・一括操作の
        // 「INSERT→フラグ更新」途中で一時的に孤児に見えているだけの可能性があるため対象外とする。
        // ※ cutoff は DetectedAt 書き込み（DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")）と同じ無カルチャ書式で
        //   生成し、TEXT 同士の辞書順（＝固定幅 ISO 風で時系列順）比較が成立するようにする。
        var cutoff = insertedBefore.ToString("yyyy-MM-ddTHH:mm:ss");
        return await _db.Connection.ExecuteAsync(
            "DELETE FROM Book WHERE SeriesId IS NULL AND IsPurchased=0 AND IsFavorite=0 AND IsCalendarRegistered=0 AND IsNewDetected=0 AND (DetectedAt IS NULL OR DetectedAt < ?)",
            cutoff);
    }
}
