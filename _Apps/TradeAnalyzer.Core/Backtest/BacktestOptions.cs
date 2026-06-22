namespace TradeAnalyzer.Core.Backtest;

/// <summary>
/// バックテストの実行パラメータ。撤退基準はデータから推定せず、実行前に明示設定する。
/// </summary>
public class BacktestOptions
{
    public const string SectionName = "BacktestOptions";

    /// <summary>リバランス間隔（営業日）。例: 5＝週次相当。</summary>
    public int RebalanceIntervalDays { get; set; } = 5;

    /// <summary>各リバランスで採用する上位銘柄数（RuleScore 降順）。</summary>
    public int TopN { get; set; } = 10;

    /// <summary>最大保有営業日数。これに達したら手仕舞い。</summary>
    public int MaxHoldDays { get; set; } = 20;

    /// <summary>ATR ストップ倍率（エントリ価格 − 倍率×ATR を割れたら損切り）。0以下で無効。</summary>
    public double AtrStopMultiplier { get; set; } = 2.0;

    /// <summary>ATR 算定期間。</summary>
    public int AtrPeriod { get; set; } = 14;

    /// <summary>片道手数料率（往復で2回計上）。</summary>
    public double CommissionRate { get; set; } = 0.0005;

    /// <summary>片道スリッページ率（往復で2回計上）。</summary>
    public double SlippageRate { get; set; } = 0.0005;
}
