using System.Globalization;
using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using SQLite;

namespace NewReleaseChecker.Core.Services;

/// <summary>
/// 巻のフラグ操作に伴う共通ロジック。非永続巻（RakutenBook）の SeriesId=NULL 永続化を集約する。
/// 巻詳細（BookDetailViewModel）と一覧の一括操作（SelectableBookListViewModel 派生）で共有し、
/// INSERT-on-demand のコピペを排除する。
/// </summary>
public sealed class BookActionService
{
    private readonly IBookRepository _book;
    public BookActionService(IBookRepository book) => _book = book;

    /// <summary>
    /// 非永続巻を SeriesId=NULL で永続化して返す。既存（ItemNumber 一致）があればそれを返す。
    /// 裏のチェックと同 ItemNumber を同時 INSERT して UNIQUE に抵触した場合は、読み直して既存行を返す。
    /// </summary>
    public async Task<Book> EnsurePersistedAsync(RakutenBook src)
    {
        var existing = await _book.GetByItemNumberAsync(src.ItemNumber);
        if (existing is not null) return existing;

        var b = new Book
        {
            SeriesId = null,
            ItemNumber = src.ItemNumber,
            Isbn = src.Isbn,
            Title = src.Title,
            Author = string.IsNullOrEmpty(src.Author) ? null : src.Author,
            Publisher = string.IsNullOrEmpty(src.Publisher) ? null : src.Publisher,
            ReleaseDate = ReleaseDateParser.ToIso(src.SalesDate),
            ImageUrl = string.IsNullOrEmpty(src.ImageUrl) ? null : src.ImageUrl,
            ItemUrl = src.ItemUrl,
            Caption = src.Caption,
            // DeleteOrphansAsync の cutoff と TEXT 辞書順比較が成立するよう、不変書式で書き込む。
            DetectedAt = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture),
        };
        try
        {
            b.Id = await _book.InsertAsync(b);
            return b;
        }
        catch (SQLiteException)
        {
            // UNIQUE 競合等の DB 例外のみ対象。読み直して既存があればそれを採用、無ければ再送出。
            // （無関係な例外を握り潰さないよう SQLiteException に限定する）
            var raced = await _book.GetByItemNumberAsync(src.ItemNumber);
            if (raced is not null) return raced;
            throw;
        }
    }
}
