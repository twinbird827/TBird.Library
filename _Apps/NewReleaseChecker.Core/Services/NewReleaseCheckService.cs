using NewReleaseChecker.Core.Abstractions;
using NewReleaseChecker.Core.Models;
using TBird.Core;

namespace NewReleaseChecker.Core.Services;

/// <summary>
/// 新刊チェックの共通サービス（要件 §3.2.6 / §7.1）。
/// 「タイトル検索 → 除外フィルタ → 著者集合一致判定 → 差分判定 → INSERT → お気に入り自動登録 → 通知」を
/// 単一サービスとして実装し、自動（WorkManager）・手動（更新ボタン）の両方から呼ぶ。
/// シリーズ登録（F-001）の既刊収集も本サービスの取り込みロジックを共有する。
/// </summary>
public sealed class NewReleaseCheckService
{
    /// <summary>1 回の Work あたりのチェック上限シリーズ数（要件 §6.1 / §7.6）。</summary>
    public const int MaxSeriesPerWork = 50;

    private readonly IRakutenApiClient _api;
    private readonly ISeriesRepository _series;
    private readonly IBookRepository _book;
    private readonly ILocalNotifier _notifier;
    private readonly IPreferencesService _prefs;

    public NewReleaseCheckService(
        IRakutenApiClient api,
        ISeriesRepository series,
        IBookRepository book,
        ILocalNotifier notifier,
        IPreferencesService prefs)
    {
        _api = api;
        _series = series;
        _book = book;
        _notifier = notifier;
        _prefs = prefs;
    }

