namespace TradeAnalyzer.Data.Entities;

/// <summary>
/// ルールエンジンが特定日に出力した銘柄シグナル。複合キー (Date, Code)。
/// 段階2では RuleScore を LambdaRank スコアに差し替える拡張点。
/// </summary>
public class Signal
{
    /// <summary>判断基準日（複合キーの一部）。</summary>
    public DateOnly Date { get; set; }

    /// <summary>銘柄コード（複合キーの一部）。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>全ハードフィルタを通過したか。</summary>
    public bool Passed { get; set; }

    /// <summary>通過したテクニカルゲート数の単純合算（段階2でML置換）。</summary>
    public double RuleScore { get; set; }

    /// <summary>判断根拠（通過/不通過の理由を human-readable に連結）。</summary>
    public string? Rationale { get; set; }

    /// <summary>ML 一次モデル（LambdaRank）の out-of-sample スコア。Python が書き戻す。未推論なら null。段階2で追加。</summary>
    public double? MlScore { get; set; }
}

/// <summary>バックテスト1回分の実行パラメータと集計結果。</summary>
public class BacktestRun
{
    /// <summary>サロゲートキー。</summary>
    public int Id { get; set; }

    /// <summary>実行ラベル（任意）。</summary>
    public string? Label { get; set; }

    /// <summary>IS（学習）期間開始。</summary>
    public DateOnly InSampleStart { get; set; }
    /// <summary>IS（学習）期間終了。</summary>
    public DateOnly InSampleEnd { get; set; }
    /// <summary>OOS（検証）期間開始。</summary>
    public DateOnly OutSampleStart { get; set; }
    /// <summary>OOS（検証）期間終了。</summary>
    public DateOnly OutSampleEnd { get; set; }

    /// <summary>適用したオプションの JSON スナップショット（再現性）。</summary>
    public string? OptionsJson { get; set; }

    /// <summary>勝率（リターン>0 のトレード割合）。</summary>
    public double WinRate { get; set; }
    /// <summary>平均リターン。</summary>
    public double AvgReturn { get; set; }
    /// <summary>トレード件数。</summary>
    public int TradeCount { get; set; }

    public ICollection<BacktestResult> Results { get; set; } = new List<BacktestResult>();
}

/// <summary>バックテストの個別トレード結果（1エントリ＝1保有）。</summary>
public class BacktestResult
{
    /// <summary>サロゲートキー。</summary>
    public long Id { get; set; }

    /// <summary>所属する実行。</summary>
    public int RunId { get; set; }
    public BacktestRun? Run { get; set; }

    /// <summary>銘柄コード。</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>エントリ日（リバランス日 t）。</summary>
    public DateOnly EntryDate { get; set; }
    /// <summary>エグジット日。</summary>
    public DateOnly ExitDate { get; set; }

    /// <summary>エントリ価格（調整後）。</summary>
    public double EntryPrice { get; set; }
    /// <summary>エグジット価格（調整後）。</summary>
    public double ExitPrice { get; set; }

    /// <summary>リターン（(Exit-Entry)/Entry − コスト）。</summary>
    public double Return { get; set; }

    /// <summary>エグジット理由（MaxHoldDays / AtrStop 等）。</summary>
    public string? ExitReason { get; set; }
}
