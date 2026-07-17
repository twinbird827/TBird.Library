# 段階3b 当日定性層の日次オーケストレータ（run-today の後に非致命で走らせる）。
#
# 何をするか:
#   CWD を Worker プロジェクト dir に固定し `dotnet run -- explain-today` を起動、stdout/stderr を
#   logs/explain-today-<date>.log に追記する。Claude 実行時失敗（per-銘柄スキップ）は C# 側が ExitCode=0 で
#   表現済み＝非致命。C# が ExitCode=1 を返すのは設定エラー/取引日皆無など真に起動不能な条件のみで、
#   それはタスクの前回結果として可視化する（exit $code。握り潰すと QualitativeJson が永久に埋まらないのに
#   タスクが緑のままになる）。
#
# 前提（重要）:
#   - run-today が当日 Top-K（MlScore）を確定した「後」に走らせる（explain-today は Top-K を読むだけ）。
#   - スキーマ変更（migration 追加。QualitativeJson 列など）を含む更新の取込後は migrate を再実行すること
#     （未 migrate だと Signals 読取が no such column で ExitCode=1）。
#   - 認証: `claude login` した「同一ユーザアカウント」でタスクを走らせる（無人運用の最大の弱点＝設計）。
#     別アカウント/SYSTEM だと認証が無く全銘柄スキップ（ML のみ・非致命）。
#   - 実行ファイル解決（Windows）: Claude:ExecutablePath は既定 claude.cmd（npm シム。UseShellExecute=false 下で
#     .cmd 拡張子必須）。実体名/パスが異なる場合は絶対パスを appsettings で設定。誤設定/未解決だと起動失敗で
#     全銘柄スキップ（非致命・銘柄ごと LogWarning）＝Claude 層が無言に近い形で無効化される。初回導入時に
#     explain-today を1回手動実行して QualitativeJson が埋まることを確認すること。
#
# タスクスケジューラ登録例（run-today の数分後にトリガ。<repo> は実パスに置換）:
#   schtasks /create /tn "TradeAnalyzer-ExplainToday" /sc daily /st 19:40 ^
#     /tr "powershell.exe -NoProfile -File <repo>\_Apps\scripts\explain-today.ps1"

# Claude 障害（per-銘柄スキップ）は C# が exit 0 で表現済み。ここで止めない（run-today と独立）。
$ErrorActionPreference = "Continue"

$workerDir = (Resolve-Path (Join-Path $PSScriptRoot "..\TradeAnalyzer.Worker")).Path
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$logDir = Join-Path $repoRoot "_Tools\TradeAnalyzer\logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir ("explain-today-{0}.log" -f (Get-Date -Format "yyyyMMdd"))

Set-Location $workerDir

[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# ヘッダ/フッタの出力リテラルは ASCII に限定する（Windows PowerShell 5.1 は BOM 無し .ps1 を cp932 解釈するため）。
function Write-Log([string]$msg) { Write-Host $msg; $msg | Out-File -FilePath $logFile -Append -Encoding utf8 }

Write-Log ("=== explain-today START {0} ===" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
dotnet run --project $workerDir -- explain-today 2>&1 | ForEach-Object {
    $line = if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { [string]$_ }
    Write-Log $line
}
$code = $LASTEXITCODE
Write-Log ("=== explain-today END ExitCode={0} ({1}) ===" -f $code, (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

# ExitCode をそのまま返す（run-today.ps1 と同じ）: 非致命スキップは C# が既に 0 で表現済みのため丸め不要。
# ExitCode=1（config/data の起動不能）を 0 に丸めるとスケジューラから真の障害が見えなくなる。
exit $code
