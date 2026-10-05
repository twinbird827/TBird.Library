# TradeAnalyzer のタスク用実行物を _Tools/TradeAnalyzer/app/ へ配備する (タスクスケジューラはここから起動する)
#   app/bin   : dotnet publish -c Release の出力 (TradeAnalyzer.Worker.exe)
#   app/ml    : _Apps/ml の Python スクリプト + uv sync --frozen で作った .venv
#   app/*.ps1 : _Apps/scripts のタスク用スクリプト
# app/ は配備のたびに丸ごと入れ替える。_Tools/TradeAnalyzer 直下の実行時データ (trade.db / Secrets.json / logs / ml/models) には触れない
# 配備後に配備した exe で migrate を実行する (適用済みの migration は再適用されない)
# ブランチ切替では配備しない (app-trade-analyzer 系ブランチのメイン作業ツリーから明示的に実行する)
#
# 管理者権限は不要。実行例 (<repo> は実パスに置換):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File <repo>\_Apps\deploy.ps1

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決 (カレントディレクトリに依存しない)。$PSScriptRoot=_Apps
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dataRoot = Join-Path $repoRoot "_Tools\TradeAnalyzer"
$appDir   = Join-Path $dataRoot "app"
$binDir   = Join-Path $appDir "bin"
$mlDir    = Join-Path $appDir "ml"

# linked worktree から実行すると worktree 内の _Tools へ配備してしまう (タスクの Action はメイン作業ツリー固定)
$gitDirs = @(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-dir --git-common-dir)
if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed: ExitCode=$LASTEXITCODE" }
if ($gitDirs[0] -ne $gitDirs[1]) {
    throw "Run deploy.ps1 from the main working tree, not a linked worktree: $repoRoot"
}

# 実行中のタスクがあれば止める (exe / dll / .venv のロックで app/ の削除が途中で失敗するため)
$running = @(Get-ScheduledTask -TaskName "TradeAnalyzer-*" -ErrorAction SilentlyContinue | Where-Object { $_.State -eq "Running" })
if ($running.Count -gt 0) {
    throw ("Scheduled task is running: {0}" -f (($running | ForEach-Object { $_.TaskName }) -join ", "))
}

if (Test-Path $appDir) {
    Remove-Item -Path $appDir -Recurse -Force
}

# -o は Directory.Build.props の ArtifactsPath より優先される (開発ビルドの _Tools/TradeAnalyzer/{bin,obj,publish} とは別パス)
& dotnet publish (Join-Path $PSScriptRoot "TradeAnalyzer.Worker\TradeAnalyzer.Worker.csproj") -c Release -o $binDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: ExitCode=$LASTEXITCODE" }

# 追跡ファイルだけを写す (.venv / __pycache__ / .pytest_cache は写さない)
New-Item -ItemType Directory -Force -Path $mlDir | Out-Null
Copy-Item -Path (Join-Path $PSScriptRoot "ml\*") -Include "*.py", "pyproject.toml", "uv.lock" -Destination $mlDir
# uv run と同じ既定 (dev グループ込み) で同期しておく (初回のタスク実行で再同期を走らせないため)
& uv sync --frozen --directory $mlDir
if ($LASTEXITCODE -ne 0) { throw "uv sync failed: ExitCode=$LASTEXITCODE" }

Copy-Item -Path (Join-Path $PSScriptRoot "scripts\*.ps1") -Destination $appDir

# CWD を bin にする (appsettings.json は ContentRoot=CWD 基準で読まれるため)
$env:TRADEANALYZER_DATA_DIR = $dataRoot
Set-Location $binDir
& (Join-Path $binDir "TradeAnalyzer.Worker.exe") migrate
if ($LASTEXITCODE -ne 0) { throw "migrate failed: ExitCode=$LASTEXITCODE" }

Write-Host "Deployed to $appDir"
