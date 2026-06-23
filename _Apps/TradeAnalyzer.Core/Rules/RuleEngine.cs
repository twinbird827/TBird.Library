using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TradeAnalyzer.Core.Indicators;
using TradeAnalyzer.Data;
using TradeAnalyzer.Data.Entities;

namespace TradeAnalyzer.Core.Rules;

/// <summary>
/// 決定論的な足切り（ハードフィルタ）＋テクニカルゲートを評価する。
/// 先読み防止のため、判断は「基準日 asOf 以前に利用可能なデータのみ」で行う
/// （価格 Date≤asOf、財務 DiscloseDate≤asOf）。
/// Passed = 全ハードフィルタ通過 かつ トレンドゲート(SMA短>SMA長)通過。
/// RuleScore = 通過したテクニカルゲート数（0..3。段階2で ML スコアに置換）。
/// </summary>
public class RuleEngine
{
    private readonly AppDbContext _db;
    private readonly RuleOptions _opt;
    private readonly ILogger<RuleEngine> _logger;
    private bool _sizeFilterDegradeWarned;

    public RuleEngine(AppDbContext db, IOptions<RuleOptions> opt, ILogger<RuleEngine> logger)
    {
        _db = db;
        _opt = opt.Value;
        _logger = logger;
    }

    /// <summary>ポイントインタイム母集団の全銘柄を asOf 時点で評価する（DB アクセスあり）。</summary>
    public async Task<List<Signal>> EvaluateAsync(DateOnly asOf, CancellationToken ct = default)
    {
        // その時点で上場一覧に存在した銘柄（master AsOfDate≤asOf を持つ Code）。
        var codes = await _db.Stocks
            .Where(s => s.AsOfDate <= asOf)
            .Select(s => s.Code)
            .Distinct()
            .ToListAsync(ct).ConfigureAwait(false);

        var windowStart = asOf.AddYears(-2).AddDays(-30);
        var signals = new List<Signal>(codes.Count);

        // 規模フィルタの実効モードを決める（純粋性維持のため EvaluateStock へ引数で渡す）。
        // 判定は「FinSummary テーブルが全期間で空」（＝ J-Quants 財務の取得経路が無い ≒ --skip-jquants /
        // EDINET 単独）か否か。空なら ApproxMarketCap が全 null で規模フィルタが全却下するため、規模フィルタを
        // 実効的に無効化する（EDINET 財務からの近似配線は段階2）。
        // 注: 「DiscloseDate<=asOf の有無」では判定しない——通常モードでも開示が出揃う前の初期 asOf で空になり、
        //     バックテスト前半だけ規模フィルタが無効化される point-in-time 非一貫を生むため。
        var effectiveMode = _opt.MarketCapMode;
        if (_opt.MarketCapMode == MarketCapMode.Approximate
            && !await _db.FinSummaries.AsNoTracking().AnyAsync(ct).ConfigureAwait(false))
        {
            effectiveMode = MarketCapMode.Disabled;
            if (!_sizeFilterDegradeWarned)
            {
                _sizeFilterDegradeWarned = true; // RuleEngine は scoped＝バックテスト1回につき1回のみ警告。
                _logger.LogWarning(
                    "FinSummary が空のため規模フィルタを無効化します（--skip-jquants / EDINET 単独モード。"
                    + "EDINET 財務からの時価総額近似は段階2で配線）。");
            }
        }

        foreach (var code in codes)
        {
            ct.ThrowIfCancellationRequested();

            var bars = await _db.DailyBars.AsNoTracking()
                .Where(b => b.Code == code && b.Date <= asOf && b.Date >= windowStart)
                .OrderBy(b => b.Date)
                .ToListAsync(ct).ConfigureAwait(false);

            if (bars.Count == 0) continue;

            var fin = await _db.FinSummaries.AsNoTracking()
                .Where(f => f.Code == code && f.DiscloseDate <= asOf)
                .OrderByDescending(f => f.DiscloseDate)
                .FirstOrDefaultAsync(ct).ConfigureAwait(false);

            var signal = EvaluateStock(code, asOf, bars, fin, effectiveMode);
            if (signal != null) signals.Add(signal);
        }

        _logger.LogInformation("RuleEngine {AsOf}: {Total} 銘柄評価, {Passed} 通過",
            asOf, signals.Count, signals.Count(s => s.Passed));
        return signals;
    }

