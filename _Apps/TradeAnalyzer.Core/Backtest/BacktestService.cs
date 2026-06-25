using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeAnalyzer.Core.Indicators;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Core.Backtest;

/// <summary>
/// 先読み防止・サバイバーシップ対策を組み込んだウォークフォワード・バックテスト土台。
/// - リバランス日 t の銘柄母集団は signals コマンドが保存した <see cref="Signal"/>（Date≤t / DiscloseDate≤t のみで評価済み）を読む。
/// - エントリは t の翌営業日始値（AdjOpen、無ければ AdjClose）。先読みにならない。
/// - 撤退は MaxHoldDays か ATR ストップ。リターンは調整後価格・手数料/スリッページ込み。
/// 段階1は単純集計（勝率・平均リターン）。Rank-IC/NDCG は段階2で追加。
/// </summary>
public class BacktestService
{
    private readonly AppDbContext _db;
    private readonly BacktestOptions _opt;
    private readonly ILogger<BacktestService> _logger;

    public BacktestService(
        AppDbContext db, IOptions<BacktestOptions> opt, ILogger<BacktestService> logger)
    {
        _db = db;
        _opt = opt.Value;
        _logger = logger;
    }

    /// <summary>
    /// OOS 期間でバックテストを実行し、結果を永続化して返す。
    /// ML/非ML とも母集団は保存済み Signal（signals コマンドの出力）で、選択キーのみ
    /// <paramref name="useMl"/> で MlScore/RuleScore を切り替える（provenance 対称化）。
    /// </summary>
    public async Task<BacktestRun> RunAsync(
        DateOnly isStart, DateOnly isEnd, DateOnly oosStart, DateOnly oosEnd,
        bool useMl, string? label = null, CancellationToken ct = default)
    {
        // 営業日は signals 経路と共通のヘルパで取得（母数起点を1箇所に閉じ込める）。
        var tradingDays = await QueryTradingDaysAsync(_db, oosStart, oosEnd, ct).ConfigureAwait(false);

        // OptionsJson は実効 useMl を反映する。DI singleton の _opt を破壊書換えせず JSON ラウンド
        // トリップでコピーしてから上書きする（手動フィールドコピーは将来のプロパティ追加でコピー漏れし
        // OptionsJson が静かに欠落するため避ける＝全プロパティを自動転写）。
        var optSnapshot = JsonSerializer.Deserialize<BacktestOptions>(JsonSerializer.Serialize(_opt))!;
        optSnapshot.UseMlScore = useMl;

        var run = new BacktestRun
        {
            Label = label,
            InSampleStart = isStart, InSampleEnd = isEnd,
            OutSampleStart = oosStart, OutSampleEnd = oosEnd,
            OptionsJson = JsonSerializer.Serialize(optSnapshot),
        };

        if (tradingDays.Count == 0)
        {
            // 取引日皆無は ingest 不足（signals 未実行とは別事象）。従来どおり空 run を保存して返す。
            _logger.LogWarning("OOS 期間に取引日がありません。ingest 済みか確認してください。");
            _db.BacktestRuns.Add(run);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return run;
        }

        // OOS 範囲に Signal 行が在る日の集合を1回で取得し、入口ガードと reb 日単位の未生成判定を
        // この単一集合に統合する（入口とループ内で別述語の Signal クエリを発行するとどちらかの範囲
        // 条件変更で無言にズレるため、述語を1本化して構造的にズレ余地を消す）。Passed は問わない。
        var signalDays = (await _db.Signals
            .Where(s => s.Date >= oosStart && s.Date <= oosEnd)
            .Select(s => s.Date).Distinct()
            .ToListAsync(ct).ConfigureAwait(false))
            .ToHashSet();

        // 取引日は在るのに OOS ウィンドウに Signal 行が皆無＝signals 未実行の手順違反として明示停止する
        // （両モードとも Signal を読むため、非ML でも signals 実行が前提になった＝段階2の破壊的変更）。
        if (signalDays.Count == 0)
            throw new InvalidOperationException(
                $"OOS 期間 [{oosStart}..{oosEnd}] に Signal 行がありません。signals 未実行の可能性。"
                + " 正しい順序は signals → train → (evaluate) → backtest です。");

        var results = new List<BacktestResult>();

        // 部分的な signals 欠落（signals 後に ingest が伸び新規リバランス日だけ Signal 未生成）を観測する。
        // 入口ガードと同じ signalDays 集合で reb 日単位に判定する（DB 再問い合わせ不要・述語の単一化）。
        int missingDays = 0;
        int rebCount = 0;

        // リバランス日は signals コマンドと共通の純粋関数で列挙し、ML 有無で母数を一致させる。
        foreach (var t in EnumerateRebalanceDays(tradingDays, _opt.RebalanceIntervalDays))
        {
            ct.ThrowIfCancellationRequested();

            // 両モードとも signals が保存した Signal を「単一の真実の源」として読む（同一 picks 母集団）。
            // DB が signals→backtest 間で変わっても ML/非ML が定義上同一母集団を共有する。
            var rows = await _db.Signals.AsNoTracking()
                .Where(s => s.Date == t && s.Passed)
                .ToListAsync(ct).ConfigureAwait(false);

            // 未生成日の判定は Passed フィルタ済みの rows.Count==0 ではなく「その reb 日に Signal 行が一切無い」
            // で行う（rows.Count==0 は全銘柄 rule 不通過の正常日と区別できない＝Passed=false 行も保存される）。
            // 入口で構築した signalDays（OOS 範囲の Signal 保有日）と in-memory 照合する（DB 往復ゼロ）。
            rebCount++;
            if (!signalDays.Contains(t)) missingDays++;

            // silent fallback 禁止: 並べ替え前に null 検査し、未推論を明示エラーで止める
            // （double.MinValue で黙殺すると未推論銘柄が静かに最下位化し A/B 比較が無言で壊れる）。
            if (useMl && rows.Any(r => r.MlScore is null))
                throw new InvalidOperationException(
                    $"{t}: MlScore 未設定の Passed 行があります。signals 未実行/期間不一致/Python 未書戻しの可能性。"
                    + " 正しい順序は signals → train → (evaluate) → backtest --use-ml です。");

            var picks = rows
                .OrderByDescending(r => useMl ? r.MlScore!.Value : r.RuleScore)
                .ThenByDescending(r => r.RuleScore) // ML時のみ意味を持つ第2キー（非ML時は第1キーと同値で無害）
                .ThenBy(r => r.Code)
                .Take(_opt.TopN)
                .ToList();

            foreach (var pick in picks)
            {
                var trade = await SimulateTradeAsync(pick.Code, t, ct).ConfigureAwait(false);
                if (trade != null) results.Add(trade);
            }
        }

        run.TradeCount = results.Count;
        run.WinRate = results.Count == 0 ? 0 : (double)results.Count(r => r.Return > 0) / results.Count;
        run.AvgReturn = results.Count == 0 ? 0 : results.Average(r => r.Return);
        run.Results = results;

        _db.BacktestRuns.Add(run);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);

