# 段階3a 再学習ラッパ（タスクスケジューラ登録対象）。推論(run-today=日次)とは別タスクで登録する。
#   週次  : モデル更新（best_params.json を再利用＝optuna は回さない）
#   月次  : -Retune でハイパラ再探索（optuna）→ best_params.json 更新
#
# train-end は「昨日(暦日)」を渡す。train.py --full は LabelConfirmed で直近 H 日を自動除外するため、
# train-end が確定ラベル境界以降なら確定行集合は train-end の選び方に依らず同一。昨日を使うことで
# 当日採点との先読み防止規約 (train-end < 採点日) を常に満たす。
# 本タスクは日次 run-today(22:30)より後(23:00以降)に走らせること（当日採点に使うモデルは前日以前＝先読みなし）。
#
# 前提: uv が PATH 上にあること（ログオンユーザーで実行）。models/ への書込みは _Apps/ml/ 内（gitignore 済み）。
#
# タスクスケジューラ登録例（管理者不要 PowerShell。<repo> は実パスに置換。-File は絶対パス必須）:
#   週次: schtasks /Create /TN "TradeAnalyzer-RetrainWeekly" /SC WEEKLY /D SUN /ST 23:00 `
#           /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File <repo>\_Apps\scripts\retrain.ps1" /F
#   月次: schtasks /Create /TN "TradeAnalyzer-RetrainMonthly" /SC MONTHLY /D 1 /ST 23:30 `
#           /TR "powershell.exe -NoProfile -ExecutionPolicy Bypass -File <repo>\_Apps\scripts\retrain.ps1 -Retune" /F

param([switch]$Retune)

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決（タスクスケジューラの不定 CWD に依存しない）。
$mlDir  = (Resolve-Path (Join-Path $PSScriptRoot "..\ml")).Path
$dbPath = (Resolve-Path (Join-Path $mlDir "..\TradeAnalyzer.Worker\trade.db")).Path
$logDir = Join-Path $PSScriptRoot "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir ("retrain-{0}.log" -f (Get-Date -Format "yyyyMMdd"))

# UTF-8 統一（run-today.ps1 と同様。ヘッダ/フッタは ASCII 限定＝PS5.1 の .ps1 ANSI 解釈対策）。
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8
function Write-Log([string]$msg) { Write-Host $msg; $msg | Out-File -FilePath $logFile -Append -Encoding utf8 }

# train.py が models/ を解決し相対参照も効くよう CWD を ml dir に固定。
Set-Location $mlDir
$env:PYTHONUTF8 = "1"

$trainEnd = (Get-Date).AddDays(-1).ToString("yyyy-MM-dd")
$mode = if ($Retune) { "monthly(--retune)" } else { "weekly" }

$cmd = @("run", "python", "train.py", "--full", "--db", $dbPath, "--train-end", $trainEnd)
if ($Retune) { $cmd += "--retune" }

Write-Log ("=== retrain START {0} mode={1} train-end={2} ===" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"), $mode, $trainEnd)
& uv @cmd 2>&1 | ForEach-Object {
    $line = if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { [string]$_ }
    Write-Log $line
}
$code = $LASTEXITCODE
Write-Log ("=== retrain END ExitCode={0} ({1}) ===" -f $code, (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

exit $code
