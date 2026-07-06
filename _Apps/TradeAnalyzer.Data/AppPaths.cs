namespace TradeAnalyzer.Data;

/// <summary>
/// TradeAnalyzer の実行時状態（trade.db / Secrets.json / ML モデル / 実行ログ）の置き場を解決する。
///
/// 方針: これらは「git 追跡外だが実行に必要」なファイルで、_Apps 内に置くとブランチ切替や _Apps 内
/// TradeAnalyzer の一括削除で失われる。そこでリポジトリルート直下の <c>_Tools/TradeAnalyzer</c>
/// （.gitignore 済＝ブランチ切替でも消えない・全ブランチ共有）へ集約する。
///
/// ルートは実行ファイル位置（<see cref="AppContext.BaseDirectory"/>）から上位へたどり <c>_Apps</c> を持つ
/// ディレクトリとして求める（CWD 非依存）。これにより run-today（タスクスケジューラの不定 CWD）・
/// <c>dotnet run</c>・publish 済み exe のいずれから起動しても同一ファイルへ解決し、旧来の「CWD=Worker dir 前提」
/// の脆さを排除する。環境変数 <c>TRADEANALYZER_DATA_DIR</c> で DataRoot を明示上書きできる（リポ外配置向け）。
/// </summary>
public static class AppPaths
{
    /// <summary>実行時状態のルート環境変数名（設定時はこのパスを DataRoot として絶対化して使う）。</summary>
    public const string DataDirEnvVar = "TRADEANALYZER_DATA_DIR";

    /// <summary>リポジトリルート（<c>_Apps</c> を子に持つ最寄りの上位ディレクトリ）。</summary>
    public static string RepoRoot { get; } = ResolveRepoRoot();

    /// <summary>実行時状態のルート（<c>_Tools/TradeAnalyzer</c>。env で上書き可）。</summary>
    public static string DataRoot { get; } = ResolveDataRoot();

    /// <summary>SQLite 本体（<c>_Tools/TradeAnalyzer/trade.db</c>）。</summary>
    public static string TradeDbPath => Path.Combine(DataRoot, "trade.db");

    /// <summary>API キー（<c>_Tools/TradeAnalyzer/Secrets.json</c>）。</summary>
    public static string SecretsPath => Path.Combine(DataRoot, "Secrets.json");

    /// <summary>ML モデル/ハイパラ置き場（<c>_Tools/TradeAnalyzer/ml/models</c>。Python 側 train.py と一致）。</summary>
    public static string MlModelsDir => Path.Combine(DataRoot, "ml", "models");

    /// <summary>実行ログ置き場（<c>_Tools/TradeAnalyzer/logs</c>。run-today.ps1 / retrain.ps1 と一致）。</summary>
    public static string LogDir => Path.Combine(DataRoot, "logs");

    /// <summary>ML スクリプト群（<c>_Apps/ml</c>）。追跡対象ソースのため _Apps 側に置く（成果物は DataRoot）。</summary>
    public static string MlScriptsDir => Path.Combine(RepoRoot, "_Apps", "ml");

    /// <summary>SQLite 接続文字列（<c>Data Source=&lt;絶対パス&gt;</c>）。</summary>
    public static string TradeDbConnectionString => $"Data Source={TradeDbPath}";

    private static string ResolveRepoRoot()
    {
        // 実行ファイル位置から上位へ「_Apps を子に持つ」ディレクトリ（=リポジトリルート）を探索する。
        // artifacts output 有効時 BaseDirectory は _Tools/TradeAnalyzer/bin/<proj>/<cfg>/ 配下だが、
        // 上位に唯一 _Apps を持つのはルートのみのため一意に定まる。
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "_Apps")))
                return dir.FullName;
        }
        // フォールバック: リポ外（単体 publish 等）では実行ファイル隣を基点にする。
        return AppContext.BaseDirectory;
    }

    private static string ResolveDataRoot()
    {
        var overridePath = Environment.GetEnvironmentVariable(DataDirEnvVar);
        if (!string.IsNullOrWhiteSpace(overridePath))
            return Path.GetFullPath(overridePath);
        return Path.Combine(RepoRoot, "_Tools", "TradeAnalyzer");
    }
}
