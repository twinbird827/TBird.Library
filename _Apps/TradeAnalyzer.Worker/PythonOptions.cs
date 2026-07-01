namespace TradeAnalyzer.Worker;

/// <summary>
/// 段階3a の Python 採点プロセス起動設定（<c>uv run python predict.py …</c>）。
/// Python 関心は Worker 固有（Core/Data は Python を知らない）のため Worker プロジェクトに置く。
/// プロパティはプレーンに保ち、MlDir の絶対化・TimeoutMinutes のフォールバックは消費側（RunPythonAsync）で行う。
/// </summary>
public class PythonOptions
{
    public const string SectionName = "Python";

    /// <summary>uv 実行ファイル。PATH 非依存にしたい場合は絶対パス指定可。</summary>
    public string UvPath { get; set; } = "uv";

    /// <summary>ml スクリプト群のディレクトリ（相対は Worker CWD 基準。消費側で <c>Path.GetFullPath</c> 絶対化）。</summary>
    public string? MlDir { get; set; }

    /// <summary>採点スクリプト名（既定 <c>predict.py</c>）。</summary>
    public string PredictScript { get; set; } = "predict.py";

    /// <summary>Python 実行のタイムアウト（分）。0 以下は消費側で既定 10 にフォールバック。</summary>
    public int TimeoutMinutes { get; set; } = 10;
}
