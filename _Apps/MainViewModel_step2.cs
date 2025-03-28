using AngleSharp.Dom;
using AngleSharp.Text;
using MathNet.Numerics.Statistics;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TBird.Core;
using TBird.DB;
using TBird.DB.SQLite;
using TBird.Web;
using TBird.Wpf;

namespace Netkeiba
{
    public partial class MainViewModel
    {
        public CheckboxItemModel S2Overwrite { get; } = new CheckboxItemModel("", "") { IsChecked = false };

        public IRelayCommand S2EXEC => RelayCommand.Create(async _ =>
        {
            using var selenium = TBirdSeleniumFactory.GetDisposer();
            using (var conn = AppUtil.CreateSQLiteControl())
            {
                var create = S2Overwrite.IsChecked || !await conn.ExistsColumn("t_model", "着順");

                var drops = await conn.ExistsColumn("t_model", "着順") && !create
                    ? await conn.GetRows(r => $"{r.GetValue(0)}", "SELECT DISTINCT ﾚｰｽID FROM t_model")
                    : Enumerable.Empty<string>();

                var maxdate = await conn.ExecuteScalarAsync("SELECT MAX(開催日数) FROM t_orig");
                var mindate = await conn.ExecuteScalarAsync("SELECT MIN(開催日数) FROM t_orig");
                var target = maxdate.GetDouble().Subtract(mindate.GetDouble()).Multiply(0.3).Add(mindate.GetDouble());
                var racbase = await conn.GetRows(r => r.Get<string>(0),
                    "SELECT DISTINCT ﾚｰｽID FROM t_orig WHERE 開催日数 >= ? ORDER BY ﾚｰｽID DESC",
                    SQLiteUtil.CreateParameter(System.Data.DbType.Int64, target)
                );

                var rac = racbase
                    .Where(id => !drops.Contains(id))
                    .ToArray();

                var 馬性 = await AppUtil.Get馬性(conn);
                var 調教場所 = await AppUtil.Get調教場所(conn);
                var 追切 = await AppUtil.Get追切(conn);

                // 血統情報の作成
                await RefreshKetto(conn);

                //// 産駒成績の更新
                //await RefreshSanku(conn);

                // ﾚｰｽ情報の初期化
                await InitializeModelBase(conn);

                Progress.Value = 0;
                Progress.Minimum = 0;
                Progress.Maximum = rac.Length;

                foreach (var raceid in rac)
                {
                    MessageService.Debug($"ﾚｰｽID:開始:{raceid}");

                    // ﾚｰｽ毎の纏まり
                    var racarr = await CreateRaceModel(conn, "v_orig", raceid, 馬性, 調教場所, 追切);
                    var header = Arr("ﾚｰｽID", "開催日数", "枠番", "馬番", "着順", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID");

                    if (create)
                    {
                        create = false;

                        // ﾃｰﾌﾞﾙ作成
                        await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
                        await conn.ExecuteNonQueryAsync(Arr(
                            "CREATE TABLE IF NOT EXISTS t_model (",
                            header.Select(x => $"{x} INTEGER").GetString(","),
                            ",単勝 REAL,Features BLOB, PRIMARY KEY (ﾚｰｽID, 馬番))").GetString(" "));

                        await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_model_index00 ON t_model (開催日数, ﾗﾝｸ2, ﾚｰｽID, 馬番)");
                    }

                    await conn.BeginTransaction();
                    foreach (var ins in racarr)
                    {
                        var prms1 = header.Select(x => SQLiteUtil.CreateParameter(DbType.Int64, ins[x]));
                        var prms2 = SQLiteUtil.CreateParameter(DbType.Single, ins["単勝"]);
                        var prms3 = SQLiteUtil.CreateParameter(DbType.Binary, AppUtil.CreateFeatures(ins));

                        await conn.ExecuteNonQueryAsync(
                            $"REPLACE INTO t_model ({header.GetString(",")},単勝,Features) VALUES ({Enumerable.Repeat("?", header.Length).GetString(",")}, ?, ?)",
                            prms1.Concat(Arr(prms2)).Concat(Arr(prms3)).ToArray()
                        );
                    }
                    conn.Commit();

                    AddLog($"Step5 Proccess ﾚｰｽID: {raceid}");

                    Progress.Value += 1;
                }
                AppSetting.Instance.Save();

                MessageService.Info("Step5 Completed!!");
            }
        });
        private Dictionary<string, Dictionary<string, float>> DEF = new();
        //private Dictionary<long, float> TOU = new();
        private Dictionary<long, Dictionary<string, float>> TOP = new();
        private Dictionary<string, float> TIM = new();
        //private Dictionary<long, float> SYO = new();
        private Dictionary<string, float> UMASYO1 = new();
        private Dictionary<string, float> UMASYO2 = new();

        private async Task InitializeModelBase(SQLiteControl conn)
        {
            // 血統情報付きのVIEWを作成
            await conn.ExecuteNonQueryAsync($"CREATE VIEW IF NOT EXISTS v_orig1 AS select c.*, a.父ID         , a.母ID          from t_orig  c, t_ketto a where a.馬ID = c.馬ID");
            await conn.ExecuteNonQueryAsync($"CREATE VIEW IF NOT EXISTS v_orig2 AS select c.*, a.父ID 母父ID  , a.母ID 母母ID   from v_orig1 c, t_ketto a where a.馬ID = c.母ID");
            await conn.ExecuteNonQueryAsync($"CREATE VIEW IF NOT EXISTS v_orig3 AS select c.*, a.父ID 母母父ID, a.母ID 母母母ID from v_orig2 c left outer join t_ketto a on a.馬ID = c.母母ID");
            await conn.ExecuteNonQueryAsync($"CREATE VIEW IF NOT EXISTS v_shutuba1 AS select c.*, a.父ID         , a.母ID          from t_shutuba  c left outer join t_ketto a on a.馬ID = c.馬ID");
            await conn.ExecuteNonQueryAsync($"CREATE VIEW IF NOT EXISTS v_shutuba2 AS select c.*, a.父ID 母父ID  , a.母ID 母母ID   from v_shutuba1 c, t_ketto a where a.馬ID = c.母ID");
            await conn.ExecuteNonQueryAsync($"CREATE VIEW IF NOT EXISTS v_shutuba3 AS select c.*, a.父ID 母母父ID, a.母ID 母母母ID from v_shutuba2 c left outer join t_ketto a on a.馬ID = c.母母ID");
            await conn.ExecuteNonQueryAsync($"CREATE VIEW IF NOT EXISTS v_race AS SELECT ﾚｰｽID, COUNT(ﾚｰｽID) 頭数, MAX(ﾀｲﾑ指数) ﾀｲﾑ指数, MIN(ﾀｲﾑ変換) ﾀｲﾑ変換, MIN(上り) 上り, MAX(斤量) 斤量, SUM(賞金) 総額, SUM(CASE WHEN 着順 = 1 THEN 賞金 ELSE 0 END) 賞金 FROM (SELECT ﾚｰｽID, 着順, CAST(ﾀｲﾑ指数 AS REAL) ﾀｲﾑ指数, CAST(ﾀｲﾑ変換 AS REAL) ﾀｲﾑ変換, CAST(上り AS REAL) 上り, CAST(斤量 AS REAL) 斤量, CAST(賞金 AS REAL) 賞金 FROM t_orig) GROUP BY ﾚｰｽID");

            //TOU = await conn.GetRows("SELECT ﾚｰｽID, 頭数 FROM v_race").RunAsync(arr =>
            //{
            //    return arr.ToDictionary(x => x["ﾚｰｽID"].GetInt64(), x => x.SINGLE("頭数"));
            //});

            if (!TIM.Any())
            {
                TIM = await conn.GetRows("SELECT 馬場, 距離, 障害, AVG(ﾀｲﾑ変換) ﾀｲﾑ変換 FROM (SELECT 馬場, 距離, (ﾗﾝｸ1 LIKE '%障%') 障害, ﾀｲﾑ変換 FROM t_orig) GROUP BY 馬場, 距離, 障害").RunAsync(arr =>
                {
                    return arr.ToDictionary(x => $"{x["馬場"]},{x["距離"]},{x["障害"].Int32() == 1}", x => x.SINGLE("ﾀｲﾑ変換"));
                });
            }

            //SYO = await conn.GetRows("SELECT ﾚｰｽID, 賞金 FROM v_race").RunAsync(arr =>
            //{
            //    return arr.ToDictionary(x => x["ﾚｰｽID"].GetInt64(), x => x.SINGLE("賞金"));
            //});

            if (!UMASYO1.Any())
            {
                var umasyo = Arr(
                    $"SELECT a.ﾚｰｽID, a.馬ID, IFNULL(SUM(c.賞金 / b.着順), 100) 賞金",
                    $"FROM t_orig a",
                    $"LEFT JOIN t_orig b ON a.馬ID = b.馬ID AND b.開催日数 BETWEEN a.開催日数 - {開催日数MIN} AND a.開催日数 - {開催日数MAX}",
                    $"LEFT JOIN v_race c ON b.ﾚｰｽID = c.ﾚｰｽID",
                    $"GROUP BY a.ﾚｰｽID, a.馬ID"
                );
                UMASYO1 = await conn.GetRows(umasyo.GetString(" ")).RunAsync(arr =>
                {
                    return arr.ToDictionary(x => $"{x["ﾚｰｽID"]},{x["馬ID"]}", x => x.SINGLE("賞金"));
                });
                UMASYO2 = UMASYO1.GroupBy(x => x.Key.Split(',')[0]).ToDictionary(x => x.Key, x => x.Sum(y => y.Value));
            }

            //TIM = await conn.GetRows("SELECT ﾚｰｽID, MEDIAN(ﾀｲﾑ指数) ﾀｲﾑ合計 FROM t_orig WHERE 着順 <= 6 GROUP BY ﾚｰｽID").RunAsync(arr =>
            //{
            //    return arr.ToDictionary(x => x["ﾚｰｽID"].GetInt64(), x => x.SINGLE("ﾀｲﾑ合計"));
            //});

            // ﾃﾞﾌｫﾙﾄ値の作製
            if (!DEF.Any())
            {
                DEF = await conn.GetRows<object>(Arr(
                    $"WITH w_tou AS (SELECT ﾚｰｽID, ﾀｲﾑ指数, ﾀｲﾑ変換 FROM v_race)",
                    $"SELECT",
                    $"ﾗﾝｸ1,",
                    $"AVG(t_orig.着順) 着順,",
                    $"AVG(t_orig.体重) 体重,",
                    $"AVG(t_orig.単勝) 単勝,",
                    $"AVG(t_orig.距離) 距離,",
                    $"AVG(t_orig.上り) 上り,",
                    $"AVG(t_orig.ﾀｲﾑ指数) ﾀｲﾑ指数,",
                    $"AVG(t_orig.ﾀｲﾑ指数 / w_tou.ﾀｲﾑ指数) ﾀｲﾑ差,",
                    $"AVG(t_orig.ﾀｲﾑ変換 - w_tou.ﾀｲﾑ変換) 勝時差,",
                    $"0 賞金,",
                    $"AVG(t_orig.斤量) 斤量",
                    $"FROM t_orig, w_tou",
                    $"WHERE t_orig.ﾚｰｽID = w_tou.ﾚｰｽID",
                    $"GROUP BY ﾗﾝｸ1"
                ).GetString(" ")).RunAsync(val =>
                {
                    var dic = val.ToDictionary(
                        x => x["ﾗﾝｸ1"].Str(),
                        x => x.Where(x => x.Key != "ﾗﾝｸ1").ToDictionary(y => y.Key.Str(), y => y.Value.GetSingle())
                    );
                    foreach (var row in dic.Values)
                    {
                        row["斤上"] = Get斤上(row["上り"], row["斤量"]);
                        row["時間"] = 16.237541F;
                        row["勝上差"] = row["斤上"] - row["斤上"] * 0.9F;
                        row["出走間隔"] = 40F;
                    }
                    return dic;
                });
            }

            if (!TOP.Any())
            {
                TOP = await conn.GetRows($"SELECT ﾚｰｽID, 頭数, 賞金, ﾀｲﾑ変換, 上り, 斤量, ﾀｲﾑ指数 FROM v_race").RunAsync(val =>
                {
                    return val.ToDictionary(
                        x => x["ﾚｰｽID"].GetInt64(),
                        x => x.Where(x => x.Key != "ﾚｰｽID").ToDictionary(y => y.Key, y => y.Value.GetSingle())
                    );
                });
            }
        }

        private float GetSingle(IEnumerable<float> arr, float def, Func<IEnumerable<float>, float> func)
        {
            return arr.Where(x => !float.IsNaN(x)).Any() ? func(arr.Where(x => !float.IsNaN(x))) : def;
        }

        private float Median(IEnumerable<float> arr, float def)
        {
            return GetSingle(arr, def, ret => ret.Percentile(50));
        }

        private float Median(IEnumerable<Dictionary<string, object>> arr, string rank, string n)
        {
            return Median(arr, n, DEF[rank][n]);
        }

        private float Median(IEnumerable<Dictionary<string, object>> arr, string n, float def)
        {
            return Median(arr.Select(x => x.SINGLE(n)), def);
        }

        private async Task<IEnumerable<Dictionary<string, object>>> CreateRaceModel(SQLiteControl conn, string tablename, string raceid, List<string> 馬性, List<string> 調教場所, List<string> 追切)
        {
            // 同ﾚｰｽの平均を取りたいときに使用する
            var 同ﾚｰｽ = await conn.GetRows($"SELECT * FROM {tablename}2 WHERE ﾚｰｽID = ?",
                SQLiteUtil.CreateParameter(System.Data.DbType.String, raceid)
            );

            if (!TOP.ContainsKey(raceid.GetInt64()))
            {
                TOP[raceid.GetInt64()] = new Dictionary<string, float>()
                {
                    { "頭数" , 同ﾚｰｽ.Count }
                };
            }

            if (同ﾚｰｽ.Any(x => !UMASYO2.ContainsKey($"{raceid},{x["馬ID"]}")))
            {
                var umasyosql = Arr(
                    $"SELECT 馬ID, IFNULL(SUM(c.賞金 / b.着順), 100) 賞金",
                    $"FROM t_orig b, ( SELECT ﾚｰｽID, SUM(CASE WHEN 着順 = 1 THEN CAST(賞金 AS REAL) ELSE 0 END) 賞金 FROM t_orig WHERE 開催日数 BETWEEN ? AND ? GROUP BY ﾚｰｽID ) c",
                    $"WHERE b.ﾚｰｽID = c.ﾚｰｽID AND b.馬ID IN ({同ﾚｰｽ.Select(x => "?").GetString(",")})",
                    $"GROUP BY 馬ID"
                );
                var umasyopara = Arr(開催日数MIN, 開催日数MAX)
                    .Select(i => SQLiteUtil.CreateParameter(DbType.Int64, 同ﾚｰｽ.First()["開催日数"].GetInt64() - i))
                    .Concat(同ﾚｰｽ.Select(src => SQLiteUtil.CreateParameter(DbType.String, src["馬ID"])))
                    .ToArray();
                var umasyo = await conn.GetRows(umasyosql.GetString(" "), umasyopara).RunAsync(arr =>
                {
                    return arr.ToDictionary(x => x["馬ID"], x => x["賞金"].GetSingle());
                });

                foreach (var src in 同ﾚｰｽ)
                {
                    UMASYO1[$"{raceid},{src["馬ID"]}"] = umasyo.ContainsKey(src["馬ID"].Str()) ? umasyo[src["馬ID"].Str()] : 100F;
                }
                UMASYO2[raceid] = 同ﾚｰｽ.Sum(src => UMASYO1[$"{raceid},{src["馬ID"]}"]);
            }

            // ﾚｰｽ毎の纏まり
            var racarr = await 同ﾚｰｽ.AsParallel().WithDegreeOfParallelism(2).Select(src => ToModel(conn, src, 馬性, 調教場所, 追切)).WhenAll();
            var keys = racarr.First().Keys.Where(y => !AppUtil.DropKeys.Contains(y)).ToArray();

            // 他の馬との比較
            keys.ForEach(key =>
            {
                var arr = racarr.Select(x => x.SINGLE(key)).Where(x => !float.IsNaN(x)).ToArray();

                racarr.ForEach(dic =>
                {
                    var val = dic.SINGLE(key);

                    dic[$"{key}_Diff"] = val == 0F ? 0F : arr.Percentile(50) / val;
                });
            });

            return racarr;
        }

        private async Task<Dictionary<string, object>> ToModel(SQLiteControl conn, Dictionary<string, object> src, List<string> 馬性, List<string> 調教場所, List<string> 追切)
        {
            var dic = new Dictionary<string, object>();
            var rnk = src["ﾗﾝｸ1"].Str();
            var rankwhere = rnk.Contains("障") ? "" : " AND 回り <> '障' ";

            // ﾍｯﾀﾞ情報
            dic["ﾚｰｽID"] = src["ﾚｰｽID"].GetInt64();
            dic["開催日数"] = src["開催日数"].GetInt64();
            dic["ﾗﾝｸ1"] = AppUtil.ﾗﾝｸ1Arr.IndexOf(rnk);
            dic["ﾗﾝｸ2"] = AppUtil.Getﾗﾝｸ2(src["ﾗﾝｸ2"]);

            // 予測したいﾃﾞｰﾀ
            dic["着順"] = src["着順"].GetInt64();
            dic["単勝"] = src.SINGLE("単勝");

            // 馬毎に違う情報
            dic["枠番"] = src["枠番"].GetInt64();
            dic["馬番"] = src["馬番"].GetInt64();
            dic["馬ID"] = src["馬ID"].GetInt64();
            dic["馬性"] = 馬性.IndexOf(src["馬性"]);
            dic["馬齢"] = src.SINGLE("馬齢");
            dic["斤量"] = src.SINGLE("斤量");
            dic["枠"] = dic.SINGLE("馬番") / TOP[dic["ﾚｰｽID"].GetInt64()]["頭数"];
            dic["距離"] = src.SINGLE("距離");

            // 追切基準で濃い色付きの数
            dic[$"追切基準1"] = Arr(1, 2, 3, 4, 5)
                .Select(j => src[$"追切基準{j}"].Str())
                .Count(x => x == "TokeiColor01").GetSingle() / 5F;
            // 追切基準で薄い色付きと濃い色付きの数
            dic[$"追切基準2"] = Arr(1, 2, 3, 4, 5)
                .Select(j => src[$"追切基準{j}"].Str())
                .Count(x => x == "TokeiColor01" || x == "TokeiColor02").GetSingle() / 5F;

            dic["調教場所"] = 調教場所.IndexOf(src["調教場所"]);
            dic["追切評価"] = 追切.IndexOf(src["追切評価"]).GetSingle() / 追切.Count.GetSingle();

            dic["能力比較A"] = UMASYO1[$"{src["ﾚｰｽID"]},{src["馬ID"]}"];
            dic["能力比較B"] = UMASYO1[$"{src["ﾚｰｽID"]},{src["馬ID"]}"] / UMASYO2[$"{src["ﾚｰｽID"]}"];

            void ADD情報(string key, List<Dictionary<string, object>> arr, int i)
            {
                float GET着順(Dictionary<string, object> tgt, float jun)
                {
                    var 頭数 = TOP[tgt["ﾚｰｽID"].GetInt64()]["頭数"];
                    var 着順 = tgt.SINGLE("着順") + jun;
                    //var 基礎点 = Math.Abs(AppUtil.RankRateBase - 着順).Pow(1.25F) * (AppUtil.RankRateBase < 着順 ? -1F : 1F);
                    //var ﾗﾝｸ点 = AppUtil.RankRate[tgt["ﾗﾝｸ1"].Str()] / 着順.Pow(0.75F);
                    //return (着順 / 頭数).Pow(1.5F);
                    return UMASYO2[$"{tgt["ﾚｰｽID"]}"] / 着順;
                }

                float GET距離(Dictionary<string, object> tgt) => Arr(tgt, src).Select(y => y["距離"].Single()).Run(arr => arr.Min() / arr.Max());

                var KEY = $"{key}{i.ToString(2)}";

                void ADDﾗﾝｸ情報(string childkey, IEnumerable<Dictionary<string, object>> arr1, IEnumerable<Dictionary<string, object>> arr2)
                {
                    var tgts = !rnk.Contains("障")
                        ? Arr(
                            Arr("G1古", "G2古", "G3古", "オープン古"),
                            Arr("G1古", "G2古", "G3古", "オープン古", "3勝古", "2勝古", "1勝古", "G1ク", "G2ク", "G3ク", "オープンク", "2勝ク", "1勝ク", "未勝利ク", "新馬ク")
                        )
                        : Arr(
                            Arr("G1古", "G2古", "G3古", "オープン古", "3勝古", "2勝古", "1勝古", "G1ク", "G2ク", "G3ク", "オープンク", "2勝ク", "1勝ク", "未勝利ク", "新馬ク", "G1障", "G2障", "G3障", "オープン障", "未勝利障"),
                            Arr("G1障", "G2障", "G3障", "オープン障", "未勝利障")
                        );
                    tgts.ForEach((keys, j) =>
                    {
                        float[] ToArr(IEnumerable<Dictionary<string, object>> tmp, float jun) =>
                            tmp.Where(x => keys.Contains(x["ﾗﾝｸ1"].Str())).Select(tgt => GET着順(tgt, jun)).ToArray();

                        // 計算したい値
                        var tmp1 = ToArr(arr2, 0.0F).Run(tmp => tmp.Any() ? tmp : ToArr(arr1, 0.5F));

                        dic[$"{KEY}{childkey}{j.ToString(2)}Me"] = tmp1.Any()
                            ? tmp1.Median()
                            : 0F;
                        //dic[$"{KEY}{childkey}{j.ToString(2)}Ma"] = tmp1.Any()
                        //    ? tmp1.Max()
                        //    : 0F;
                        //dic[$"{KEY}{childkey}{j.ToString(2)}Mi"] = tmp1.Any()
                        //    ? tmp1.Min()
                        //    : 0F;
                    });
                }

                ADDﾗﾝｸ情報("着順A", arr, arr);

                ADDﾗﾝｸ情報("着順B", arr, arr.Where(x => x["馬場"] == src["馬場"]));

                ADDﾗﾝｸ情報("着順C", arr, arr.Where(x => x["馬場状態"] == src["馬場状態"]));

                ADDﾗﾝｸ情報("着順D", arr, arr.Where(x => 0.75F < GET距離(x)));

                dic[$"{KEY}距離"] = Median(arr.Select(GET距離), 0.75F);

                dic[$"{KEY}出遅"] = Calc(arr.Count(x => x["備考"].Str().Contains("出遅")), arr.Count, (c1, c2) => c2 == 0 ? 0 : c1 / c2).GetSingle() * 100F;

                dic[$"{KEY}ﾀｲﾑ差"] = !rnk.Contains("障")
                    ? Median(arr.Select(x => x.SINGLE("ﾀｲﾑ指数") / TOP[x["ﾚｰｽID"].GetInt64()]["ﾀｲﾑ指数"]), DEF[rnk]["ﾀｲﾑ差"])
                    : 0F;

                dic[$"{KEY}ﾀｲﾑ変換"] = Median(arr.Select(x => x.SINGLE("ﾀｲﾑ変換") - TOP[x["ﾚｰｽID"].GetInt64()]["ﾀｲﾑ変換"]), DEF[rnk]["勝時差"]);

                dic[$"{KEY}ﾀｲﾑ基準"] = Median(arr.Select(x => x.SINGLE("ﾀｲﾑ変換") - TIM[$"{x["馬場"]},{x["距離"]},{x["ﾗﾝｸ1"].Str().Contains("障")}"]), 0F);
            }

            List<Dictionary<string, object>>[] CREATE情報(IEnumerable<Dictionary<string, object>> arr, int[] takes)
            {
                return takes.Select(i => arr.Take(i).ToList()).ToArray();
            };

            var 馬情報 = await conn.GetRows(
                    $"SELECT {SELECT_DATA} FROM t_orig WHERE 馬ID = ? AND 開催日数 < ? {rankwhere} ORDER BY 開催日数 DESC",
                    SQLiteUtil.CreateParameter(DbType.String, src["馬ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"])
            ).RunAsync(arr => arr.Take(20).ToArray());

            // 出走間隔
            dic[$"出走間隔"] = 馬情報.Any() ? src.SINGLE("開催日数") - 馬情報[0].SINGLE("開催日数") : DEF[rnk]["出走間隔"];

            CREATE情報(馬情報, Arr(3, 6)).ForEach((arr, i) =>
            {
                // 通過順変換ﾌｧﾝｸｼｮﾝ
                float GET通過(object v) => v.Str().Split('-')
                    .Skip(1)
                    .Take(1)
                    .Run(xxx => xxx.Any() ? xxx.Average(x => x.GetSingle() / TOP[src["ﾚｰｽID"].GetInt64()]["頭数"]) : 0.5F);

                ADD情報("馬", arr, i);

                var KEY = $"馬{i.ToString(2)}";

                // 斤量
                dic[$"斤量{KEY}"] = Median(arr, rnk, "斤量");

                dic[$"ﾀｲﾑ指数A{KEY}"] = !rnk.Contains("障") ? Median(arr, rnk, "ﾀｲﾑ指数") : 0F;

                // 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
                dic[$"通過{KEY}"] = Median(arr.Select(x => GET通過(x["通過"])), TOP[dic["ﾚｰｽID"].GetInt64()]["頭数"] / 2);

                // 上り×斤量
                dic[$"斤上{KEY}"] = Median(arr.Select(x => Get斤上(x)), DEF[rnk]["斤上"]);

                // 1着との差(上り×斤量)
                dic[$"勝上差{KEY}"] = Median(arr.Select(x => Get斤上(x) - Get斤上(TOP[x["ﾚｰｽID"].GetInt64()])), DEF[rnk]["勝上差"]);
            });

            //using (MessageService.Measure("父馬"))
            {
                var 両親馬情報 = await conn.GetRows(
                        $"SELECT {SELECT_DATA} FROM t_orig WHERE 馬ID IN (?, ?, ?) {rankwhere}",
                        SQLiteUtil.CreateParameter(DbType.String, src["父ID"]),
                        SQLiteUtil.CreateParameter(DbType.String, src["母ID"]),
                        SQLiteUtil.CreateParameter(DbType.String, src["母父ID"])
                );
                CREATE情報(両親馬情報, Arr(50000)).ForEach((arr, i) =>
                {
                    ADD情報("両親馬", arr, i);
                });
            }

            //using (MessageService.Measure("産父情報"))
            {
                var 産父情報 = await conn.GetRows(
                    $"SELECT {SELECT_DATA} FROM v_orig1 WHERE 父ID = ? AND 開催日数 BETWEEN ? AND ? {rankwhere}",
                    SQLiteUtil.CreateParameter(DbType.String, src["父ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MIN),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MAX)
                );
                CREATE情報(産父情報, Arr(50000)).ForEach((arr, i) =>
                {
                    ADD情報("産父", arr, i);
                });
            }

            //using (MessageService.Measure("産母父"))
            {
                var 産母父情報 = await conn.GetRows(
                    $"SELECT {SELECT_DATA} FROM v_orig2 WHERE 母父ID = ? AND 開催日数 BETWEEN ? AND ? {rankwhere}",
                    SQLiteUtil.CreateParameter(DbType.String, src["母父ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MIN),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MAX)
                );
                CREATE情報(産母父情報, Arr(50000)).ForEach((arr, i) =>
                {
                    ADD情報("産母父", arr, i);
                });
            }

            //using (MessageService.Measure("産父母父"))
            {
                var 産父母父情報 = await conn.GetRows(
                    $"SELECT {SELECT_DATA} FROM v_orig2 WHERE 父ID = ? AND 母父ID = ? AND 開催日数 BETWEEN ? AND ? {rankwhere}",
                    SQLiteUtil.CreateParameter(DbType.String, src["父ID"]),
                    SQLiteUtil.CreateParameter(DbType.String, src["母父ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MIN),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MAX)
                );
                CREATE情報(産父母父情報, Arr(50000)).ForEach((arr, i) =>
                {
                    ADD情報("産父母父", arr, i);
                });
            }

            //using (MessageService.Measure("騎手"))
            {
                var 騎手情報 = await conn.GetRows(
                    $"SELECT {SELECT_DATA} FROM t_orig WHERE 騎手ID = ? AND 開催日数 BETWEEN ? AND ? {rankwhere}",
                    SQLiteUtil.CreateParameter(DbType.String, src["騎手ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MIN),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MAX)
                );
                CREATE情報(騎手情報, Arr(50000)).ForEach((arr, i) =>
                {
                    ADD情報("騎手", arr, i);
                });
            }

            //using (MessageService.Measure("調教騎手"))
            {
                var 調教騎手情報 = await conn.GetRows(
                    $"SELECT {SELECT_DATA} FROM t_orig WHERE 騎手ID = ? AND 調教師ID = ? AND 開催日数 BETWEEN ? AND ? {rankwhere}",
                    SQLiteUtil.CreateParameter(DbType.String, src["騎手ID"]),
                    SQLiteUtil.CreateParameter(DbType.String, src["調教師ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MIN),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 開催日数MAX)
                );
                CREATE情報(調教騎手情報, Arr(50000)).ForEach((arr, i) =>
                {
                    ADD情報("調教騎手", arr, i);
                });
            }

            return dic;
        }

        private const int 開催日数MIN = 365;
        private const int 開催日数MAX = 4;
        private const string SELECT_DATA = "ﾚｰｽID, ﾗﾝｸ1, ﾗﾝｸ2, 着順, 上り, 斤量, ﾀｲﾑ変換, ﾀｲﾑ指数, 賞金, 距離, 馬場, 馬場状態, 備考, 開催日数, 通過";

        private float Get斤上(float 上り, float 斤量) => 上り.GetSingle() * 600F / (斤量.GetSingle() + 545F);
        private float Get斤上(Dictionary<string, float> src) => Get斤上(src["上り"], src["斤量"]);
        private float Get斤上(Dictionary<string, object> src) => Get斤上(src["上り"].GetSingle(), src["斤量"].GetSingle());

        private async Task RefreshKetto(SQLiteControl conn)
        {
            var keys = await conn.ExistsColumn("t_ketto", "馬ID").RunAsync(exists =>
            {
                return exists
                    ? "SELECT DISTINCT 馬ID FROM t_orig WHERE NOT EXISTS (SELECT * FROM t_ketto WHERE t_orig.馬ID = t_ketto.馬ID)"
                    : "SELECT DISTINCT 馬ID FROM t_orig";
            }).RunAsync(async sql => await conn.GetRows(r => r.Get<string>(0), sql));

            await RefreshKetto(conn, keys);
        }

        private async Task RefreshKetto(SQLiteControl conn, IEnumerable<string> keys, bool progress = true)
        {
            // ﾃｰﾌﾞﾙ作成
            await conn.ExecuteNonQueryAsync("CREATE TABLE IF NOT EXISTS t_ketto (馬ID, 父ID, 母ID, PRIMARY KEY (馬ID))");

            var newkeys = await keys.WhereAsync(async uma =>
            {
                return await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) CNT FROM t_ketto WHERE 馬ID = ?", SQLiteUtil.CreateParameter(DbType.String, uma)) == 0;
            }).RunAsync(arr => arr.ToArray());

            if (progress)
            {
                Progress.Minimum = 0;
                Progress.Maximum = newkeys.Length;
                Progress.Value = 0;
            }

            foreach (var chunk in newkeys.Chunk(100))
            {
                await conn.BeginTransaction();
                foreach (var ketto in chunk.Select(uma => GetKetto(uma)))
                {
                    await foreach (var dic in ketto)
                    {
                        if (!string.IsNullOrEmpty(dic["馬ID"]))
                        {
                            await conn.ExecuteNonQueryAsync("REPLACE INTO t_ketto (馬ID,父ID,母ID) VALUES (?, ?, ?)",
                                SQLiteUtil.CreateParameter(DbType.String, dic["馬ID"]),
                                SQLiteUtil.CreateParameter(DbType.String, dic["父ID"]),
                                SQLiteUtil.CreateParameter(DbType.String, dic["母ID"])
                            );
                        }
                    }
                    if (progress) Progress.Value += 1;
                }
                conn.Commit();
            }
        }

        //private async Task RefreshSanku(SQLiteControl conn)
        //{
        //    var keys = await conn.ExistsColumn("t_sanku", "馬ID").RunAsync(exists =>
        //    {
        //        var sqlbase = "SELECT DISTINCT 馬ID FROM t_ketto";

        //        return exists
        //            ? $"WITH w_ketto AS ({sqlbase}) SELECT * FROM w_ketto WHERE NOT EXISTS (SELECT * FROM t_sanku WHERE w_ketto.馬ID = t_sanku.馬ID)"
        //            : $"WITH w_ketto AS ({sqlbase}) SELECT * FROM w_ketto";
        //    }).RunAsync(async sql => await conn.GetRows(r => r.Get<string>(0), sql));

        //    await RefreshSanku(conn, keys);
        //}

        //private async Task RefreshSanku(SQLiteControl conn, IEnumerable<string> keys, bool progress = true)
        //{
        //    var existssanku = await conn.ExistsColumn("t_sanku", "馬ID");
        //    var newkeys = await existssanku.Run(async exists =>
        //    {
        //        var sql = Arr(
        //            $"WITH w_ketto AS (SELECT 父ID, 母ID FROM t_ketto WHERE 馬ID IN ({keys.Select(x => $"'{x}'").GetString(",")}))",
        //            $"SELECT DISTINCT 父ID FROM w_ketto WHERE 父ID NOT IN (SELECT 馬ID FROM t_sanku)",
        //            $"UNION",
        //            $"SELECT DISTINCT 父ID FROM t_ketto WHERE 馬ID IN (SELECT 母ID FROM w_ketto) AND 父ID NOT IN (SELECT 馬ID FROM t_sanku)"
        //        ).GetString(" ");

        //        return exists
        //            ? await conn.GetRows(r => r.Get<string>(0), sql)
        //            : keys;
        //    }).RunAsync(arr => arr.ToArray());

        //    if (progress)
        //    {
        //        Progress.Minimum = 0;
        //        Progress.Maximum = newkeys.Length;
        //        Progress.Value = 0;
        //    }

        //    foreach (var chunk in newkeys.Chunk(100))
        //    {
        //        if (existssanku) await conn.BeginTransaction();
        //        foreach (var sanku in chunk.Select(uma => GetSanku(uma)))
        //        {
        //            await foreach (var dic in sanku)
        //            {
        //                if (!existssanku)
        //                {
        //                    existssanku = true;

        //                    // ﾃｰﾌﾞﾙ作成
        //                    await conn.ExecuteNonQueryAsync(Arr("CREATE TABLE IF NOT EXISTS t_sanku (馬ID,年度,順位 REAL,出走頭数 REAL,勝馬頭数 REAL,出走回数 REAL,勝利回数 REAL,重出 REAL,重勝 REAL,特出 REAL,特勝 REAL,平出 REAL,平勝 REAL,芝出 REAL,芝勝 REAL,ダ出 REAL,ダ勝 REAL,EI REAL,賞金 REAL,芝距 REAL,ダ距 REAL,",
        //                        "PRIMARY KEY (馬ID,年度))").GetString(" ")
        //                    );

        //                    await conn.ExecuteNonQueryAsync(Arr(
        //                        "CREATE TABLE IF NOT EXISTS t_sanku (",
        //                        dic.Keys.Select(x => Arr("馬ID", "年度").Contains(x) ? x : $"{x} REAL").GetString(","),
        //                        ",PRIMARY KEY (馬ID,年度))").GetString(" "));

        //                    await conn.BeginTransaction();
        //                }

        //                await conn.ExecuteNonQueryAsync(
        //                    $"REPLACE INTO t_sanku ({dic.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", dic.Keys.Count).GetString(",")})",
        //                    dic.Values.Select((x, i) => SQLiteUtil.CreateParameter(i < 2 ? DbType.String : DbType.Single, x)).ToArray()
        //                );
        //            }
        //            if (progress) Progress.Value += 1;
        //        }
        //        conn.Commit();
        //    }
        //}
    }
}