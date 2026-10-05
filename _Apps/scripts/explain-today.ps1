# 段階3b 当日定性層の日次オーケストレータ（run-today の後に非致命で走らせる）。
#
# 何をするか:
#   配備先の TradeAnalyzer.Worker.exe explain-today を CWD=bin で起動し、stdout/stderr を
#   logs/explain-today-<date>.log に追記する。Claude 実行時失敗（per-銘柄スキップ）は C# 側が ExitCode=0 で
#   表現済み＝非致命。C# が ExitCode=1 を返すのは設定エラー/取引日皆無など真に起動不能な条件のみで、
#   それはタスクの前回結果として可視化する（exit $code。握り潰すと QualitativeJson が永久に埋まらないのに
#   タスクが緑のままになる）。
#
# 前提（重要）:
#   - 本スクリプトは _Apps/deploy.ps1 が _Tools/TradeAnalyzer/app/ へ配備したものを実行する（$PSScriptRoot=app）。
#     環境変数 TRADEANALYZER_DATA_DIR / Python__MlDir と CWD=bin の理由は run-today.ps1 と同じ。
#   - run-today が当日 Top-K（MlScore）を確定した「後」に走らせる（explain-today は Top-K を読むだけ）。
#   - migrate は _Apps/deploy.ps1 が配備時に実行する。スキーマ変更（migration 追加。QualitativeJson 列など）を
#     含む更新の取込後は deploy.ps1 で配備し直すこと (未 migrate だと Signals 読取が no such column で ExitCode=1)
#   - 認証: `claude login` した「同一ユーザアカウント」でタスクを走らせる（無人運用の最大の弱点＝設計）。
#     別アカウント/SYSTEM だと認証が無く全銘柄スキップ（ML のみ・非致命）。
#   - 実行ファイル解決（Windows）: Claude:ExecutablePath は既定 claude.cmd（npm シム。UseShellExecute=false 下で
#     .cmd 拡張子必須）。実体名/パスが異なる場合は絶対パスを appsettings で設定。誤設定/未解決だと起動失敗で
#     全銘柄スキップ（非致命・銘柄ごと LogWarning）＝Claude 層が無言に近い形で無効化される。初回導入時に
#     explain-today を1回手動実行して QualitativeJson が埋まることを確認すること。
#
# タスクスケジューラ登録例（run-today の数分後にトリガ。<repo> は実パスに置換）:
#   schtasks /create /tn "TradeAnalyzer-ExplainToday" /sc daily /st 19:40 ^
#     /tr "powershell.exe -NoProfile -File <repo>\_Tools\TradeAnalyzer\app\explain-today.ps1"

# Claude 障害（per-銘柄スキップ）は C# が exit 0 で表現済み。ここで止めない（run-today と独立）。
$ErrorActionPreference = "Continue"

$binDir = Join-Path $PSScriptRoot "bin"
$dataRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$logDir = Join-Path $dataRoot "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir ("explain-today-{0}.log" -f (Get-Date -Format "yyyyMMdd"))

$env:TRADEANALYZER_DATA_DIR = $dataRoot
$env:Python__MlDir = Join-Path $PSScriptRoot "ml"
Set-Location $binDir

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# ヘッダ/フッタの出力リテラルは ASCII に限定する（Windows PowerShell 5.1 は BOM 無し .ps1 を cp932 解釈するため）。
function Write-Log([string]$msg) { Write-Host $msg; $msg | Out-File -FilePath $logFile -Append -Encoding utf8 }

Write-Log ("=== explain-today START {0} ===" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
& (Join-Path $binDir "TradeAnalyzer.Worker.exe") explain-today 2>&1 | ForEach-Object {
    $line = if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { [string]$_ }
    Write-Log $line
}
$code = $LASTEXITCODE
# exe が起動しなかった場合（未配備・配備途中で bin が無い等）は $LASTEXITCODE が未設定（$null）のまま
# 流れ、exit $null = exit 0 に化けてタスクが緑のまま QualitativeJson が永久に埋まらない。$null は 1 へ倒す
# （ErrorActionPreference=Continue の本スクリプト固有の穴。run-today.ps1 は Stop のため -File が exit 1 を返す）。
if ($null -eq $code) {
    Write-Log "=== explain-today FAILED: TradeAnalyzer.Worker.exe did not start (run _Apps\deploy.ps1) ==="
    exit 1
}
Write-Log ("=== explain-today END ExitCode={0} ({1}) ===" -f $code, (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

# ExitCode をそのまま返す（run-today.ps1 と同じ）: 非致命スキップは C# が既に 0 で表現済みのため丸め不要。
# ExitCode=1（config/data の起動不能）を 0 に丸めるとスケジューラから真の障害が見えなくなる。
exit $code
