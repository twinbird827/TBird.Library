using System.Globalization;
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

    public async Task<List<(Novel novel, int newEpisodeCount)>> CheckAllAsync(
        CancellationToken ct = default, Action? onSkippedDueToContention = null)
    {
        if (!await _semaphore.WaitAsync(0, ct).ConfigureAwait(false))
        {
            // 別経路が実チェック中。背景経路の呼び出し側はこの通知を受け取り、近接リトライ
            // (WorkManager フォールバック)へ委ねて取りこぼしを防げる。手動更新側は無視してよい
            // (進行中のチェックが DB を更新し UpdatesDetectedMessage で一覧へ反映されるため)。
            MessageService.Warn("Update check already running, skipping");
            onSkippedDueToContention?.Invoke();
            return [];
        }

        try
        {
            // 「最後にチェックした時刻が古い順(未チェック=null 優先)」で回す。3分上限(shortService)
            // 等で打ち切られても、次回が続きから拾える (ラウンドロビン) ようにするため。
            var novels = await _novelRepo.GetAllForCheckAsync().ConfigureAwait(false);
            var updates = new List<(Novel, int)>();
            // 「新着なし」作品の last_checked_at 前進を蓄積し、ループ末尾で 1 トランザクションに束ねる。
            // 作品ごとの個別コミット(巡回1周＝作品数ぶんの書き込み)を避けるため。
            var pendingLastChecked = new List<(int Id, string Ts)>();
            // 全作品を回りきったかどうか。打ち切り(キャンセル/3分上限)で抜けた場合は false とし、
            // 完了時刻の記録(=アラームのバックストップ抑止)を行わない。
            var completedFullSweep = true;
            // 1件以上を実際にチェックできたか。全件失敗(ネット断等)や空テーブルでの「完了」記録による
            // アラーム冗長ゲートの無用な抑止を避けるため、完了時刻記録の条件に用いる。
            var anySuccess = false;
            // (U2) 1 巡で「移行補完」(フル TOC 取得+backfill)を試みる旧 Kakuyomu 作品の上限。完結/安定済みで
            // 新着検出枝に入らない作品も site_episode_id へ移行できるようにしつつ、1 巡の追加ネットワーク
            // コストを抑える。round-robin(last_checked_at 昇順)で巡回するため複数巡で全旧作品を順に拾える。
            var migrationBudget = 3;

            foreach (var novel in novels)
            {
                if (ct.IsCancellationRequested) { completedFullSweep = false; break; }

                // 取得自体は常に行う。HasUnconfirmedUpdate=true の小説をスキップすると、
                // ユーザがアプリを開かない限り更新追跡から脱落する問題があったため (H-2)。
                // 通知は notificationId=novel.Id で上書き表示されるため重複通知にはならない。

                var hadError = novel.HasCheckError;
                var persisted = false;
                // 新着なしでも novel 行の永続化(全カラム UPDATE)が必要になったか。
                // 新着なし・エラー不変だが TotalEpisodes を是正した場合(サイトのカウントズレ吸収)に立てる。
                var metadataChanged = false;
                try
                {
                    var service = _serviceFactory.GetService((SiteType)novel.SiteType);
                    var (totalEpisodes, lastUpdatedAt, isCompleted, author) = await service.FetchNovelInfoAsync(novel.NovelId, ct).ConfigureAwait(false);

                    // 取得に成功した時点でエラーフラグを解除する。以降の永続化(新着あり経路の即時
                    // UpdateAsync / 新着なし経路の末尾更新)がいずれもこの値を書き込むため、
                    // ここで一度だけ設定すれば全成功経路をカバーできる。
                    novel.HasCheckError = false;

                    var currentMaxEpisode = await _episodeRepo.GetMaxEpisodeNoAsync(novel.Id).ConfigureAwait(false);

                    // サイト報告の総話数が DB 上限を超え、かつ「前回処理した報告値から変化した」または
                    // 「サイトの最終更新時刻が前回から進んだ」場合に実取得する。
                    // 「!= novel.TotalEpisodes」だけで判定する狙い: Narou の general_all_no 等、サイト報告値が
                    // 実際に解析できる話数より多くなることがある(カウントのズレ)。報告値比較だけだと、解析可能な
                    // 新話が無い(newEpisodes 空)まま毎周期フル一覧を再取得し続けるのを抑止できる。
                    // ただし報告値が頭打ち(水増しカウント据え置き)のまま実話だけ後から埋まると、報告値比較
                    // だけでは新着を恒久的に取りこぼす。サイトは新話公開時に最終更新時刻を進めるため、
                    // lastUpdatedAt の変化も再取得条件に加えて取りこぼしを塞ぐ。両者とも不変なら再取得しない。
                    var reportedTotalChanged = totalEpisodes != novel.TotalEpisodes;
                    // 文字列の単純比較ではなく実時刻として比較する。保存値はサイト形式("yyyy-MM-dd HH:mm:ss"
                    // 等)のことも、新着確定時のフォールバック ISO("o")のこともあり、同一時刻でも形式差で
                    // 不一致と誤判定して無駄なフル再取得を招くため(両方パースできる場合は UTC 実時刻で比較)。
                    var siteUpdatedSinceLast = lastUpdatedAt is not null
                        && !SameInstant(lastUpdatedAt, novel.LastUpdatedAt);
                    if (totalEpisodes > currentMaxEpisode && (reportedTotalChanged || siteUpdatedSinceLast))
                    {
                        // Fetch new episodes
                        var allEpisodes = await service.FetchEpisodeListAsync(novel.NovelId, ct).ConfigureAwait(false);

                        // 旧データ(site_episode_id 未保存の既存話)に、いま取得した新鮮な TOC のサイト話 ID を
                        // 補完する(Kakuyomu の本文取得を位置依存からの脱却=誤話表示の是正)。タイトル一致時のみ
                        // 更新するためドリフト誤補完は避けられる。Narou は SiteEpisodeId を持たず no-op。
                        // best-effort: 失敗しても更新検出処理は続行する。
                        try
                        {
                            await _episodeRepo.BackfillSiteEpisodeIdsAsync(novel.Id, allEpisodes).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            MessageService.Warn($"Backfill site_episode_id failed for {novel.Title}: {ex.Message}");
                        }

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
                            // (HasCheckError は取得成功直後に解除済み)

                            // 挿入した episodes と novel メタデータ(HasUnconfirmedUpdate/TotalEpisodes 等)は
                            // 一括で確定させる。永続化を後続(プリフェッチ enqueue / ループ末尾)へ遅延すると、
                            // その窓でプロセスが kill / 3分上限で打ち切られた場合に episodes だけ commit され
                            // novel 行が古いまま残り、次回 GetMaxEpisodeNoAsync が新 max を返すため新着が
                            // 二度と再検出されない(NEW 喪失)。挿入直後に永続化して窓を最小化する。
                            novel.LastCheckedAt = DateTime.UtcNow.ToString("o");
                            await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);
                            persisted = true;

                            updates.Add((novel, newEpisodes.Count));

                            // 挿入済み episodes をバックグラウンド先読みへ積む(Wi-Fi ゲート)。
                            // InsertAllAsync が各 Episode.Id を設定済みのため再取得(GetByNovelIdAsync)は不要。
                            // プリフェッチ登録は best-effort: ここでの失敗を outer catch へ波及させると
                            // novel.HasCheckError=true となり anySuccess を落として完了記録(LAST_CHECK_
                            // COMPLETED_MS)を阻害し、更新確定済みでもアラームが毎周期 FGS を起動し続ける。
                            // 更新は既に永続化(=成功)済みなので、enqueue 失敗は個別に握りつぶす。
                            if (_jobQueue is not null)
                            {
                                try
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
                                            SiteEpisodeId = ep.SiteEpisodeId,
                                        }, novel.IsFavorite ? JobPriority.High : JobPriority.Normal).ConfigureAwait(false);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    MessageService.Warn($"Prefetch enqueue failed for {novel.Title}: {ex.Message}");
                                }
                            }
                        }
                        else
                        {
                            // 報告値は増えたが解析可能な新話は無い(サイトのカウントズレ)。処理済みの報告値と
                            // 最終更新時刻を記録し、同じ報告値・同じ更新時刻での無駄なフル再取得を次回以降抑止する。
                            // (永続化は末尾の !persisted ブロックで metadataChanged を見て全カラム更新)
                            // 報告値・最終更新時刻のいずれかが進めば上の条件で再取得し新着を拾い直す(自己修復)。
                            novel.TotalEpisodes = totalEpisodes;
                            if (lastUpdatedAt is not null) novel.LastUpdatedAt = lastUpdatedAt;
                            metadataChanged = true;
                        }
                    }
                    else if (migrationBudget > 0
                        && (SiteType)novel.SiteType == SiteType.Kakuyomu
                        && currentMaxEpisode > 0)
                    {
                        // (U2) 完結/安定済みの旧 Kakuyomu 作品は新着検出枝に入らず site_episode_id が永久に
                        // 未補完のまま残る(本文取得が位置依存=誤話リスク)。サイト話 ID を 1 件も持たない
                        // Kakuyomu 作品(=列追加前の旧データ)に限り、低頻度(1巡で migrationBudget 件まで)で
                        // フル TOC を取得して移行補完する。補完後は非 NULL 行が生じ再該当しないため概ね初回限り。
                        // best-effort: 失敗しても巡回(LastCheckedAt 前進)は継続する。EXISTS 判定やフル TOC
                        // 取得の失敗を outer catch へ波及させると、更新チェック自体は成功しているのに
                        // HasCheckError=true となり完了記録を阻害するため、判定ごと try で握りつぶす。
                        try
                        {
                            if (!await _episodeRepo.HasAnySiteEpisodeIdAsync(novel.Id).ConfigureAwait(false))
                            {
                                migrationBudget--;
                                var allEpisodes = await service.FetchEpisodeListAsync(novel.NovelId, ct).ConfigureAwait(false);
                                await _episodeRepo.BackfillSiteEpisodeIdsAsync(novel.Id, allEpisodes).ConfigureAwait(false);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageService.Warn($"Migration backfill failed for {novel.Title}: {ex.Message}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (ct.IsCancellationRequested)
                    {
                        // 打ち切り(3分上限等)。この作品は LastCheckedAt を更新せず、
                        // 次回この作品から再開できるようにループを抜ける(完了記録もしない)。
                        completedFullSweep = false;
                        break;
                    }

                    // ネットワーク/パース/DB 等、その作品固有のあらゆる失敗を握りつぶして次へ進む。
                    // ここを HttpRequestException 系に限定すると、想定外の例外で foreach 全体が脱出し
                    // LastCheckedAt が前進しないため、巡回の先頭(最古)に居座る poison 作品が
                    // 全作品のチェックを永久に止めてしまう。広く捕捉して必ず巡回を前進させる。
                    MessageService.Warn($"Failed to check {novel.Title}: {ex.Message}");
                    novel.HasCheckError = true;
                }

                if (!persisted)
                {
                    // 新着なし作品。成功・失敗いずれも LastCheckedAt を更新して 1 回だけ永続化し、
                    // ラウンドロビンを前進させる(失敗作品も後ろへ回し、特定作品で詰まらせない)。
                    // (新着あり作品は挿入直後に LastCheckedAt 込みで永続化済み = NEW 喪失防止)
                    novel.LastCheckedAt = DateTime.UtcNow.ToString("o");
                    if (novel.HasCheckError != hadError || metadataChanged)
                    {
                        // エラーフラグ変化、または TotalEpisodes 是正(カウントズレ吸収)があったときは全カラム更新。
                        await _novelRepo.UpdateAsync(novel).ConfigureAwait(false);
                    }
                    else
                    {
                        // 変化が無い大多数の作品は last_checked_at 列のみ更新。作品ごとの即時 await は
                        // 巡回1周で作品数ぶんのコミットになるため、蓄積してループ末尾で 1 トランザクションに束ねる。
                        pendingLastChecked.Add((novel.Id, novel.LastCheckedAt));
                    }
                }

                if (!novel.HasCheckError) anySuccess = true;
            }

            // 蓄積した last_checked_at をまとめて 1 トランザクションで永続化する。break(キャンセル/打ち切り)
            // でも foreach を抜けた後のここを必ず通過するため、巡回済み作品のタイムスタンプは前進する。
            // 万一プロセス kill で未 flush でも失われるのは「新着なし作品」の巡回時刻のみ(次回その作品を
            // 再チェックするだけ＝冪等・NEW 喪失なし)。新着あり作品は挿入直後に即時永続化済み。
            if (pendingLastChecked.Count > 0)
            {
                try
                {
                    await _novelRepo.UpdateLastCheckedAtBatchAsync(pendingLastChecked).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // last_checked_at は巡回順序のヒントに過ぎず、失敗しても次回再チェックされるだけ。
                    // 確定済みの updates 通知を巻き込まないよう例外は伝播させず握りつぶす。
                    MessageService.Warn($"Batch last_checked_at update failed: {ex.Message}");
                }
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
                WeakReferenceMessenger.Default.Send(
                    new UpdatesDetectedMessage(updates.Count, updates.Select(u => u.Item1.Id).ToList()));
            }

            return updates;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// 2 つの最終更新時刻文字列が「同一の実時刻」を表すか。両方が DateTime としてパースできれば UTC に
    /// 揃えて比較し、サイト形式("yyyy-MM-dd HH:mm:ss")と ISO("o")のような形式差を吸収する。どちらかが
    /// パースできない場合は素の文字列一致で判定する。オフセット無しの値は UTC とみなす(端末ローカルTZ
    /// による揺れを避ける)。
    /// </summary>
    private static bool SameInstant(string? a, string? b)
    {
        if (a == b) return true;
        if (a is null || b is null) return false;

        const DateTimeStyles styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTime.TryParse(a, CultureInfo.InvariantCulture, styles, out var da)
            && DateTime.TryParse(b, CultureInfo.InvariantCulture, styles, out var db))
        {
            return da == db;
        }
        return false;
    }
}