    /// <summary>
    /// 1銘柄を評価（純粋・テスト可能）。bars は昇順・Date≤asOf・連続営業日を前提。
    /// 規模フィルタは <paramref name="effectiveMode"/>（呼び出し側が asOf 時点で決定）に従う
    /// （_opt.MarketCapMode を直読みせず引数で受けることで純粋性を保つ）。
    /// </summary>
    public Signal EvaluateStock(
        string code, DateOnly asOf, IReadOnlyList<DailyBar> bars, FinSummary? latestFin, MarketCapMode effectiveMode)
    {
        var reasons = new List<string>();

        // 調整後終値・出来高・売買代金の系列（欠損は除外）。
        var adjCloses = new List<double>(bars.Count);
        var adjVols = new List<double>(bars.Count);
        var highs = new List<double>(bars.Count);
        var lows = new List<double>(bars.Count);
        foreach (var b in bars)
        {
            if (b.AdjClose is double c)
            {
                adjCloses.Add(c);
                adjVols.Add(b.AdjVolume ?? 0);
                highs.Add(b.AdjHigh ?? c);
                lows.Add(b.AdjLow ?? c);
            }
        }

        bool hardPass = true;

        // --- ハードフィルタ1: 上場期間代理（利用可能日足本数）---
        if (adjCloses.Count < _opt.MinAvailableBars)
        {
            hardPass = false;
            reasons.Add($"上場期間代理NG(bars={adjCloses.Count}<{_opt.MinAvailableBars})");
        }

        // --- ハードフィルタ2: 流動性（直近N日平均売買代金 AdjC×AdjVo）---
        double avgTurnover = AverageTurnover(adjCloses, adjVols, _opt.LiquidityDays);
        if (avgTurnover < _opt.MinTurnoverYen)
        {
            hardPass = false;
            reasons.Add($"流動性NG(avg={avgTurnover:F0}<{_opt.MinTurnoverYen:F0})");
        }

        // --- ハードフィルタ3: 規模（時価総額近似）---
        if (effectiveMode == MarketCapMode.Approximate)
        {
            double? cap = ApproxMarketCap(latestFin, adjCloses.Count > 0 ? adjCloses[adjCloses.Count - 1] : (double?)null);
            if (cap == null)
            {
                hardPass = false;
                reasons.Add("規模NG(時価総額近似不能: Eq/BPS欠損 or BPS<=0)");
            }
            else if (cap.Value < _opt.MinMarketCapYen)
            {
                hardPass = false;
                reasons.Add($"規模NG(cap≈{cap.Value:F0}<{_opt.MinMarketCapYen:F0})");
            }
        }

        if (!hardPass)
            return new Signal { Date = asOf, Code = code, Passed = false, RuleScore = 0, Rationale = string.Join("; ", reasons) };

        // --- テクニカルゲート ---
        int gatesPassed = 0;
        bool trendGate = false;

        var smaShort = TechnicalIndicators.Sma(adjCloses, _opt.SmaShort);
        var smaLong = TechnicalIndicators.Sma(adjCloses, _opt.SmaLong);
        int last = adjCloses.Count - 1;
        if (smaShort[last] is double ss && smaLong[last] is double sl)
        {
            trendGate = ss > sl;
            if (trendGate) { gatesPassed++; reasons.Add($"トレンドOK(SMA{_opt.SmaShort}>{_opt.SmaLong})"); }
            else reasons.Add($"トレンドNG(SMA{_opt.SmaShort}<=SMA{_opt.SmaLong})");
        }
        else reasons.Add("トレンド評価不能(SMAデータ不足)");

        var rsi = TechnicalIndicators.Rsi(adjCloses, _opt.RsiPeriod);
        if (rsi[last] is double r)
        {
            if (r >= _opt.RsiLow && r <= _opt.RsiHigh) { gatesPassed++; reasons.Add($"RSI OK({r:F1})"); }
            else reasons.Add($"RSI NG({r:F1} not in [{_opt.RsiLow},{_opt.RsiHigh}])");
        }
        else reasons.Add("RSI評価不能");

        // ベースラインから当日（末尾1本）を除外する。当日を含めると自分自身が基準を押し上げ、
        // フラット出来高では決して通過しない（VolumeSpikeMultiplier=1.0 で恒久 NG になる）。
        double volAvg = AverageTail(adjVols, _opt.VolumeAvgDays, endExclusive: 1);
        if (volAvg > 0 && adjVols.Count > 0)
        {
            double lastVol = adjVols[adjVols.Count - 1];
            if (lastVol > volAvg * _opt.VolumeSpikeMultiplier) { gatesPassed++; reasons.Add("出来高OK"); }
            else reasons.Add("出来高NG");
        }
        else reasons.Add("出来高評価不能");

        return new Signal
        {
            Date = asOf,
            Code = code,
            Passed = trendGate,            // ハードフィルタ通過済み + トレンドゲート
            RuleScore = gatesPassed,
            Rationale = string.Join("; ", reasons),
        };
    }

    /// <summary>時価総額近似 ≈ (Eq / BPS) × AdjC。BPS≤0 / 欠損時は近似不能で null。</summary>
    public static double? ApproxMarketCap(FinSummary? fin, double? adjClose)
    {
        if (fin?.Equity is not double eq || fin.Bps is not double bps || adjClose is not double px) return null;
        if (bps <= 0) return null;
        double shares = eq / bps;
        return shares * px;
    }

    private static double AverageTurnover(List<double> adjCloses, List<double> adjVols, int days)
    {
        int n = adjCloses.Count;
        if (n == 0) return 0;
        int start = Math.Max(0, n - days);
        double sum = 0;
        int cnt = 0;
        for (int i = start; i < n; i++) { sum += adjCloses[i] * adjVols[i]; cnt++; }
        return cnt == 0 ? 0 : sum / cnt;
    }

    /// <summary>
    /// 末尾 <paramref name="endExclusive"/> 本を除いた直近 <paramref name="days"/> 日平均。
    /// 窓は [max(0, end-days), end)（end = n-endExclusive）。窓が空なら 0。
    /// </summary>
    private static double AverageTail(List<double> values, int days, int endExclusive = 0)
    {
        int end = values.Count - endExclusive;
        if (end <= 0) return 0;
        int start = Math.Max(0, end - days);
        double sum = 0;
        int cnt = 0;
        for (int i = start; i < end; i++) { sum += values[i]; cnt++; }
        return cnt == 0 ? 0 : sum / cnt;
    }
}
