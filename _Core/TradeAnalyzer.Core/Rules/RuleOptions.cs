namespace TradeAnalyzer.Core.Rules;

/// <summary>時価総額フィルタの算定方式。</summary>
public enum MarketCapMode
{
    /// <summary>近似（発行済株数 ≈ Eq/BPS を株価に乗算）。Free データの制約下の既定。</summary>
    Approximate,
    /// <summary>規模フィルタを無効化（全銘柄通過扱い）。</summary>
    Disabled,
}

/// <summary>
/// ルールエンジンの閾値。すべて外出し（データから推定せず実行前に設定）。
/// Free データの制約（発行済株式数・上場日が無い）から、時価総額と上場期間は近似である点に注意。
/// </summary>
public class RuleOptions
{
    public const string SectionName = "RuleOptions";

    /// <summary>流動性: 平均売買代金を取る日数。</summary>
    public int LiquidityDays { get; set; } = 20;

    /// <summary>流動性: 平均売買代金（AdjC×AdjVo）の下限（円）。既定1億円。</summary>
    public double MinTurnoverYen { get; set; } = 100_000_000;

    /// <summary>規模: 時価総額の算定方式。</summary>
    public MarketCapMode MarketCapMode { get; set; } = MarketCapMode.Approximate;

    /// <summary>規模: 時価総額の下限（円）。既定300億円。</summary>
    public double MinMarketCapYen { get; set; } = 30_000_000_000;

    /// <summary>上場期間代理: 必要な利用可能日足数（連続営業日換算の最低本数）。</summary>
    public int MinAvailableBars { get; set; } = 400;

    /// <summary>テクニカル: 短期SMA期間。</summary>
    public int SmaShort { get; set; } = 25;
    /// <summary>テクニカル: 長期SMA期間。</summary>
    public int SmaLong { get; set; } = 75;

    /// <summary>テクニカル: RSI期間。</summary>
    public int RsiPeriod { get; set; } = 14;
    /// <summary>テクニカル: RSI下限。</summary>
    public double RsiLow { get; set; } = 40;
    /// <summary>テクニカル: RSI上限。</summary>
    public double RsiHigh { get; set; } = 70;

    /// <summary>テクニカル: 出来高が直近平均の何倍を超えれば通過とするか。</summary>
    public double VolumeSpikeMultiplier { get; set; } = 1.0;
    /// <summary>テクニカル: 出来高平均日数。</summary>
    public int VolumeAvgDays { get; set; } = 20;
}