    /// <summary>
    /// 共通チェック本体。LastCheckedAt NULL 最優先・古い順で最大 50 シリーズをチェックする。
    /// 予約検知（未発売の新刊）があれば通知 ON 時に 1 件へ集約して通知する。
    /// </summary>
    public async Task<CheckSummary> CheckAsync(CheckTrigger trigger, CancellationToken ct = default)
    {
        var targets = await _series.GetCheckTargetsAsync(MaxSeriesPerWork);
        MessageService.Info($"新刊チェック開始: {trigger}, 対象={targets.Count}件");

        var excludes = _prefs.ExcludeKeywords;
        var now = DateTime.Now;
        var nowIso = now.ToString("yyyy-MM-ddTHH:mm:ss");
        var reservations = new List<(Book Book, Series Series)>();
        int totalNew = 0;

        // 既存巻を ItemNumber で一括取得して辞書引きする（候補ごとの単発 SELECT による N+1 を回避）。
        // INSERT した新刊は同辞書へ反映し、同一 Work 内で別シリーズの候補が同じ ItemNumber を二重 INSERT
        // して UNIQUE 制約に抵触するのを防ぐ。
        var existingByItemNumber = (await _book.GetAllAsync())
            .ToDictionary(b => b.ItemNumber, StringComparer.Ordinal);

        foreach (var s in targets)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var registeredSet = AuthorNormalizer.ParseStored(s.AuthorSet);
                var candidates = await _api.SearchByKeywordAsync(s.SeriesKey, ct);

                foreach (var c in candidates)
                {
                    if (ContainsExcludeKeyword(c.Title, excludes)) continue;
                    if (!SeriesIdentifier.IsSameSeries(registeredSet, c.Author)) continue;

                    var (isNew, book) = await UpsertAsync(c, s.Id, now, nowIso, markNewDetected: true, existingByItemNumber);
                    if (isNew)
                    {
                        totalNew++;
                        if (book.IsNewDetected == 1) reservations.Add((book, s));
                    }
                }

                await _series.TouchLastCheckedAsync(s.Id, nowIso);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // 1 シリーズの失敗で全体を止めない。API/DB エラーはファイルに残す（Error）。
                MessageService.Error($"シリーズチェック失敗: SeriesId={s.Id} '{s.SeriesKey}': {ex.Message}");
            }
        }

        int notified = 0;
        if (reservations.Count > 0)
        {
            if (_prefs.NotificationEnabled)
            {
                await NotifyReservationsAsync(reservations);
                notified = reservations.Count;
            }
            // 通知の有無に関わらず IsNewDetected を降ろす。通知 OFF のまま放置すると INSERT 時に立てた
            // IsNewDetected=1 が残留し、再有効化後はその巻が既存扱いで再検知されず永久に未通知になるため。
            foreach (var (book, _) in reservations)
            {
                book.IsNewDetected = 0;
                await _book.UpdateFlagsAsync(book);
            }
        }

        MessageService.Info($"新刊チェック終了: {trigger}, 検知={totalNew}件, 予約通知={notified}件");
        return new CheckSummary(targets.Count, totalNew, reservations.Count);
    }

    /// <summary>
    /// シリーズを登録し、既刊を収集して取り込む（F-001）。登録時は通知せず、IsNewDetected も立てない。
    /// 戻り値は登録した Series.Id。
    /// </summary>
    public async Task<int> RegisterSeriesAsync(SeriesRegistration reg, CancellationToken ct = default)
    {
        var now = DateTime.Now;
        var nowIso = now.ToString("yyyy-MM-ddTHH:mm:ss");

        var authorSet = reg.SelectedAuthors
            .Select(AuthorNormalizer.NormalizeName)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToHashSet(StringComparer.Ordinal);

        var series = new Series
        {
            SeriesKey = reg.SeriesKey,
            AuthorSet = AuthorNormalizer.ToStored(authorSet),
            MediaType = reg.MediaType,
            RegisteredAt = nowIso,
            LastCheckedAt = nowIso,
        };
        var seriesId = await _series.InsertAsync(series);

        var excludes = _prefs.ExcludeKeywords;
        try
        {
            // 既刊収集も ItemNumber 一括取得で N+1 を避ける（INSERT 分は辞書へ反映）。
            var existingByItemNumber = (await _book.GetAllAsync())
                .ToDictionary(b => b.ItemNumber, StringComparer.Ordinal);

            var candidates = await _api.SearchByKeywordAsync(reg.SeriesKey, ct);
            foreach (var c in candidates)
            {
                if (ContainsExcludeKeyword(c.Title, excludes)) continue;
                if (!SeriesIdentifier.IsSameSeries(authorSet, c.Author)) continue;

                // 登録時の既刊は通知対象外（markNewDetected: false）
                await UpsertAsync(c, seriesId, now, nowIso, markNewDetected: false, existingByItemNumber);
            }
        }
        catch (Exception ex)
        {
            MessageService.Error($"シリーズ登録時の既刊収集に失敗: '{reg.SeriesKey}': {ex.Message}");
        }

        return seriesId;
    }

    /// <summary>
    /// 候補巻を DB に反映する。
    /// 新刊（DB に無い ItemNumber）なら INSERT（IsFavorite=1、予約検知かつ markNewDetected なら IsNewDetected=1）。
    /// 既存巻なら書誌列のみ上書き（ユーザーフラグ列は保護）。SeriesId=NULL の既存巻のみ当該シリーズを設定。
    /// </summary>
    private async Task<(bool IsNew, Book Book)> UpsertAsync(
        RakutenBook c, int seriesId, DateTime now, string nowIso, bool markNewDetected,
        Dictionary<string, Book> existingByItemNumber)
    {
        var iso = ReleaseDateParser.ToIso(c.SalesDate);
        existingByItemNumber.TryGetValue(c.ItemNumber, out var existing);

        if (existing == null)
        {
            var isFuture = ReleaseDateParser.IsFuture(iso, now);
            var book = new Book
            {
                SeriesId = seriesId,
                ItemNumber = c.ItemNumber,
                Isbn = c.Isbn,
                Title = c.Title,
                Author = c.Author,
                Publisher = c.Publisher,
                ReleaseDate = iso,
                ImageUrl = c.ImageUrl,
                ItemUrl = c.ItemUrl,
                Caption = c.Caption,
                IsFavorite = 1,
                IsNewDetected = (markNewDetected && isFuture) ? 1 : 0,
                DetectedAt = nowIso,
            };
            book.Id = await _book.InsertAsync(book);
            existingByItemNumber[c.ItemNumber] = book; // 同一 Run 内の二重 INSERT 防止
            return (true, book);
        }

        // 既存巻: 書誌列のみ上書き
        existing.Isbn = c.Isbn;
        existing.Title = c.Title;
        existing.Author = c.Author;
        existing.Publisher = c.Publisher;
        existing.ReleaseDate = iso;
        existing.ImageUrl = c.ImageUrl;
        existing.ItemUrl = c.ItemUrl;
        existing.Caption = c.Caption;
        await _book.UpdateBibliographyAsync(existing);

        // SeriesId=NULL の単発巻が同定ヒットしたら当該シリーズに帰属させる
        if (existing.SeriesId == null)
        {
            await _book.SetSeriesIdAsync(existing.Id, seriesId);
            existing.SeriesId = seriesId;
        }

        return (false, existing);
    }

    private async Task NotifyReservationsAsync(IReadOnlyList<(Book Book, Series Series)> reservations)
    {
        var first = reservations[0].Series.SeriesKey;
        string message = reservations.Count == 1
            ? $"「{first}」の新刊が予約開始"
            : $"「{first}」ほか{reservations.Count - 1}件の新刊が予約開始";

        int? tapSeriesId = reservations.Count == 1 ? reservations[0].Series.Id : null;
        await _notifier.ShowAsync("新刊チェッカー", message, tapSeriesId);
        MessageService.Info($"通知発行: {message}");
    }

    private static bool ContainsExcludeKeyword(string title, IReadOnlyList<string> excludes)
    {
        if (string.IsNullOrEmpty(title)) return false;
        foreach (var kw in excludes)
        {
            if (!string.IsNullOrEmpty(kw) && title.Contains(kw, StringComparison.Ordinal)) return true;
        }
        return false;
    }
}
