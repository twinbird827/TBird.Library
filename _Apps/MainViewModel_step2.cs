using AngleSharp.Dom;
using AngleSharp.Text;
using ControlzEx.Standard;
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

                var ﾗﾝｸ2 = AppUtil.Getﾗﾝｸ2(conn);
                var 馬性 = await AppUtil.Get馬性(conn);
                var 調教場所 = await AppUtil.Get調教場所(conn);
                var 追切 = await AppUtil.Get追切(conn);

                // 血統情報の作成
                await RefreshKetto(conn);

                // 産駒成績の更新
                await RefreshSanku(conn);

                // ﾚｰｽ情報の初期化
                await InitializeModelBase(conn);

                Progress.Value = 0;
                Progress.Minimum = 0;
                Progress.Maximum = rac.Length;

                //var racall = await rac.AsParallel().WithDegreeOfParallelism(4).Select(raceid =>
                //{
                //	AddLog($"Step5 Proccess ﾚｰｽID: {raceid}");

                //	Progress.Value += 1;

                //	return CreateRaceModel(conn, "t_orig", raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切);
                //}).WhenAll();
                //foreach (var racarr in racall)
                //{
                //	// ﾚｰｽ毎の纏まり
                //	var head1 = Arr("ﾚｰｽID", "開催日数", "枠番", "馬番", "着順", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID");
                //	var head2 = Arr("ﾚｰｽID", "開催日数", "着順", "単勝", "人気", "距離", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID");

                //	AppSetting.Instance.Features = null;

                //	if (create)
                //	{
                //		create = false;

                //		// ﾃｰﾌﾞﾙ作成
                //		await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
                //		await conn.ExecuteNonQueryAsync(Arr(
                //			"CREATE TABLE IF NOT EXISTS t_model (",
                //			head1.Select(x => $"{x} INTEGER").GetString(","),
                //			",単勝 REAL,Features BLOB, PRIMARY KEY (ﾚｰｽID, 馬番))").GetString(" "));

                //		await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_model_index00 ON t_model (開催日数, ﾗﾝｸ2, ﾚｰｽID)");
                //	}

                //	await conn.BeginTransaction();
                //	foreach (var ins in racarr)
                //	{
                //		AppSetting.Instance.Features = AppSetting.Instance.Features ?? ins.Keys.Where(x => !head2.Contains(x)).ToArray();

                //		var prms1 = head1.Select(x => SQLiteUtil.CreateParameter(DbType.Int64, ins[x]));
                //		var prms2 = SQLiteUtil.CreateParameter(DbType.Single, ins["単勝"]);
                //		var prms3 = SQLiteUtil.CreateParameter(DbType.Binary,
                //			AppSetting.Instance.Features.SelectMany(x => BitConverter.GetBytes(ins[x))).ToArray()
                //		);

                //		await conn.ExecuteNonQueryAsync(
                //			$"REPLACE INTO t_model ({head1.GetString(",")},単勝,Features) VALUES ({Enumerable.Repeat("?", head1.Length).GetString(",")}, ?, ?)",
                //			prms1.Concat(Arr(prms2)).Concat(Arr(prms3)).ToArray()
                //		);
                //	}
                //	conn.Commit();
                //}
                foreach (var raceid in rac)
                {
                    MessageService.Debug($"ﾚｰｽID:開始:{raceid}");

                    // ﾚｰｽ毎の纏まり
                    var racarr = await CreateRaceModel(conn, "t_orig", raceid, ﾗﾝｸ2, 馬性, 調教場所, 追切);
                    var head1 = Arr("ﾚｰｽID", "開催日数", "枠番", "馬番", "着順", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID");
                    var head2 = Arr("ﾚｰｽID", "開催日数", "着順", "単勝", "人気", "距離", "ﾗﾝｸ1", "ﾗﾝｸ2", "馬ID");

                    AppSetting.Instance.Features = null;

                    if (create)
                    {
                        create = false;

                        // ﾃｰﾌﾞﾙ作成
                        await conn.ExecuteNonQueryAsync("DROP TABLE IF EXISTS t_model");
                        await conn.ExecuteNonQueryAsync(Arr(
                            "CREATE TABLE IF NOT EXISTS t_model (",
                            head1.Select(x => $"{x} INTEGER").GetString(","),
                            ",単勝 REAL,Features BLOB, PRIMARY KEY (ﾚｰｽID, 馬番))").GetString(" "));

                        await conn.ExecuteNonQueryAsync($"CREATE INDEX IF NOT EXISTS t_model_index00 ON t_model (開催日数, ﾗﾝｸ2, ﾚｰｽID)");
                    }

                    await conn.BeginTransaction();
                    foreach (var ins in racarr)
                    {
                        AppSetting.Instance.Features = AppSetting.Instance.Features ?? ins.Keys.Where(x => !head2.Contains(x)).ToArray();

                        var prms1 = head1.Select(x => SQLiteUtil.CreateParameter(DbType.Int64, ins[x]));
                        var prms2 = SQLiteUtil.CreateParameter(DbType.Single, ins["単勝"]);
                        var prms3 = SQLiteUtil.CreateParameter(DbType.Binary,
                            AppSetting.Instance.Features.SelectMany(x => BitConverter.GetBytes(ins.SINGLE(x))).ToArray()
                        );

                        await conn.ExecuteNonQueryAsync(
                            $"REPLACE INTO t_model ({head1.GetString(",")},単勝,Features) VALUES ({Enumerable.Repeat("?", head1.Length).GetString(",")}, ?, ?)",
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
        private Dictionary<long, float> TOU = new();
        private Dictionary<long, float> TIM = new();
        private Dictionary<object, Dictionary<string, object>> TOP = new();

        private async Task InitializeModelBase(SQLiteControl conn)
        {
            TOU = await conn.GetRows("SELECT ﾚｰｽID, COUNT(馬番) 頭数 FROM t_orig GROUP BY ﾚｰｽID").RunAsync(arr =>
            {
                return arr.ToDictionary(x => x["ﾚｰｽID"].GetInt64(), x => x.SINGLE("頭数"));
            });

            TIM = await conn.GetRows("SELECT ﾚｰｽID, MEDIAN(ﾀｲﾑ指数) ﾀｲﾑ合計 FROM t_orig WHERE 着順 <= 6 GROUP BY ﾚｰｽID").RunAsync(arr =>
            {
                return arr.ToDictionary(x => x["ﾚｰｽID"].GetInt64(), x => x.SINGLE("ﾀｲﾑ合計"));
            });

            // ﾃﾞﾌｫﾙﾄ値の作製
            DEF.Clear();
            AppUtil.RankAges.ForEach(async rank =>
            {
                var dic = await conn.GetRow<float>(Arr(
                        $"SELECT",
                        $"AVG(t_orig.着順) 着順,",
                        $"AVG(t_orig.体重) 体重,",
                        $"AVG(t_orig.単勝) 単勝,",
                        $"AVG(t_orig.距離) 距離,",
                        $"AVG(t_orig.上り) 上り,",
                        $"AVG(t_orig.ﾀｲﾑ指数) ﾀｲﾑ指数,",
                        $"AVG(t_orig.ﾀｲﾑ指数 / (SELECT MAX(w_tou.ﾀｲﾑ指数) FROM t_orig w_tou WHERE w_tou.ﾚｰｽID = t_orig.ﾚｰｽID AND w_tou.着順 = 1)) ﾀｲﾑ差,",
                        $"AVG(t_orig.ﾀｲﾑ変換 - (SELECT MIN(w_tou.ﾀｲﾑ変換) FROM t_orig w_tou WHERE w_tou.ﾚｰｽID = t_orig.ﾚｰｽID AND w_tou.着順 = 1)) 勝時差,",
                        $"0 賞金,",
                        $"AVG(t_orig.斤量) 斤量",
                        $"FROM t_orig",
                        $"WHERE t_orig.ﾗﾝｸ1 = ?"
                    ).GetString(" "), SQLiteUtil.CreateParameter(DbType.String, rank == "新馬" ? "未勝利" : rank));

                dic["斤上"] = Get斤上(dic["上り"], dic["斤量"]);
                dic["時間"] = 16.237541F;
                dic["勝上差"] = dic["斤上"] - dic["斤上"] * 0.9F;
                dic["出走間隔"] = 40F;

                DEF.Add(rank, dic);
            });

            TOP = await conn.GetRows(Arr(
                $"SELECT ﾚｰｽID, MIN(CAST(ﾀｲﾑ変換 AS REAL)) ﾀｲﾑ変換, MIN(CAST(上り AS REAL)) 上り, MAX(CAST(斤量 AS REAL)) 斤量, MAX(ﾀｲﾑ指数) ﾀｲﾑ指数 FROM t_orig WHERE 着順 = 1 GROUP BY ﾚｰｽID"
            ).GetString(" ")).RunAsync(val =>
            {
                return val.ToDictionary(
                    x => x["ﾚｰｽID"],
                    x => x.Where(x => x.Key != "ﾚｰｽID").ToDictionary(y => y.Key, y => y.Value)
                );
            });
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

        private float RnkMax(IEnumerable<Dictionary<string, object>> arr, string rank, string n)
        {
            return GetSingle(arr.Select(x => x.SINGLE(n)), DEF[rank][n], x => x.Max());
        }

        private float Median(IEnumerable<Dictionary<string, object>> arr, string n, float def)
        {
            return Median(arr.Select(x => x.SINGLE(n)), def);
        }

        private float Std(IEnumerable<float> arr) => arr.Where(x => !float.IsNaN(x)).Run(xxx => 1 < xxx.Count() ? (float)xxx.StandardDeviation() : 0F);

        private float Var(IEnumerable<float> arr) => arr.Where(x => !float.IsNaN(x)).Run(xxx => 1 < xxx.Count() ? (float)xxx.Variance() : 0F);

        private async Task<IEnumerable<Dictionary<string, object>>> CreateRaceModel(SQLiteControl conn, string tablename, string raceid, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
        {
            // 同ﾚｰｽの平均を取りたいときに使用する
            var 同ﾚｰｽ = await conn.GetRows($"SELECT {tablename}.*, a.父ID 父ID, b.父ID 母父ID FROM {tablename} LEFT JOIN t_ketto a ON {tablename}.馬ID = a.馬ID LEFT JOIN t_ketto b ON a.母ID = b.馬ID WHERE ﾚｰｽID = ?",
                SQLiteUtil.CreateParameter(System.Data.DbType.String, raceid)
            );

            TOU[raceid.GetInt64()] = 同ﾚｰｽ.Count;

            var 開催WHERE = Arr(
                SQLiteUtil.CreateParameter(DbType.Int64, 同ﾚｰｽ[0]["開催日数"].GetInt64()),
                SQLiteUtil.CreateParameter(DbType.Int64, 同ﾚｰｽ[0]["開催日数"].GetInt64() - 365)
            );

            // ﾚｰｽ毎の纏まり
            var racarr = await 同ﾚｰｽ.AsParallel().WithDegreeOfParallelism(2).Select(src => ToModel(conn, src, ﾗﾝｸ2, 馬性, 調教場所, 追切)).WhenAll();

            var drops = Arr("距離", "調教場所", "枠番", "馬番", "馬ID", "着順", "単勝", "ﾚｰｽID", "開催日数", "ﾗﾝｸ1", "ﾗﾝｸ2"); ;
            var keys = racarr.First().Keys.Where(y => !drops.Contains(y)).ToArray();

            // 他の馬との比較
            racarr.ForEach(dic =>
            {
                keys.ForEach(key =>
                {
                    try
                    {
                        var val = dic.SINGLE(key);
                        var arr = racarr.Select(x => x.SINGLE(key)).Where(x => !float.IsNaN(x)).ToArray();
                        var std = Std(arr);

                        //dic[$"{key}A1"] = val - arr.Average();
                        //dic[$"{key}A2"] = val - arr.Percentile(25);
                        //dic[$"{key}A3"] = val - arr.Percentile(75);
                        //dic[$"{key}A4"] = val - arr.Percentile(50);
                        //dic[$"{key}A5"] = val - arr.Max();
                        //dic[$"{key}A6"] = val - arr.Min();
                        //dic[$"{key}A7"] = val * std;
                        //dic[$"{key}B1"] = val == 0 ? 0F : arr.Average() / val * 100;
                        //dic[$"{key}B2"] = val == 0 ? 0F : arr.Percentile(25) / val * 100;
                        //dic[$"{key}B3"] = val == 0 ? 0F : arr.Percentile(75) / val * 100;
                        //dic[$"{key}B4"] = val == 0 ? 0F : arr.Percentile(50) / val * 100;
                        //dic[$"{key}B5"] = val == 0 ? 0F : arr.Sum() / val;
                        //dic[$"{key}B6"] = val == 0 ? 0F : arr.Max() / val * 100;
                        //dic[$"{key}B7"] = val == 0 ? 0F : arr.Min() / val * 100;

                        //dic[$"{key}C1"] = val - arr.Percentile(10);
                        //dic[$"{key}C2"] = val - arr.Percentile(30);
                        //dic[$"{key}C3"] = val - arr.Percentile(50);
                        //dic[$"{key}C4"] = val - arr.Percentile(70);
                        //dic[$"{key}C9"] = Arr(dic[$"{key}C2"], dic[$"{key}C3"], dic[$"{key}C4"]).Average(x => x.GetSingle());
                        dic[$"{key}C0"] = val == 0F ? 0F : arr.Percentile(50) / val;
                        //dic[$"{key}C5"] = val - arr.Percentile(90);

                        //dic[$"{key}D1"] = val - arr.Percentile(20);
                        //dic[$"{key}D2"] = val - arr.Percentile(40);
                        //dic[$"{key}D3"] = val - arr.Percentile(60);
                        //dic[$"{key}D4"] = val - arr.Percentile(80);

                    }
                    catch
                    {
                        throw;
                    }
                });
            });

            return racarr;
        }

        private async Task<Dictionary<string, object>> ToModel(SQLiteControl conn, Dictionary<string, object> src, List<string> ﾗﾝｸ2, List<string> 馬性, List<string> 調教場所, List<string> 追切)
        {
            var dic = new Dictionary<string, object>();
            var rnk = src["ﾗﾝｸ1"].Str();
            var rankwhere = rnk.Contains("障") ? "=" : "<>";

            // ﾍｯﾀﾞ情報
            dic["ﾚｰｽID"] = src["ﾚｰｽID"].GetInt64();
            dic["開催日数"] = src["開催日数"].GetInt64();
            dic["ﾗﾝｸ1"] = AppUtil.RankAges.IndexOf(rnk);
            dic["ﾗﾝｸ2"] = ﾗﾝｸ2.IndexOf(src["ﾗﾝｸ2"]);

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
            dic["枠"] = dic.SINGLE("馬番") / TOU[dic["ﾚｰｽID"].GetInt64()];
            dic["距離"] = src.SINGLE("距離");

            // 追切基準で濃い色付きの数
            dic[$"追切基準1"] = Arr(1, 2, 3, 4, 5)
                .Select(j => src[$"追切基準{j}"].Str())
                .Count(x => x == "TokeiColor01");
            // 追切基準で薄い色付きと濃い色付きの数
            dic[$"追切基準2"] = Arr(1, 2, 3, 4, 5)
                .Select(j => src[$"追切基準{j}"].Str())
                .Count(x => x == "TokeiColor01" || x == "TokeiColor02");

            dic["調教場所"] = 調教場所.IndexOf(src["調教場所"]);
            dic["追切評価"] = 追切.IndexOf(src["追切評価"]);

            float GET着順(Dictionary<string, object> tgt)
            {
                var 頭数 = TOU[tgt["ﾚｰｽID"].GetInt64()];
                var 着順 = tgt.SINGLE("着順");
                var RANK = AppUtil.RankRate[tgt["ﾗﾝｸ1"].Str()];
                return (着順 / 頭数).Pow(1.5F) * (AppUtil.RankRate["G1古"] / RANK);
            }

            float GET距離(Dictionary<string, object> tgt) => Arr(tgt, src).Select(y => y["距離"].Single()).Run(arr => arr.Min() / arr.Max());

            float GET着距(Dictionary<string, object> tgt) => GET着順(tgt) / GET距離(tgt);

            const float RATE = 1.15F;

            void ADD情報(string key, List<Dictionary<string, object>> arr, int i)
            {
                var KEY = $"{key}{i.ToString(2)}";

                dic[$"{KEY}着順A"] = Median(arr, rnk, "着順");
                dic[$"{KEY}着順B"] = Median(arr.Select(GET着順), 1F);
                dic[$"{KEY}着順C"] = Median(arr.Select(GET着距), 1F);

                var tyktmp = arr.Select(GET着距).ToArray();
                dic[$"着順MIN"] = GetSingle(tyktmp, 1.0F, arr => arr.Min());
                dic[$"着順MAX"] = GetSingle(tyktmp, 1.5F, arr => arr.Max());

                dic[$"{KEY}距離"] = Median(arr.Select(GET距離), 0.75F);

                float WHEREGET1(string key) => Median(arr.Where(x => x[key] == src[key]).Select(GET着距), dic.SINGLE($"{KEY}着順C") * RATE);
                dic[$"{KEY}馬場"] = WHEREGET1("馬場");
                dic[$"{KEY}馬場状態"] = WHEREGET1("馬場状態");

                float WHEREGET2(float no, string key1, string key2) => arr.Where(x => x[key1] == src[key1]).Run(xxx => xxx.Any() ? xxx.Count(y => y.SINGLE("着順") <= no) / xxx.Count() : dic.SINGLE(key2));
                dic[$"{KEY}連対1"] = arr.Any() ? arr.Count(y => y.SINGLE("着順") <= 3F) / arr.Count() : 0F;
                dic[$"{KEY}連対2"] = arr.Any() ? arr.Count(y => y.SINGLE("着順") <= 2F) / arr.Count() : 0F;
                dic[$"{KEY}連対3"] = arr.Any() ? arr.Count(y => y.SINGLE("着順") <= 1F) / arr.Count() : 0F;
                dic[$"{KEY}連対馬場1"] = WHEREGET2(3F, "馬場", $"{KEY}連対1");
                dic[$"{KEY}連対馬場2"] = WHEREGET2(2F, "馬場", $"{KEY}連対2");
                dic[$"{KEY}連対馬場3"] = WHEREGET2(1F, "馬場", $"{KEY}連対3");
                dic[$"{KEY}連対馬場状態1"] = WHEREGET2(3F, "馬場状態", $"{KEY}連対1");
                dic[$"{KEY}連対馬場状態2"] = WHEREGET2(2F, "馬場状態", $"{KEY}連対2");
                dic[$"{KEY}連対馬場状態3"] = WHEREGET2(1F, "馬場状態", $"{KEY}連対3");

                dic[$"{KEY}出遅"] = Calc(arr.Count(x => x["備考"].Str().Contains("出遅")), arr.Count, (c1, c2) => c2 == 0 ? 0 : c1 / c2).GetSingle() * 100F;

                dic[$"{KEY}ﾀｲﾑ差"] = !rnk.Contains("障")
                    ? Median(arr.Select(x => x.SINGLE("ﾀｲﾑ指数") / TOP[x["ﾚｰｽID"]].SINGLE("ﾀｲﾑ指数")), DEF[rnk]["ﾀｲﾑ差"])
                    : 0F;
            }

            //void ADDﾗﾝｸ情報(string key, List<Dictionary<string, object>> arr, int i)
            //{
            //	var KEY = $"{key}{i.ToString(2)}";

            //	var rnktmp = AppUtil.RankAges.AsParallel().ToDictionary(
            //		r => r,
            //		r => arr.Where(x => x["ﾗﾝｸ1"].Str() == r).Select(GET着距).ToArray());
            //	AppUtil.RankAges.ForEach(r =>
            //	{
            //		float GetDefault(int i, Func<float[], float> func)
            //		{
            //			if (AppUtil.RankAges.Length <= i)
            //			{
            //				return 1.00F;
            //			}
            //			if (rnktmp[AppUtil.RankAges[i]].Any())
            //			{
            //				return func(rnktmp[AppUtil.RankAges[i]]) * RATE;
            //			}
            //			else
            //			{
            //				return GetDefault(i + 1, func) * RATE;
            //			}
            //		}

            //		var tmp = rnktmp[r];
            //		dic[$"{KEY}着順{r}"] = tmp.Any() ? tmp.Median() : GetDefault(AppUtil.RankAges.IndexOf(r) + 1, xxx => xxx.Median());
            //	});
            //}

            List<Dictionary<string, object>>[] CREATE情報(IEnumerable<Dictionary<string, object>> arr, int[] takes)
            {
                return takes.Select(i => arr.Take(i).ToList()).ToArray();
            };

            var 馬情報 = await conn.GetRows(
                    $"SELECT * FROM t_orig WHERE t_orig.馬ID = ? AND t_orig.開催日数 < ? AND t_orig.回り {rankwhere} '障' ORDER BY t_orig.開催日数 DESC",
                    SQLiteUtil.CreateParameter(DbType.String, src["馬ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"])
            ).RunAsync(arr => arr.Take(20).ToArray());

            // 出走間隔
            dic[$"出走間隔"] = 馬情報.Any() ? src.SINGLE("開催日数") - 馬情報[0].SINGLE("開催日数") : DEF[rnk]["出走間隔"];

            CREATE情報(馬情報, Arr(1, 3, 5, 10)).ForEach((arr, i) =>
            {
                // 通過順変換ﾌｧﾝｸｼｮﾝ
                float GET通過(object v) => v.Str().Split('-')
                    .Skip(1)
                    .Take(1)
                    .Run(xxx => xxx.Any() ? xxx.Average(x => x.GetSingle() / TOU[src["ﾚｰｽID"].GetInt64()]) : 0.5F);

                ADD情報("馬", arr, i);

                var KEY = $"馬{i.ToString(2)}";

                // 斤量
                dic[$"斤量{KEY}"] = Median(arr, rnk, "斤量");

                // 賞金
                dic[$"賞金A{KEY}"] = Median(arr, rnk, "賞金");
                dic[$"賞金B{KEY}"] = RnkMax(arr, rnk, "賞金");

                dic[$"ﾀｲﾑ指数A{KEY}"] = !rnk.Contains("障") ? Median(arr, rnk, "ﾀｲﾑ指数") : 0F;
                dic[$"ﾀｲﾑ指数B{KEY}"] = !rnk.Contains("障") ? RnkMax(arr, rnk, "ﾀｲﾑ指数") : 0F;
                dic[$"ﾀｲﾑ指数C{KEY}"] = !rnk.Contains("障") ? Median(arr.Select(x =>
                    TIM[x["ﾚｰｽID"].GetInt64()] * 0.9F.Pow(x.SINGLE("着順"))
                ), 50F) : 0F;

                // 通過の平均、及び他の馬との比較⇒ﾚｰｽ単位で計算が終わったら
                dic[$"通過{KEY}"] = Median(arr.Select(x => GET通過(x["通過"])), TOU[dic["ﾚｰｽID"].GetInt64()] / 2);

                // 上り×斤量
                dic[$"斤上{KEY}"] = Median(arr.Select(x => Get斤上(x)), DEF[rnk]["斤上"]);

                // 1着との差(上り×斤量)
                dic[$"勝上差{KEY}"] = Median(arr.Select(x => Get斤上(x) - Get斤上(TOP[x["ﾚｰｽID"]])), DEF[rnk]["勝上差"]);
            });

            ////using (MessageService.Measure("父馬"))
            //{
            //	var 父馬情報 = await conn.GetRows(
            //			$"SELECT * FROM t_orig WHERE 馬ID = ? AND 回り {rankwhere} '障' ORDER BY 開催日数 DESC",
            //			SQLiteUtil.CreateParameter(DbType.String, src["父ID"])
            //	);
            //	CREATE情報(父馬情報, Arr(500)).ForEach((arr, i) =>
            //	{
            //		ADD情報("父馬", arr, i);
            //	});
            //}

            ////using (MessageService.Measure("母父馬"))
            //{
            //	var 母父馬情報 = await conn.GetRows(
            //		$"SELECT * FROM t_orig WHERE 馬ID = ? AND 回り {rankwhere} '障' ORDER BY 開催日数 DESC",
            //		SQLiteUtil.CreateParameter(DbType.String, src["母父ID"])
            //	);
            //	CREATE情報(母父馬情報, Arr(500)).ForEach((arr, i) =>
            //	{
            //		ADD情報("母父馬", arr, i);
            //	});
            //}

            //using (MessageService.Measure("産父情報"))
            {
                var 産父情報 = await conn.GetRows(
                    $"SELECT * FROM t_orig WHERE 馬ID IN (SELECT 馬ID FROM t_ketto WHERE 父ID = ?) AND 開催日数 < ? AND 開催日数 > ? AND 回り {rankwhere} '障' ORDER BY 開催日数 DESC",
                    SQLiteUtil.CreateParameter(DbType.String, src["父ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64()),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 365)
                );
                CREATE情報(産父情報, Arr(500)).ForEach((arr, i) =>
                {
                    ADD情報("産父", arr, i);
                });
            }

            //using (MessageService.Measure("産母父"))
            {
                var 産母父情報 = await conn.GetRows(
                    $"SELECT * FROM t_orig WHERE 馬ID IN (SELECT a.馬ID FROM t_ketto a WHERE a.母ID IN (SELECT b.馬ID FROM t_ketto b WHERE b.父ID = ?)) AND 開催日数 < ? AND 開催日数 > ? AND 回り {rankwhere} '障' ORDER BY 開催日数 DESC",
                    SQLiteUtil.CreateParameter(DbType.String, src["母父ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64()),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 365)
                );
                CREATE情報(産母父情報, Arr(500)).ForEach((arr, i) =>
                {
                    ADD情報("産母父", arr, i);
                });
            }

            //using (MessageService.Measure("騎手"))
            {
                var 騎手情報 = await conn.GetRows(
                    $"SELECT * FROM t_orig WHERE 騎手ID = ? AND 開催日数 < ? AND 開催日数 > ? AND 回り {rankwhere} '障' ORDER BY 開催日数 DESC",
                    SQLiteUtil.CreateParameter(DbType.String, src["騎手ID"]),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64()),
                    SQLiteUtil.CreateParameter(DbType.Int64, src["開催日数"].GetInt64() - 365)
                );
                CREATE情報(騎手情報, Arr(500)).ForEach((arr, i) =>
                {
                    ADD情報("騎手", arr, i);
                });
            }

            return dic;
        }

        private float Get斤上(float 上り, float 斤量) => 上り.GetSingle() * 600F / (斤量.GetSingle() + 545F);

        private float Get斤上(Dictionary<string, object> src, string k1, string k2) => Get斤上(src.SINGLE(k1), src.SINGLE(k2));

        private float Get斤上(Dictionary<string, object> src) => Get斤上(src, "上り", "斤量");

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

        private async Task RefreshSanku(SQLiteControl conn)
        {
            var keys = await conn.ExistsColumn("t_sanku", "馬ID").RunAsync(exists =>
            {
                var sqlbase = "SELECT DISTINCT 馬ID FROM t_ketto";

                return exists
                    ? $"WITH w_ketto AS ({sqlbase}) SELECT * FROM w_ketto WHERE NOT EXISTS (SELECT * FROM t_sanku WHERE w_ketto.馬ID = t_sanku.馬ID)"
                    : $"WITH w_ketto AS ({sqlbase}) SELECT * FROM w_ketto";
            }).RunAsync(async sql => await conn.GetRows(r => r.Get<string>(0), sql));

            await RefreshSanku(conn, keys);
        }

        private async Task RefreshSanku(SQLiteControl conn, IEnumerable<string> keys, bool progress = true)
        {
            var existssanku = await conn.ExistsColumn("t_sanku", "馬ID");
            var newkeys = await existssanku.Run(async exists =>
            {
                var sql = Arr(
                    $"WITH w_ketto AS (SELECT 父ID, 母ID FROM t_ketto WHERE 馬ID IN ({keys.Select(x => $"'{x}'").GetString(",")}))",
                    $"SELECT DISTINCT 父ID FROM w_ketto WHERE 父ID NOT IN (SELECT 馬ID FROM t_sanku)",
                    $"UNION",
                    $"SELECT DISTINCT 父ID FROM t_ketto WHERE 馬ID IN (SELECT 母ID FROM w_ketto) AND 父ID NOT IN (SELECT 馬ID FROM t_sanku)"
                ).GetString(" ");

                return exists
                    ? await conn.GetRows(r => r.Get<string>(0), sql)
                    : keys;
            }).RunAsync(arr => arr.ToArray());

            if (progress)
            {
                Progress.Minimum = 0;
                Progress.Maximum = newkeys.Length;
                Progress.Value = 0;
            }

            foreach (var chunk in newkeys.Chunk(100))
            {
                if (existssanku) await conn.BeginTransaction();
                foreach (var sanku in chunk.Select(uma => GetSanku(uma)))
                {
                    await foreach (var dic in sanku)
                    {
                        if (!existssanku)
                        {
                            existssanku = true;

                            // ﾃｰﾌﾞﾙ作成
                            await conn.ExecuteNonQueryAsync(Arr("CREATE TABLE IF NOT EXISTS t_sanku (馬ID,年度,順位 REAL,出走頭数 REAL,勝馬頭数 REAL,出走回数 REAL,勝利回数 REAL,重出 REAL,重勝 REAL,特出 REAL,特勝 REAL,平出 REAL,平勝 REAL,芝出 REAL,芝勝 REAL,ダ出 REAL,ダ勝 REAL,EI REAL,賞金 REAL,芝距 REAL,ダ距 REAL,",
                                "PRIMARY KEY (馬ID,年度))").GetString(" ")
                            );

                            await conn.ExecuteNonQueryAsync(Arr(
                                "CREATE TABLE IF NOT EXISTS t_sanku (",
                                dic.Keys.Select(x => Arr("馬ID", "年度").Contains(x) ? x : $"{x} REAL").GetString(","),
                                ",PRIMARY KEY (馬ID,年度))").GetString(" "));

                            await conn.BeginTransaction();
                        }

                        await conn.ExecuteNonQueryAsync(
                            $"REPLACE INTO t_sanku ({dic.Keys.GetString(",")}) VALUES ({Enumerable.Repeat("?", dic.Keys.Count).GetString(",")})",
                            dic.Values.Select((x, i) => SQLiteUtil.CreateParameter(i < 2 ? DbType.String : DbType.Single, x)).ToArray()
                        );
                    }
                    if (progress) Progress.Value += 1;
                }
                conn.Commit();
            }
        }
    }

    public static class MainViewModel_Extension
    {
        public static float SINGLE(this Dictionary<string, object> x, string key) => x[key].GetSingle();
    }
}