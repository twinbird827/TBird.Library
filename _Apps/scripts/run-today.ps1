# 段階3a 当日 EOD 推論の日次オーケストレータ（Windows タスクスケジューラ登録対象）。
#
# 何をするか:
#   配備先の TradeAnalyzer.Worker.exe run-today を CWD=bin で起動し、stdout/stderr を
#   logs/run-today-<date>.log に追記し、アプリの終了コードをそのまま exit する。
#
# 前提（重要）:
#   - 本スクリプトは _Apps/deploy.ps1 が _Tools/TradeAnalyzer/app/ へ配備したものを実行する（_Apps/scripts からの
#     直接実行は不可）。$PSScriptRoot=app、exe は app/bin、ML スクリプトは app/ml (_Apps の無いブランチでも動く)
#   - migrate は _Apps/deploy.ps1 が配備時に実行する（コールドスタート＝段階1/2 で複数年 ingest 済みの trade.db が
#     必要。run-today は当日 1 日分の bar しか足さない増分運用で、run-today 自身は migrate しない）。
#     スキーマ変更（migration 追加）を含む更新の取込後は deploy.ps1 で配備し直すこと (未 migrate だと no such column)
#   - trade.db / Secrets.json / ML モデル / logs は _Tools/TradeAnalyzer/ 直下に置き、本スクリプトが設定する
#     環境変数 TRADEANALYZER_DATA_DIR で C# の AppPaths と Python の train.py がそこへ解決する（_Apps の有無に依らない）。
#     ML スクリプト位置は環境変数 Python__MlDir (=app/ml) で appsettings の Python:MlDir を上書きして渡す。
#   - CWD 固定の理由: appsettings.json は ContentRoot(=CWD) 基準でロードされるため、bin 固定で publish 出力に
#     同梱された appsettings.json を確実に読ませる（DB 位置は CWD に依存しない）。
#   - 起動時刻は J-Quants Light の当日 EOD 反映後（19:00〜20:00 目安）。仕様で反映時刻を確認すること
#     （早すぎると当日 bar 未反映で採点対象 t が前営業日に落ちる＝アプリは警告のみでクラッシュしない）。
#
# タスクスケジューラ登録例（管理者 PowerShell。<repo> は実パスに置換。-File は絶対パス必須）:
#   schtasks /create /tn "TradeAnalyzer-RunToday" /sc daily /st 19:30 ^
#     /tr "powershell.exe -NoProfile -File <repo>\_Tools\TradeAnalyzer\app\run-today.ps1"
#   （ノートPCはタスクのプロパティで「スリープ解除して実行」を有効化。シャットダウン中は走らない＝本基盤の弱点。）
#
# 将来 VPS/ミニPC へ移すときは本スクリプトを cron/systemd timer に置換するだけで C#/Python は不変。

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決（タスクスケジューラの作業ディレクトリに依存しない絶対パス化）。
# $PSScriptRoot=_Tools/TradeAnalyzer/app のため bin が exe、.. が実行時データのルート (_Tools/TradeAnalyzer)
$binDir = Join-Path $PSScriptRoot "bin"
$dataRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$logDir = Join-Path $dataRoot "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir ("run-today-{0}.log" -f (Get-Date -Format "yyyyMMdd"))

# 子プロセス (exe -> uv -> python) へ継承させる。_Apps の無いブランチでも配備先と実行時データへ解決させるため
$env:TRADEANALYZER_DATA_DIR = $dataRoot
$env:Python__MlDir = Join-Path $PSScriptRoot "ml"
# CWD を bin に固定 (appsettings.json の解決基点)
Set-Location $binDir

# 子プロセス(exe)の UTF-8 stdout を正しく取り込み、ログも UTF-8 で書く（cp932 二重エンコードによる
# 日本語ログ文字化けの回避。Program.cs が Console.OutputEncoding=UTF8 を設定し、こちらは PS 側の
# 取り込み/書込みエンコーディングを揃える。Tee-Object は Windows PowerShell 5.1 で UTF-16 固定のため使わない）。
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$OutputEncoding = [System.Text.Encoding]::UTF8

# 1行ずつ console へ echo しつつ UTF-8 でログ追記する。
# 注: ヘッダ/フッタの出力リテラルは ASCII に限定する。Windows PowerShell 5.1 は BOM 無し .ps1 を
#     ANSI(cp932) として解釈し日本語リテラルを壊すため（C# 子プロセスの出力は実行時 UTF-8 取り込みで無事）。
function Write-Log([string]$msg) { Write-Host $msg; $msg | Out-File -FilePath $logFile -Append -Encoding utf8 }

Write-Log ("=== run-today START {0} ===" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
& (Join-Path $binDir "TradeAnalyzer.Worker.exe") run-today 2>&1 | ForEach-Object {
    $line = if ($_ -is [System.Management.Automation.ErrorRecord]) { $_.ToString() } else { [string]$_ }
    Write-Log $line
}
$code = $LASTEXITCODE
Write-Log ("=== run-today END ExitCode={0} ({1}) ===" -f $code, (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

exit $code
