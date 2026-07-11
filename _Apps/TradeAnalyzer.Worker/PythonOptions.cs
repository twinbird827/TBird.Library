namespace TradeAnalyzer.Worker;

/// <summary>
/// 段階3a の Python 採点プロセス起動設定（<c>uv run python predict.py …</c>）。
/// Python 関心は Worker 固有（Core/Data は Python を知らない）のため Worker プロジェクトに置く。
/// プロパティはプレーンに保ち、MlDir の絶対化は消費側（RunPythonAsync）で行う。TimeoutMinutes は非正値だと
/// ProcessRunner.RunAsync が ArgumentOutOfRangeException（fail-fast。黙示フォールバックはしない）。
/// </summary>
public class PythonOptions
{
    public const string SectionName = "Python";

    /// <summary>uv 実行ファイル。PATH 非依存にしたい場合は絶対パス指定可。</summary>
    public string UvPath { get; set; } = "uv";

    /// <summary>ml スクリプト群のディレクトリ。未指定（既定）は <c>AppPaths.MlScriptsDir</c>（<c>_Apps/ml</c> の
    /// 絶対パス・CWD 非依存）。相対指定した場合はリポジトリルート基準で絶対化する（消費側 RunPythonAsync）。</summary>
    public string? MlDir { get; set; }

    /// <summary>採点スクリプト名（既定 <c>predict.py</c>）。</summary>
    public string PredictScript { get; set; } = "predict.py";

    /// <summary>Python 実行のタイムアウト（分）。0 以下は ProcessRunner.RunAsync が
    /// ArgumentOutOfRangeException（fail-fast＝設定ミスの顕在化。黙示フォールバックはしない）。</summary>
    public int TimeoutMinutes { get; set; } = 10;
}
