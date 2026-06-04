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

    public async Task<int> DeleteOrphansAsync()
    {
        await _db.EnsureInitializedAsync();
        // SeriesId=NULL かつ全ユーザーフラグ0 の単発巻（発掘導線で一括お気に入り→解除した残骸等）を掃除する。
        return await _db.Connection.ExecuteAsync(
            "DELETE FROM Book WHERE SeriesId IS NULL AND IsPurchased=0 AND IsFavorite=0 AND IsCalendarRegistered=0 AND IsNewDetected=0");
    }
}
