using LanobeReader.Helpers;
using LanobeReader.Models;
using SQLite;
using TBird.Core;
using TBird.Maui.DB;

namespace LanobeReader.Services.Database;

public class DatabaseService : SqliteDatabaseBase
{
    private const int CURRENT_SCHEMA_VERSION = 5;

    public DatabaseService()
        : base(Path.Combine(FileSystem.AppDataDirectory, "lanobereader.db"), CURRENT_SCHEMA_VERSION)
    {
    }

    protected override async Task CreateTablesAsync(SQLiteAsyncConnection conn)
    {
        // 0. 並行アクセス対策。前面UI・背景更新チェック(FGS/Worker)・プリフェッチが同一接続へ
        //    書き込むため、競合時の即時 "database is locked" を抑止する。busy_timeout を先に設定し、
        //    続く WAL 切替 PRAGMA 自体も競合時に最大 5 秒待機できるようにする(起動時の一時ロックで
        //    最初の DDL が即失敗→初期化 Task が faulted で固着するのを防ぐ)。WAL は DB ファイルに
        //    永続化され読取と書込の並行性を上げる。冪等(毎起動の再適用は無害)。
        // これらの PRAGMA は「設定後の値」を結果行として返すため、行を読まない ExecuteAsync
        // (=ExecuteNonQuery) で実行すると Step() が Row(100) を返し、sqlite-net が無条件 throw する
        // ("not an error" = SQLITE_OK の errmsg)。結果行を読む ExecuteScalarAsync<T> を使う
        // (journal_mode=WAL の有効化は sqlite-net 公式もこの呼び方)。
        await conn.ExecuteScalarAsync<int>("PRAGMA busy_timeout=5000").ConfigureAwait(false);
        await conn.ExecuteScalarAsync<string>("PRAGMA journal_mode=WAL").ConfigureAwait(false);

        // 1. CreateTable は冪等。v0 の新規インストール時の初期化も兼ねる。
        await conn.CreateTableAsync<Novel>().ConfigureAwait(false);
        await conn.CreateTableAsync<Episode>().ConfigureAwait(false);
        await conn.CreateTableAsync<EpisodeCache>().ConfigureAwait(false);
        await conn.CreateTableAsync<AppSetting>().ConfigureAwait(false);

        // 2. 既存カラム追加（新規カラムの後方互換）
        await EnsureColumnAsync(conn, "novels", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "novels", "favorited_at", "TEXT NULL").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "novels", "last_checked_at", "TEXT NULL").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "episodes", "is_favorite", "INTEGER NOT NULL DEFAULT 0").ConfigureAwait(false);
        await EnsureColumnAsync(conn, "episodes", "favorited_at", "TEXT NULL").ConfigureAwait(false);
        // サイト側エピソード ID（Kakuyomu の本文取得を位置依存解決から安定 ID へ移行するため）。
        // 列追加は EnsureColumnAsync で冪等。新規インストールは CreateTableAsync<Episode> が含めて作成する。
        await EnsureColumnAsync(conn, "episodes", "site_episode_id", "TEXT NULL").ConfigureAwait(false);

        // 3. novels の UNIQUE 制約（v1 時点で既に整備済みなので再適用するだけ）
        await conn.ExecuteAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS idx_novels_site_novel ON novels (site_type, novel_id)"
        ).ConfigureAwait(false);
    }

    protected override async Task SeedAsync(SQLiteAsyncConnection conn)
    {
        var defaults = new Dictionary<string, string>
        {
            [SettingsKeys.CACHE_MONTHS]          = SettingsKeys.DEFAULT_CACHE_MONTHS.ToString(),
            [SettingsKeys.UPDATE_INTERVAL_HOURS] = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString(),
            [SettingsKeys.FONT_SIZE_SP]          = SettingsKeys.DEFAULT_FONT_SIZE_SP.ToString(),
            [SettingsKeys.BACKGROUND_THEME]      = SettingsKeys.DEFAULT_BACKGROUND_THEME.ToString(),
            [SettingsKeys.LINE_SPACING]          = SettingsKeys.DEFAULT_LINE_SPACING.ToString(),
            [SettingsKeys.EPISODES_PER_PAGE]     = SettingsKeys.DEFAULT_EPISODES_PER_PAGE.ToString(),
            [SettingsKeys.PREFETCH_ENABLED]      = SettingsKeys.DEFAULT_PREFETCH_ENABLED.ToString(),
            [SettingsKeys.REQUEST_DELAY_MS]      = SettingsKeys.DEFAULT_REQUEST_DELAY_MS.ToString(),
            [SettingsKeys.VERTICAL_WRITING]      = SettingsKeys.DEFAULT_VERTICAL_WRITING.ToString(),
            [SettingsKeys.NOVEL_SORT_KEY]        = SettingsKeys.DEFAULT_NOVEL_SORT_KEY,
            [SettingsKeys.LAST_SCHEDULED_HOURS]  = SettingsKeys.DEFAULT_UPDATE_INTERVAL_HOURS.ToString(),
            [SettingsKeys.AUTO_MARK_READ_ENABLED] = SettingsKeys.DEFAULT_AUTO_MARK_READ_ENABLED.ToString(),
        };

        foreach (var (key, value) in defaults)
        {
            var existing = await conn.FindAsync<AppSetting>(key).ConfigureAwait(false);
            if (existing is null)
            {
                await conn.InsertAsync(new AppSetting { Key = key, Value = value }).ConfigureAwait(false);
            }
        }
    }

    protected override IReadOnlyList<IMigration> GetMigrations()
        => new IMigration[] { new MigrateToV2(), new MigrateToV3(), new MigrateToV4(), new MigrateToV5() };

    protected override async Task<int> ReadSchemaVersionAsync(SQLiteAsyncConnection conn)
    {
        try
        {
            var row = await conn.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
            if (row is null) return 1; // 未設定は v1 扱い（既存リリースは v1 で動作してきた）
            return int.TryParse(row.Value, out var v) ? v : 1;
        }
        catch
        {
            return 1;
        }
    }

    protected override async Task WriteSchemaVersionAsync(SQLiteAsyncConnection conn, int version)
    {
        var existing = await conn.FindAsync<AppSetting>("schema_version").ConfigureAwait(false);
        if (existing is null)
        {
            await conn.InsertAsync(
                new AppSetting { Key = "schema_version", Value = version.ToString() }
            ).ConfigureAwait(false);
        }
        else
        {
            existing.Value = version.ToString();
            await conn.UpdateAsync(existing).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// v1 → v2: episodes(novel_id, episode_no) を UNIQUE 化。
    /// 既存の非UNIQUEインデックス idx_episodes_novel_episode を DROP してから
    /// 重複レコードを除去し、UNIQUE インデックスを貼り直す。
    /// 重複の episode_cache も連鎖削除。
    /// </summary>
    private class MigrateToV2 : IMigration
    {
        public int FromVersion => 1;

        public async Task ExecuteAsync(SQLiteAsyncConnection conn)
        {
            try
            {
                await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_episodes_novel_episode")
                    .ConfigureAwait(false);

                var dupCount = await conn.ExecuteScalarAsync<int>(
                    "SELECT COUNT(*) FROM (" +
                    "  SELECT novel_id, episode_no FROM episodes " +
                    "  GROUP BY novel_id, episode_no HAVING COUNT(*) > 1" +
                    ")"
                ).ConfigureAwait(false);

                if (dupCount > 0)
                {
                    MessageService.Warn(
                        $"[MigrateToV2] Found {dupCount} duplicate (novel_id, episode_no) groups. Deduping.");

                    // 孤立 cache 先 → episodes 後（FK なしのため手動順序管理）
                    await conn.ExecuteAsync(
                        "DELETE FROM episode_cache WHERE episode_id IN (" +
                        "  SELECT id FROM episodes WHERE id NOT IN (" +
                        "    SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                        "  )" +
                        ")"
                    ).ConfigureAwait(false);

                    var deleted = await conn.ExecuteAsync(
                        "DELETE FROM episodes WHERE id NOT IN (" +
                        "  SELECT MIN(id) FROM episodes GROUP BY novel_id, episode_no" +
                        ")"
                    ).ConfigureAwait(false);
                    MessageService.Info($"[MigrateToV2] Deleted {deleted} duplicate episode rows.");
                }

                await conn.ExecuteAsync(
                    "CREATE UNIQUE INDEX IF NOT EXISTS idx_episodes_novel_episode " +
                    "ON episodes (novel_id, episode_no)"
                ).ConfigureAwait(false);

                MessageService.Info("[MigrateToV2] Done.");
            }
            catch (Exception ex)
            {
                MessageService.Warn($"[MigrateToV2] Failed: {ex.Message}");
                throw; // 上位 (SqliteDatabaseBase) で WriteSchemaVersionAsync を skip させるため再送出
            }
        }
    }

    /// <summary>
    /// v2 → v3: episodes(novel_id, is_read) に複合インデックスを追加。
    /// NovelRepository.GetAllWithUnreadCountAsync の集計サブクエリ
    /// (episode_count / read_count / unread_count を 1 パスで GROUP BY) の
    /// covering index として機能する。
    /// </summary>
    private class MigrateToV3 : IMigration
    {
        public int FromVersion => 2;

        public async Task ExecuteAsync(SQLiteAsyncConnection conn)
        {
            try
            {
                await conn.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_episodes_novel_isread " +
                    "ON episodes (novel_id, is_read)"
                ).ConfigureAwait(false);
                MessageService.Info("[MigrateToV3] Done.");
            }
            catch (Exception ex)
            {
                MessageService.Warn($"[MigrateToV3] Failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// v3 → v4: 更新チェック系クエリ向けのインデックスを整備。
    /// - episodes(novel_id, is_read, episode_no): GetDeepLinkTargetEpisodeIdsAsync の
    ///   相関サブクエリ MIN/MAX(episode_no) をインデックス端のシークで解決する covering index。
    ///   (novel_id, is_read) の上位互換のため旧 idx_episodes_novel_isread は DROP する。
    /// - novels(last_checked_at): GetAllForCheckAsync の「最終チェック古い順」ソートを索引化する
    ///   (NULL=未チェックが先頭に来るラウンドロビン順)。
    /// </summary>
    private class MigrateToV4 : IMigration
    {
        public int FromVersion => 3;

        public async Task ExecuteAsync(SQLiteAsyncConnection conn)
        {
            try
            {
                // 新インデックスを先に作成し、旧 idx_episodes_novel_isread の DROP は最後に行う。
                // この 3 文はトランザクション外のため、DROP を先頭に置くと途中失敗(プロセス kill /
                // ディスク逼迫)で「旧索引は消えたが新索引は未作成」となり、該当クエリがフルスキャンへ
                // 退化する窓が残る(schema_version 未前進で再実行されるが、その窓が問題)。順序を逆にすれば
                // 部分失敗時も旧索引が残る(新索引と冗長だが無害)だけで covering index を失わない。
                await conn.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_episodes_novel_isread_epno " +
                    "ON episodes (novel_id, is_read, episode_no)"
                ).ConfigureAwait(false);
                await conn.ExecuteAsync(
                    "CREATE INDEX IF NOT EXISTS idx_novels_last_checked " +
                    "ON novels (last_checked_at)"
                ).ConfigureAwait(false);
                await conn.ExecuteAsync("DROP INDEX IF EXISTS idx_episodes_novel_isread")
                    .ConfigureAwait(false);
                MessageService.Info("[MigrateToV4] Done.");
            }
            catch (Exception ex)
            {
                MessageService.Warn($"[MigrateToV4] Failed: {ex.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// v4 → v5: 既存 novels.last_updated_at のうち、本PR以前に保存された Narou 由来の生 JST 値
    /// ("yyyy-MM-dd HH:mm:ss"、オフセット無し)を UTC ISO("o")へ正規化する。
    /// 本PRで Narou 取得値は <see cref="NarouDateTime.ToUtcIso"/> で UTC 統一されたが、既存行は旧 JST 生値の
    /// まま残り、UpdateCheckService.SameInstant が両者を UTC 扱いで比較して 9 時間差と誤判定し、既存 Narou 全
    /// 作品が初回チェックで一斉に無駄なフル再取得を起こす(端末ローカル TZ での誤表示も生む)。移行時に一括
    /// 正規化して塞ぐ。形式判定: 'T' を含む値(= 既に ISO/UTC。Kakuyomu の UtcNow("o") 含む)は対象外。
    /// 変換後の値は 'T' を含むため再対象にならず冪等。
    /// </summary>
    private class MigrateToV5 : IMigration
    {
        public int FromVersion => 4;

        public async Task ExecuteAsync(SQLiteAsyncConnection conn)
        {
            try
            {
                // 旧形式候補(スペース区切り・'T' 無し)だけを SQL で絞り込む(ISO 値・NULL は対象外)。
                var legacy = await conn.QueryAsync<Novel>(
                    "SELECT * FROM novels " +
                    "WHERE last_updated_at IS NOT NULL " +
                    "AND last_updated_at LIKE '% %' AND last_updated_at NOT LIKE '%T%'").ConfigureAwait(false);

                // 変換対象を先に確定し 1 トランザクションで一括 UPDATE する。行ごとの個別コミット
                // (WAL では行ごとに fsync)は起動ホットパスで旧作品が多いと逐次コミットが直列化し起動を
                // 遅らせるため、他の一括処理(Backfill/UpdateLastCheckedAtBatch)と同じく RunInTransactionAsync に揃える。
                var pendingUpdates = new List<(string normalized, int id)>();
                foreach (var novel in legacy)
                {
                    var normalized = NarouDateTime.ToUtcIso(novel.LastUpdatedAt);
                    if (normalized is null || normalized == novel.LastUpdatedAt) continue; // 解析不能/不変はスキップ
                    pendingUpdates.Add((normalized, novel.Id));
                }
                if (pendingUpdates.Count > 0)
                {
                    await conn.RunInTransactionAsync(c =>
                    {
                        foreach (var (normalized, id) in pendingUpdates)
                        {
                            c.Execute("UPDATE novels SET last_updated_at = ? WHERE id = ?", normalized, id);
                        }
                    }).ConfigureAwait(false);
                }
                MessageService.Info($"[MigrateToV5] Normalized {pendingUpdates.Count} legacy last_updated_at value(s).");
            }
            catch (Exception ex)
            {
                MessageService.Warn($"[MigrateToV5] Failed: {ex.Message}");
                throw;
            }
        }
    }
}
