using CommunityToolkit.Mvvm.Messaging;
using LanobeReader.Helpers;
using LanobeReader.Models;
using LanobeReader.Services.Background;
using LanobeReader.Services.Database;
using Microsoft.Maui.Storage;
using TBird.Core;
using TBird.Maui.Background;

namespace LanobeReader.Services;

public class UpdateCheckService
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    private readonly NovelRepository _novelRepo;
    private readonly EpisodeRepository _episodeRepo;
    private readonly INovelServiceFactory _serviceFactory;
    private readonly BackgroundJobQueue? _jobQueue;

    public UpdateCheckService(
        NovelRepository novelRepo,
        EpisodeRepository episodeRepo,
        INovelServiceFactory serviceFactory,
        BackgroundJobQueue? jobQueue = null)
    {
        _novelRepo = novelRepo;
        _episodeRepo = episodeRepo;
        _serviceFactory = serviceFactory;
        _jobQueue = jobQueue;
    }

    public async Task<List<(Novel novel, int newEpisodeCount)>> CheckAllAsync(CancellationToken ct = default)
    {
        if (!await _semaphore.WaitAsync(0, ct).ConfigureAwait(false))
        {
            MessageService.Warn("Update check already running, skipping");
            return [];
        }

        try
        {
            // 「最後にチェックした時刻が古い順(未チェック=null 優先)」で回す。3分上限(shortService)
            // 等で打ち切られても、次回が続きから拾える (ラウンドロビン) ようにするため。
            var novels = await _novelRepo.GetAllForCheckAsync().ConfigureAwait(false);
            var updates = new List<(Novel, int)>();
            // 全作品を回りきったかどうか。打ち切り(キャンセル/3分上限)で抜けた場合は false とし、
            // 完了時刻の記録(=アラームのバックストップ抑止)を行わない。
            var completedFullSweep = true;
            // 1件以上を実際にチェックできたか。全件失敗(ネット断等)や空テーブルでの「完了」記録による
            // アラーム冗長ゲートの無用な抑止を避けるため、完了時刻記録の条件に用いる。
            var anySuccess = false;

            foreach (var novel in novels)
            {
                if (ct.IsCancellationRequested) { completedFullSweep = false; break; }

                // 取得自体は常に行う。HasUnconfirmedUpdate=true の小説をスキップすると、
                // ユーザがアプリを開かない限り更新追跡から脱落する問題があったため (H-2)。
                // 通知は notificationId=novel.Id で上書き表示されるため重複通知にはならない。

                var cancelled = false;
                var hadError = novel.HasCheckError;
                var persisted = false;
                try
                {
                    var service = _serviceFactory.GetService((SiteType)novel.SiteType);
                    var (totalEpisodes, lastUpdatedAt, isCompleted, author) = await service.FetchNovelInfoAsync(novel.NovelId, ct).ConfigureAwait(false);

                    var currentMaxEpisode = await _episodeRepo.GetMaxEpisodeNoAsync(novel.Id).ConfigureAwait(false);

                    if (totalEpisodes > currentMaxEpisode)
                    {
                        // Fetch new episodes
                        var allEpisodes = await service.FetchEpisodeListAsync(novel.NovelId, ct).ConfigureAwait(false);
                        var newEpisodes = allEpisodes
                            .Where(e => e.EpisodeNo > currentMaxEpisode)
                            .Select(e => { e.NovelId = novel.Id; return e; })
                            .ToList();

                        if (newEpisodes.Count > 0)
                        {
                            await _episodeRepo.InsertAllAsync(newEpisodes).ConfigureAwait(false);

                            novel.TotalEpisodes = totalEpisodes;
                            novel.LastUpdatedAt = lastUpdatedAt ?? DateTime.UtcNow.ToString("o");
                            novel.HasUnconfirmedUpdate = true;
                            novel.IsCompleted = isCompleted;
                            if (!string.IsNullOrEmpty(author) && string.IsNullOrEmpty(novel.Author))
                            {
                                novel.Author = author;
                            }
                            novel.HasCheckError = false;

                            // 挿入した episodes と novel メタデータ(HasUnconfirmedUpdate/TotalEpisodes 等)は
                            // 一括で確定させる。永続化を後続(プリフェッチ enqueue / ループ末尾)へ遅延すると、
                            // その窓でプロセスが kill / 3分上限で打ち切られた場合に episodes だけ commit され
                            // novel 行が古いまま残り、次回 GetMaxEpisodeNoAsync が新 max を返すため新着が
                            // 二度と再検出されない(NEW 喪失)。挿入直後に永続化して窓を最小化する。
                            novel.LastCheckedAt = DateTime.UtcNow.ToString("o");
                            await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);
                            persisted = true;

                            updates.Add((novel, newEpisodes.Count));

                            // Enqueue newly-added episodes for background prefetch (Wi-Fi gated)
                            // InsertAllAsync は AutoIncrement の Id を newEpisodes へ採番済みのため、
                            // 全件再クエリ(GetByNovelIdAsync)で取り直さず、そのまま enqueue する。
                            if (_jobQueue is not null)
                            {
                                foreach (var ep in newEpisodes)
                                {
                                    await _jobQueue.EnqueueAsync(new PrefetchEpisodeJob
                                    {
                                        NovelDbId = novel.Id,
                                        EpisodeDbId = ep.Id,
                                        EpisodeNo = ep.EpisodeNo,
                                        SiteType = novel.SiteType,
                                        SiteNovelId = novel.NovelId,
                                    }, novel.IsFavorite ? JobPriority.High : JobPriority.Normal).ConfigureAwait(false);
                                }
                            }
                        }
                    }

                    novel.HasCheckError = false; // 成功時はエラーフラグを解除
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested)
                    {
                        // 打ち切り(3分上限等)。この作品は LastCheckedAt を更新せず、
                        // 次回この作品から再開できるようにループを抜ける。
                        cancelled = true;
                    }
                    else
                    {
                        // ネットワーク/パース/DB 等、その作品固有のあらゆる失敗を握りつぶして次へ進む。
                        // ここを HttpRequestException 系に限定すると、想定外の例外で foreach 全体が脱出し
                        // LastCheckedAt が前進しないため、巡回の先頭(最古)に居座る poison 作品が
                        // 全作品のチェックを永久に止めてしまう。広く捕捉して必ず巡回を前進させる。
                        MessageService.Warn($"Failed to check {novel.Title}: {ex.Message}");
                        novel.HasCheckError = true;
                    }
                }

                if (cancelled) { completedFullSweep = false; break; }

                if (!persisted)
                {
                    // 新着なし作品。成功・失敗いずれも LastCheckedAt を更新して 1 回だけ永続化し、
                    // ラウンドロビンを前進させる(失敗作品も後ろへ回し、特定作品で詰まらせない)。
                    // (新着あり作品は挿入直後に LastCheckedAt 込みで永続化済み = NEW 喪失防止)
                    novel.LastCheckedAt = DateTime.UtcNow.ToString("o");
                    if (novel.HasCheckError != hadError)
                    {
                        // エラーフラグに変化があったときだけ全カラム更新。
                        await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);
                    }
                    else
                    {
                        // 変化が無い大多数の作品は last_checked_at 列のみ更新し、毎チェックの書き込み量を抑える。
                        await _novelRepo.UpdateLastCheckedAtAsync(novel.Id, novel.LastCheckedAt).ConfigureAwait(false);
                    }
                }

                if (!novel.HasCheckError) anySuccess = true;
            }

            // いずれの経路(起動時/手動更新/WorkManager/前面サービス)でも、CheckAll を「全作品まで
            // 完遂したら」完了時刻(epoch ms)を記録する。アラームの冗長発火ゲート
            // (UpdateAlarmScheduler.ShouldSkipRedundantCheck)が直近完了を参照し、健全運用時は
            // アラームをバックストップに留める。記録を各呼び出し側に散らさず、全経路の唯一の合流点で
            // ある CheckAllAsync に置くことで、新しい呼び出し経路でも記録漏れが起きない。
            // (セマフォ獲得失敗時は早期 return しており、ここには到達しない=実チェック時のみ記録)。
            // 打ち切り(3分上限/キャンセル)で未巡回分が残る場合は記録しない。完了済みと誤記録すると
            // 次回アラームが冗長判定でスキップされ、未巡回作品の通知が最大 interval/2 遅延するため。
            // さらに、全件失敗(ネット断等)や空テーブルで「完了」を記録すると、何も検証していないのに
            // バックストップを抑止してしまう。実際に1件以上チェックできた(anySuccess)場合のみ記録する。
            if (completedFullSweep && anySuccess)
            {
                Preferences.Set(SettingsKeys.LAST_CHECK_COMPLETED_MS, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            }

            // 新着を検出したら、前面に居る一覧画面へ即時再読込を促す。背面検出はシステム通知で気づけるが、
            // 前面時は通知が抑止される(CancelAll 競合回避)ため、ここで通知しないと手動更新まで NEW が
            // 見えない窓が残る。受信側は弱参照のため購読解除不要。手動更新中は受信側が IsLoading で抑止する。
            if (updates.Count > 0)
            {
                WeakReferenceMessenger.Default.Send(new UpdatesDetectedMessage(updates.Count));
            }

            return updates;
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