        _logger.LogInformation("Backtest {Label}: trades={Trades}, winRate={Win:P1}, avgReturn={Avg:P2}",
            label ?? "(no label)", run.TradeCount, run.WinRate, run.AvgReturn);
        if (missingDays > 0)
            _logger.LogWarning("Backtest {Label}: signals 未生成リバランス日 {Missing}/{Total}（signals 未実行/期間不一致の可能性）",
                label ?? "(no label)", missingDays, rebCount);
        return run;
    }

    /// <summary>
    /// 指定ウィンドウの取引日（昇順 distinct）を返す。「DailyBar 存在日＝取引日の代理」という
    /// ドメイン規約を <see cref="EnumerateRebalanceDays"/> と対で1箇所に閉じ込め、signals 経路
    /// （<c>GenerateSignalsForWindowAsync</c>）と backtest 経路の母数起点を共通化する
    /// （片方の述語/順序変更で無言にズレるのを防ぐ）。
    /// <para>
    /// なお <c>IngestService.TradingDaysAsync</c>（TradingCalendar の HolidayDivision 由来、未取得時は
    /// 平日フォールバック）とは別定義。あちらは ingest 対象日の入力で、こちらは実 bar 存在日＝
    /// backtest/signals の母数。欠損に強い「実 bar 存在日」を母数に採るのは意図的な設計差で、統合しない。
    /// </para>
    /// </summary>
    public static async Task<List<DateOnly>> QueryTradingDaysAsync(
        AppDbContext db, DateOnly start, DateOnly end, CancellationToken ct = default)
    {
        return await db.DailyBars
            .Where(b => b.Date >= start && b.Date <= end)
            .Select(b => b.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// 取引日リスト（昇順 distinct）の先頭を起点に <paramref name="interval"/> 刻みで間引いて
    /// リバランス日を列挙する純粋関数（DB 非依存・回帰固定可能）。
    /// BacktestService と signals コマンドの両方がこれを使い、同一ウィンドウ・同一 interval なら
    /// 同一日付集合を返すことで ML 有無・C#/Python 間の母数一致を構造的に保証する。
    /// </summary>
    public static IEnumerable<DateOnly> EnumerateRebalanceDays(IReadOnlyList<DateOnly> tradingDays, int interval)
    {
        int step = Math.Max(1, interval);
        for (int di = 0; di < tradingDays.Count; di += step)
            yield return tradingDays[di];
    }

    /// <summary>1銘柄を翌営業日始値でエントリし、MaxHoldDays か ATR ストップで手仕舞いする。</summary>
    private async Task<BacktestResult?> SimulateTradeAsync(string code, DateOnly decisionDate, CancellationToken ct)
    {
        // 決定日「翌営業日以降」のバー（エントリ＝先頭、以降で手仕舞い）。
        var forward = await _db.DailyBars.AsNoTracking()
            .Where(b => b.Code == code && b.Date > decisionDate)
            .OrderBy(b => b.Date)
            .Take(_opt.MaxHoldDays + 1)
            .ToListAsync(ct).ConfigureAwait(false);

        if (forward.Count < 2) return null; // エントリ翌日以降のデータが無い

        var entryBar = forward[0];
        double entryPrice = entryBar.AdjOpen ?? entryBar.AdjClose ?? 0;
        if (entryPrice <= 0) return null;

        // ATR ストップ閾値（エントリ日までのバーから算定）。
        double? stopLevel = null;
        if (_opt.AtrStopMultiplier > 0)
        {
            var hist = await _db.DailyBars.AsNoTracking()
                .Where(b => b.Code == code && b.Date <= entryBar.Date)
                .OrderByDescending(b => b.Date)
                .Take(_opt.AtrPeriod * 3)
                .ToListAsync(ct).ConfigureAwait(false);
            hist.Reverse();
            var atr = ComputeLastAtr(hist, _opt.AtrPeriod);
            if (atr is double a) stopLevel = entryPrice - _opt.AtrStopMultiplier * a;
        }

        // 手仕舞いバー・理由・約定価格を純粋関数で決定（DB 非依存・回帰固定可能）。
        var (exitBar, exitReason, exitPrice) = ResolveExit(forward, entryPrice, stopLevel);

        double gross = exitPrice / entryPrice - 1.0;
        double cost = 2 * (_opt.CommissionRate + _opt.SlippageRate);
        double net = gross - cost;

        return new BacktestResult
        {
            Code = code,
            EntryDate = entryBar.Date,
            ExitDate = exitBar.Date,
            EntryPrice = entryPrice,
            ExitPrice = exitPrice,
            Return = net,
            ExitReason = exitReason,
        };
    }

    /// <summary>
    /// 手仕舞いバー・理由・約定価格を決める純粋関数（DB 非依存）。
    /// forward[0]=エントリバー、forward[1..]=手仕舞い候補（昇順）。
    /// stopLevel を割った（AdjLow≤stop）最初のバーで AtrStop。割らなければ末尾で MaxHoldDays。
    /// AtrStop の約定はギャップダウンを反映し Min(stopLevel, 当該バー始値)。
    /// 始値（AdjOpen）欠損時は stopLevel にフォールバック（AdjClose は寄り≠引けで歪むため使わない）。
    /// </summary>
    public static (DailyBar exitBar, string exitReason, double exitPrice) ResolveExit(
        IReadOnlyList<DailyBar> forward, double entryPrice, double? stopLevel)
    {
        var exitBar = forward[forward.Count - 1];
        string exitReason = "MaxHoldDays";
        for (int i = 1; i < forward.Count; i++)
        {
            var b = forward[i];
            exitBar = b;
            double low = b.AdjLow ?? b.AdjClose ?? entryPrice;
            if (stopLevel is double sl && low <= sl)
            {
                exitReason = "AtrStop";
                break;
            }
        }

        double exitPrice;
        if (exitReason == "AtrStop" && stopLevel is double s)
            exitPrice = exitBar.AdjOpen is double op && op <= s ? op : s; // ギャップダウンは寄り値で約定
        else
            exitPrice = exitBar.AdjClose ?? entryPrice;

        return (exitBar, exitReason, exitPrice);
    }

    private static double? ComputeLastAtr(IReadOnlyList<DailyBar> bars, int period)
    {
        var highs = new List<double>(bars.Count);
        var lows = new List<double>(bars.Count);
        var closes = new List<double>(bars.Count);
        foreach (var b in bars)
        {
            double c = b.AdjClose ?? 0;
            highs.Add(b.AdjHigh ?? c);
            lows.Add(b.AdjLow ?? c);
            closes.Add(c);
        }
        if (closes.Count <= period) return null;
        var atr = TechnicalIndicators.Atr(highs, lows, closes, period);
        return atr[atr.Length - 1];
    }
}
