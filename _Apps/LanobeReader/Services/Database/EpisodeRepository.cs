using LanobeReader.Models;

namespace LanobeReader.Services.Database;

public class EpisodeRepository(DatabaseService dbService)
{
    private Task EnsureAsync() => dbService.EnsureInitializedAsync();

    // 複数の raw SQL クエリで使う episodes の列リスト。列追加時の更新漏れ(列順・列セットのズレ)を
    // 防ぐため一元化する。Episode のプロパティ([Column] 属性)へ列名でマップされる。
    private const string EpisodeColumns =
        "id, novel_id, episode_no, chapter_name, title, " +
        "is_read, read_at, published_at, is_favorite, favorited_at, site_episode_id";

    public async Task<List<Episode>> GetByNovelIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        // ORM (Table<T>().Where().OrderBy().ToListAsync()) は LINQ 式木 → SQL コンパイルの
        // オーバーヘッドが乗るため、長尺小説 (1500+ 話) では raw SQL が体感で速い。
        return await dbService.Connection.QueryAsync<Episode>(
            $"SELECT {EpisodeColumns} " +
            "FROM episodes WHERE novel_id = ? ORDER BY episode_no",
            novelId).ConfigureAwait(false);
    }

    public async Task<Episode?> GetPreviousEpisodeAsync(int novelId, int currentEpisodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        var results = await dbService.Connection.QueryAsync<Episode>(
            $"SELECT {EpisodeColumns} " +
            "FROM episodes WHERE novel_id = ? AND episode_no < ? " +
            "ORDER BY episode_no DESC LIMIT 1",
            novelId, currentEpisodeNo).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<Episode?> GetNextEpisodeAsync(int novelId, int currentEpisodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        var results = await dbService.Connection.QueryAsync<Episode>(
            $"SELECT {EpisodeColumns} " +
            "FROM episodes WHERE novel_id = ? AND episode_no > ? " +
            "ORDER BY episode_no ASC LIMIT 1",
            novelId, currentEpisodeNo).ConfigureAwait(false);
        return results.FirstOrDefault();
    }

    public async Task<Episode?> GetByIdAsync(int id)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await dbService.Connection.Table<Episode>().FirstOrDefaultAsync(e => e.Id == id).ConfigureAwait(false);
    }

    public async Task<int> GetMaxEpisodeNoAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await dbService.Connection.ExecuteScalarAsync<int>(
            "SELECT COALESCE(MAX(episode_no), 0) FROM episodes WHERE novel_id = ?", novelId).ConfigureAwait(false);
    }

    /// <summary>
    /// 当該小説に site_episode_id を持つ話が 1 件でも存在するか(安価な EXISTS 判定)。
    /// false かつ episodes が存在する Kakuyomu 作品 = 列追加前の旧データで未移行、の判定に使う。
    /// </summary>
    public async Task<bool> HasAnySiteEpisodeIdAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        var count = await dbService.Connection.ExecuteScalarAsync<int>(
            "SELECT EXISTS(SELECT 1 FROM episodes WHERE novel_id = ? AND site_episode_id IS NOT NULL)",
            novelId).ConfigureAwait(false);
        return count != 0;
    }

    /// <summary>
    /// 指定小説の既読話の Id 集合。目次がリーダー復帰時に既読状態だけを取り直すのに使う(全列の全話再取得を避ける)。
    /// </summary>
    public async Task<HashSet<int>> GetReadEpisodeIdsAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        var ids = await dbService.Connection.QueryScalarsAsync<int>(
            "SELECT id FROM episodes WHERE novel_id = ? AND is_read = 1", novelId).ConfigureAwait(false);
        return ids.ToHashSet();
    }

    /// <summary>
    /// 複数小説のディープリンク先エピソード Id をまとめて解決する。各小説につき
    /// 「最初の未読話(最小 episode_no)」、未読が無ければ「最後に読んだ話(最大 episode_no)」。
    /// 通知ループでの作品ごと逐次クエリ(最大 2×N 往復)を、チャンクごとに 1 クエリへ集約する。戻り値は novelId -> episodeId。
    /// 該当話が無い小説はキーを持たない(呼び出し側で 0 フォールバック)。
    /// </summary>
    public async Task<Dictionary<int, int>> GetDeepLinkTargetEpisodeIdsAsync(IReadOnlyList<int> novelIds)
    {
        var result = new Dictionary<int, int>();
        if (novelIds.Count == 0) return result;
        await EnsureAsync().ConfigureAwait(false);

        // SQLite の変数上限(既定 999)に達しないよう IN 句の引数をチャンク分割して照会する。
        const int ChunkSize = 900;
        for (int offset = 0; offset < novelIds.Count; offset += ChunkSize)
        {
            var chunk = novelIds.Skip(offset).Take(ChunkSize).ToList();
            var placeholders = string.Join(",", chunk.Select(_ => "?"));
            var args = chunk.Cast<object>().ToArray();
            // 作品ごとに未読の最小 episode_no、無ければ既読の最大 episode_no の 1 件。各相関サブクエリは
            // idx_episodes_novel_isread_epno の (novel_id, is_read) 範囲の端をシークして 1 行で止まる。
            // 話が無い作品は Id が NULL になる(WHERE で除くと相関サブクエリが二重評価されるため C# 側で飛ばす)。
            var rows = await dbService.Connection.QueryAsync<EpisodeRef>(
                "SELECT n.id AS NovelId, COALESCE(" +
                "(SELECT id FROM episodes WHERE novel_id = n.id AND is_read = 0 ORDER BY episode_no LIMIT 1), " +
                "(SELECT id FROM episodes WHERE novel_id = n.id AND is_read = 1 ORDER BY episode_no DESC LIMIT 1)) AS Id " +
                $"FROM novels n WHERE n.id IN ({placeholders})",
                args).ConfigureAwait(false);
            foreach (var r in rows)
            {
                if (r.Id is int id) result[r.NovelId] = id;
            }
        }
        return result;
    }

    private sealed class EpisodeRef
    {
        public int NovelId { get; set; }
        public int? Id { get; set; }
    }

    /// <summary>
    /// 先読み対象 1 話ぶん。作品情報(サイト種別・サイト作品 ID・お気に入り)を JOIN で同じ行に持つ。
    /// </summary>
    public sealed class PrefetchTarget
    {
        public int NovelDbId { get; set; }
        public int EpisodeDbId { get; set; }
        public int EpisodeNo { get; set; }
        public string? SiteEpisodeId { get; set; }
        public int SiteType { get; set; }
        public string SiteNovelId { get; set; } = string.Empty;
        public bool IsFavorite { get; set; }
    }

    // episodes.novel_id と novels.novel_id が同名のため、列は AS <プロパティ名> で受ける。
    private const string PrefetchTargetSelect =
        "SELECT n.id AS NovelDbId, e.id AS EpisodeDbId, e.episode_no AS EpisodeNo, " +
        "e.site_episode_id AS SiteEpisodeId, n.site_type AS SiteType, n.novel_id AS SiteNovelId, " +
        "COALESCE(n.is_favorite, 0) AS IsFavorite " +
        "FROM episodes e JOIN novels n ON n.id = e.novel_id " +
        "LEFT JOIN episode_cache c ON c.episode_id = e.id ";

    /// <summary>
    /// 全作品の未読かつ未キャッシュの話。お気に入り作品 → last_updated_at 降順 → 作品ごとに話番号昇順で返す。
    /// </summary>
    public async Task<List<PrefetchTarget>> GetUnreadUncachedTargetsAsync()
    {
        await EnsureAsync().ConfigureAwait(false);
        return await dbService.Connection.QueryAsync<PrefetchTarget>(
            PrefetchTargetSelect +
            "WHERE e.is_read = 0 AND c.episode_id IS NULL " +
            "ORDER BY COALESCE(n.is_favorite, 0) DESC, n.last_updated_at DESC, n.id, e.episode_no").ConfigureAwait(false);
    }

    /// <summary>
    /// 指定作品の未キャッシュの話(既読も含む)を話番号昇順で返す。
    /// </summary>
    public async Task<List<PrefetchTarget>> GetUncachedTargetsAsync(int novelId)
    {
        await EnsureAsync().ConfigureAwait(false);
        return await dbService.Connection.QueryAsync<PrefetchTarget>(
            PrefetchTargetSelect +
            "WHERE e.novel_id = ? AND c.episode_id IS NULL " +
            "ORDER BY e.episode_no",
            novelId).ConfigureAwait(false);
    }

    public async Task InsertAllAsync(IEnumerable<Episode> episodes)
    {
        await EnsureAsync().ConfigureAwait(false);
        await dbService.Connection.InsertAllAsync(episodes).ConfigureAwait(false);
    }

    /// <summary>
    /// site_episode_id が未設定の既存話(列追加前に保存された旧データ)へ、新鮮な TOC から導出した
    /// サイト話 ID を補完する。ドリフト(序盤話の削除/並べ替え)後の誤補完を避けるため、同一 episode_no の
    /// <b>タイトルが一致する話だけ</b>更新する(不一致=ドリフト疑い→触らない)。既に設定済みの話は更新しない。
    /// SiteEpisodeId を持つ話が新鮮リストに無い場合(Narou 等)は何もしない。best-effort。
    /// </summary>
    public async Task BackfillSiteEpisodeIdsAsync(int novelId, IReadOnlyList<Episode> freshEpisodes)
    {
        if (freshEpisodes.Count == 0 || freshEpisodes.All(e => string.IsNullOrEmpty(e.SiteEpisodeId))) return;
        await EnsureAsync().ConfigureAwait(false);

        // 未補完(site_episode_id IS NULL)の話だけを SQL で絞り込む。全話フルロード+C# フィルタだと
        // 初回補完後は更新ゼロでも更新チェックの度に O(話数) のコストを払う(長尺×お気に入りで無駄が累積)。
        // 後段 UPDATE のガードも IS NULL のため、書き込み対象は本クエリと完全一致(空文字行は元々非対象)。
        var pending = await dbService.Connection.QueryAsync<Episode>(
            $"SELECT {EpisodeColumns} FROM episodes WHERE novel_id = ? AND site_episode_id IS NULL",
            novelId).ConfigureAwait(false);
        if (pending.Count == 0) return;
        // episodes(novel_id, episode_no) に一意制約は無く重複 episode_no 行がありうる
        // (上記 GetTransitionTargets と同様)。ToDictionary は重複キーで例外を投げ、当該作品の
        // 移行が毎周回失敗して恒久的に未補完のままになるため、最小 id を採用して決定的に 1 行へ畳む。
        var byNo = new Dictionary<int, Episode>();
        foreach (var e in pending)
        {
            if (!byNo.TryGetValue(e.EpisodeNo, out var existing) || e.Id < existing.Id)
            {
                byNo[e.EpisodeNo] = e;
            }
        }

        var updates = new List<(string siteId, int id)>();
        foreach (var fresh in freshEpisodes)
        {
            if (string.IsNullOrEmpty(fresh.SiteEpisodeId)) continue;
            if (!byNo.TryGetValue(fresh.EpisodeNo, out var db)) continue;
            if (db.Title != fresh.Title) continue; // タイトル不一致=ドリフト疑い→誤補完しない
            updates.Add((fresh.SiteEpisodeId!, db.Id));
        }
        if (updates.Count == 0) return;

        await dbService.Connection.RunInTransactionAsync(conn =>
        {
            foreach (var (siteId, id) in updates)
            {
                var n = conn.Execute(
                    "UPDATE episodes SET site_episode_id = ? WHERE id = ? AND site_episode_id IS NULL",
                    siteId, id);
                if (n > 0)
                {
                    // 補完前の窓で位置依存フォールバック(episodeIds[episodeNo-1])により取得・キャッシュ
                    // された本文は、TOC ドリフト時に誤話の可能性がある。安定 ID を確定したこの時点で当該
                    // キャッシュを破棄し、次回読み込みで安定 ID により取得し直させる(誤話キャッシュの是正)。
                    conn.Execute("DELETE FROM episode_cache WHERE episode_id = ?", id);
                }
            }
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// 読了点 (episode_no) を境に既読状態を一括更新する。
    /// 1..N: is_read=1（既存 read_at は COALESCE で保持、未設定なら now を入れる）
    /// N+1..max: is_read=0、read_at=NULL に巻き戻し
    /// 過去話を再読した場合は意図的に N+1 以降を未読化する仕様（ユーザ承認済み）。
    /// 全話既読になったら作品の未確認更新フラグ (has_unconfirmed_update) も同じトランザクションで解除する。
    /// </summary>
    public async Task SetReadStateUpToAsync(int novelId, int episodeNo)
    {
        await EnsureAsync().ConfigureAwait(false);
        var now = DateTime.UtcNow.ToString("o");

        await dbService.Connection.RunInTransactionAsync(conn =>
        {
            conn.Execute(
                "UPDATE episodes SET is_read = 1, read_at = COALESCE(read_at, ?) " +
                "WHERE novel_id = ? AND episode_no <= ?",
                now, novelId, episodeNo);

            conn.Execute(
                "UPDATE episodes SET is_read = 0, read_at = NULL " +
                "WHERE novel_id = ? AND episode_no > ?",
                novelId, episodeNo);

            conn.Execute(
                "UPDATE novels SET has_unconfirmed_update = 0 " +
                "WHERE id = ? AND has_unconfirmed_update = 1 " +
                "AND NOT EXISTS (SELECT 1 FROM episodes WHERE novel_id = ? AND is_read = 0)",
                novelId, novelId);
        }).ConfigureAwait(false);
    }

    public async Task SetFavoriteAsync(int episodeId, bool favorite)
    {
        await EnsureAsync().ConfigureAwait(false);
        var now = favorite ? DateTime.UtcNow.ToString("o") : null;
        await dbService.Connection.ExecuteAsync(
            "UPDATE episodes SET is_favorite = ?, favorited_at = ? WHERE id = ?",
            favorite, now, episodeId).ConfigureAwait(false);
    }
}
