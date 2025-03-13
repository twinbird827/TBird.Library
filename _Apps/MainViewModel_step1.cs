using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB.SQLite;
using TBird.Wpf;
using TBird.Wpf.Collections;

namespace Netkeiba
{
    public partial class MainViewModel
    {
        public BindableCollection<ComboboxItemModel> LogSource { get; } = new BindableCollection<ComboboxItemModel>();

        public BindableContextCollection<ComboboxItemModel> Logs { get; }

        public BindableCollection<CheckboxItemModel> BasyoSources { get; } = new BindableCollection<CheckboxItemModel>();

        public BindableContextCollection<CheckboxItemModel> Basyos { get; }

        public int SYear
        {
            get => _SYear;
            set => SetProperty(ref _SYear, value);
        }
        private int _SYear;

        public int EYear
        {
            get => _EYear;
            set => SetProperty(ref _EYear, value);
        }
        private int _EYear;

        public CheckboxItemModel S1Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

        private async Task<int> GetLastMonth()
        {
            using (var conn = AppUtil.CreateSQLiteControl())
            {
                return await conn.ExecuteScalarAsync("SELECT CAST(IFNULL(STRFTIME('%m', REPLACE(MAX(開催日), '/', '-')), 1) AS INTEGER) FROM t_orig").RunAsync(x => x.GetInt32());
            }
        }

        public IRelayCommand S1EXEC => RelayCommand.Create(async _ =>
        {
            var racebases = await GetRecentRaceIds(SYear, EYear, await GetLastMonth()).RunAsync(races =>
            {
                return races
                    .Select(x => x.Left(10))
                    .Distinct()
                    .ToArray();
            });

            Progress.Value = 0;
            Progress.Minimum = 0;
            Progress.Maximum = racebases.Length;

            bool create = S1Overwrite.IsChecked || !File.Exists(AppUtil.Sqlitepath);

            if (create)
            {
                DirectoryUtil.Create(Path.GetDirectoryName(AppUtil.Sqlitepath));
            }

            using (var conn = AppUtil.CreateSQLiteControl())
            {
                if (create)
                {
                    await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_orig");
                    await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_shutuba");
                    await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
                }

                if (await conn.ExistsColumn("t_orig", "ﾚｰｽID"))
                {
                    await conn.BeginTransaction();
                    await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 IS NULL");
                    await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 = ''");
                    await conn.ExecuteNonQueryAsync("DELETE FROM t_orig WHERE 着順 = 0");
                    conn.Commit();
                }

                await Task.Delay(1000);

                foreach (var racebase in racebases)
                {
                    if (create == false) await conn.BeginTransaction();
                    await foreach (var racearr in GetSTEP1Racearrs(conn, racebase))
                    {
                        if (racearr.Any(x => x["回り"] != "障" && string.IsNullOrEmpty(x["ﾀｲﾑ指数"]))) continue;

                        if (create)
                        {
                            create = false;

                            var integers = new[] { "開催日数", "着順" };
                            var keynames = racearr.First().Keys.Select(x => integers.Contains(x) ? $"{x} INTEGER" : $"{x} TEXT");
                            // ﾃｰﾌﾞﾙ作成
                            await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_orig (" + keynames.GetString(",") + ", PRIMARY KEY (ﾚｰｽID, 馬番))");
                            await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_shutuba (" + keynames.GetString(",") + ", PRIMARY KEY (ﾚｰｽID, 馬番))");

                            // ｲﾝﾃﾞｯｸｽ作成
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index00 ON t_orig (着順)");
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index01 ON t_orig (開催日数,ﾚｰｽID)");
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index02 ON t_orig (ﾗﾝｸ1)");
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index03 ON t_orig (馬ID,開催日数,回り)");
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index04 ON t_orig (騎手ID,開催日数,回り)");
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index05 ON t_orig (調教師ID,開催日数,回り)");
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index06 ON t_orig (馬主ID,開催日数,回り)");
                            await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_orig_index07 ON t_orig (ﾚｰｽID,着順)");

                            await conn.BeginTransaction();
                        }

                        foreach (var x in racearr)
                        {
                            var sql = "REPLACE INTO t_orig (" + x.Keys.GetString(",") + ") VALUES (" + x.Keys.Select(x => "?").GetString(",") + ")";
                            var prm = x.Keys.Select(k => SQLiteUtil.CreateParameter(DbType.String, x[k])).ToArray();
                            await conn.ExecuteNonQueryAsync(sql, prm);
                        }
                    }
                    conn.Commit();

                    AddLog($"completed racebase:{racebase}");

                    Progress.Value += 1;
                }

                // 血統情報の作成
                await RefreshKetto(conn);

                //// 産駒成績の更新
                //await RefreshSanku(conn);
            }

            MessageService.Info("Step1 Completed!!");
        });

        private async Task<bool> ExistsOrig(SQLiteControl conn, string raceid)
        {
            if (await conn.ExistsColumn("t_orig", "ﾚｰｽID"))
            {
                var cnt = await conn.ExecuteScalarAsync(
                    $"SELECT COUNT(*) FROM t_orig WHERE ﾚｰｽID = ?",
                    SQLiteUtil.CreateParameter(DbType.String, raceid)
                );
                return 0 < cnt.GetDouble();
            }
            return false;
        }

        private async Task<List<Dictionary<string, string>>> GetSTEP1Racearr(SQLiteControl conn, string raceid)
        {
            return await GetRaceResults(raceid).RunAsync(async arr =>
            {
                if (arr.Count != 0)
                {
                    var oikiri = await GetOikiris(raceid);

                    arr.ForEach(row => SetOikiris(oikiri, row));
                }
            });
        }

        private async IAsyncEnumerable<List<Dictionary<string, string>>> GetSTEP1Racearrs(SQLiteControl conn, string racebase)
        {
            var race01 = $"{racebase}01";
            if (await ExistsOrig(conn, race01)) yield break;

            var race01arr = await GetSTEP1Racearr(conn, race01);
            if (race01arr.Count == 0) yield break;

            yield return race01arr;

            var racearrs = Enumerable.Range(2, 11).Select(i =>
            {
                return GetSTEP1Racearr(conn, $"{racebase}{i.ToString(2)}");
            });

            foreach (var racearr in racearrs)
            {
                yield return await racearr;
            }
        }
    }
}