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
        int interval = Math.Max(1, _opt.RebalanceIntervalDays);

        for (int di = 0; di < tradingDays.Count; di += interval)
        {
            ct.ThrowIfCancellationRequested();
            var t = tradingDays[di];

            var signals = await _rules.EvaluateAsync(t, ct).ConfigureAwait(false);
            var picks = signals
                .Where(s => s.Passed)
                .OrderByDescending(s => s.RuleScore)
                .ThenBy(s => s.Code)
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

    /// <summary>1銘柄を翌営業日始値でエントリし、MaxHoldDays か ATR ストップで手仕舞いする。</summary>
    private async Task<BacktestResult?> SimulateTradeAsync(string code, DateOnly decisionDate, CancellationToken ct)
    {
        // 決定日「翌営業日以降」のバー（エントリ＝先頭、以降で手仕舞い）。
        var forward = await _db.DailyBars
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
            var hist = await _db.DailyBars
                .Where(b => b.Code == code && b.Date <= entryBar.Date)
                .OrderByDescending(b => b.Date)
                .Take(_opt.AtrPeriod * 3)
                .ToListAsync(ct).ConfigureAwait(false);
            hist.Reverse();
            var atr = ComputeLastAtr(hist, _opt.AtrPeriod);
            if (atr is double a) stopLevel = entryPrice - _opt.AtrStopMultiplier * a;
        }

        // 手仕舞い判定（エントリ翌バー以降）。
        var exitBar = forward[forward.Count - 1];
        string exitReason = "MaxHoldDays";
        for (int i = 1; i < forward.Count; i++)
        {
            var b = forward[i];
            double low = b.AdjLow ?? b.AdjClose ?? entryPrice;
            if (stopLevel is double sl && low <= sl)
            {
                exitBar = b;
                exitReason = "AtrStop";
                break;
            }
            exitBar = b;
        }

        double exitPrice = exitReason == "AtrStop" && stopLevel is double s
            ? s                                   // ストップ価格で約定（保守的）
            : (exitBar.AdjClose ?? entryPrice);

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
