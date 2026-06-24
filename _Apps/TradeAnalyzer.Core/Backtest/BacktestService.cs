using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeAnalyzer.Core.Indicators;
using TradeAnalyzer.Core.Rules;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Core.Backtest;

/// <summary>
/// 先読み防止・サバイバーシップ対策を組み込んだウォークフォワード・バックテスト土台。
/// - リバランス日 t の銘柄選定は <see cref="RuleEngine"/>（Date≤t / DiscloseDate≤t のみ）で行う。
/// - エントリは t の翌営業日始値（AdjOpen、無ければ AdjClose）。先読みにならない。
/// - 撤退は MaxHoldDays か ATR ストップ。リターンは調整後価格・手数料/スリッページ込み。
/// 段階1は単純集計（勝率・平均リターン）。Rank-IC/NDCG は段階2で追加。
/// </summary>
public class BacktestService
{
    private readonly AppDbContext _db;
    private readonly RuleEngine _rules;
    private readonly BacktestOptions _opt;
    private readonly ILogger<BacktestService> _logger;

    public BacktestService(
        AppDbContext db, RuleEngine rules, IOptions<BacktestOptions> opt, ILogger<BacktestService> logger)
    {
        _db = db;
        _rules = rules;
        _opt = opt.Value;
        _logger = logger;
    }

    /// <summary>OOS 期間でバックテストを実行し、結果を永続化して返す。</summary>
    public async Task<BacktestRun> RunAsync(
        DateOnly isStart, DateOnly isEnd, DateOnly oosStart, DateOnly oosEnd,
        string? label = null, CancellationToken ct = default)
    {
        // 営業日は DailyBar が存在する日付で代理（TradingCalendar より欠損に強い）。
        var tradingDays = await _db.DailyBars
            .Where(b => b.Date >= oosStart && b.Date <= oosEnd)
            .Select(b => b.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct).ConfigureAwait(false);

        var run = new BacktestRun
        {
            Label = label,
            InSampleStart = isStart, InSampleEnd = isEnd,
            OutSampleStart = oosStart, OutSampleEnd = oosEnd,
            OptionsJson = JsonSerializer.Serialize(_opt),
        };

        if (tradingDays.Count == 0)
        {
            _logger.LogWarning("OOS 期間に取引日がありません。ingest 済みか確認してください。");
            _db.BacktestRuns.Add(run);
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            return run;
        }

        var results = new List<BacktestResult>();
        bool useMl = _opt.UseMlScore;

        // リバランス日は signals コマンドと共通の純粋関数で列挙し、ML 有無で母数を一致させる。
        foreach (var t in EnumerateRebalanceDays(tradingDays, _opt.RebalanceIntervalDays))
        {
            ct.ThrowIfCancellationRequested();

            // 非ML時: RuleEngine をリバランス日 t で評価。
            // ML時:   signals が保存した Signal（MlScore 入り）を読む（再評価せず保存済み参照）。
            var rows = useMl
                ? await _db.Signals.AsNoTracking().Where(s => s.Date == t && s.Passed).ToListAsync(ct).ConfigureAwait(false)
                : (await _rules.EvaluateAsync(t, ct).ConfigureAwait(false)).Where(s => s.Passed).ToList();

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
        return run;
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
