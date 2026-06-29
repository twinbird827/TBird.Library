# 段階3a 当日 EOD 推論の日次オーケストレータ（Windows タスクスケジューラ登録対象）。
#
# 何をするか:
#   CWD を Worker プロジェクト dir に固定し `dotnet run -- run-today` を起動、stdout/stderr を
#   logs/run-today-<date>.log に追記し、アプリの終了コードをそのまま exit する。
#
# 前提（重要）:
#   - 事前に `dotnet run --project <Worker> -- migrate` 済みの trade.db が必要（コールドスタート＝
#     段階1/2 で複数年 ingest 済み）。run-today は当日 1 日分の bar しか足さない増分運用のため、
#     空/未マイグレーション DB だと ingest 内の ExecuteDeleteAsync が no such table で停止する。
#   - CWD 固定の理由: 接続文字列は相対 "Data Source=trade.db" で SQLite は CWD 基準で解決するため、
#     タスクスケジューラの不定 CWD のままだと空 DB を誤った場所に作る。Worker dir 固定で canonical な
#     _Apps/TradeAnalyzer.Worker/trade.db（段階1/2 の trade.db 置き場・train.py の慣行と一致）に解決する。
#   - 起動時刻は J-Quants Light の当日 EOD 反映後（19:00〜20:00 目安）。仕様で反映時刻を確認すること
#     （早すぎると当日 bar 未反映で採点対象 t が前営業日に落ちる＝アプリは警告のみでクラッシュしない）。
#
# タスクスケジューラ登録例（管理者 PowerShell。<repo> は実パスに置換。-File は絶対パス必須）:
#   schtasks /create /tn "TradeAnalyzer-RunToday" /sc daily /st 19:30 ^
#     /tr "powershell.exe -NoProfile -File <repo>\_Apps\scripts\run-today.ps1"
#   （ノートPCはタスクのプロパティで「スリープ解除して実行」を有効化。シャットダウン中は走らない＝本基盤の弱点。）
#
# 本番は `dotnet publish` 済み exe 直叩きに差し替え可（起動が速く dotnet SDK 不要。CWD 固定は同様に必要）。
# 将来 VPS/ミニPC へ移すときは本スクリプトを cron/systemd timer に置換するだけで C#/Python は不変。

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決（タスクスケジューラの作業ディレクトリに依存しない絶対パス化）。
$workerDir = (Resolve-Path (Join-Path $PSScriptRoot "..\TradeAnalyzer.Worker")).Path
$logDir = Join-Path $PSScriptRoot "logs"
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
$logFile = Join-Path $logDir ("run-today-{0}.log" -f (Get-Date -Format "yyyyMMdd"))

# CWD を Worker dir に固定（相対 trade.db / appsettings.json / MlDir(../ml) の解決基点）。
Set-Location $workerDir

"=== run-today 開始 {0} ===" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss") | Tee-Object -FilePath $logFile -Append
dotnet run --project $workerDir -- run-today *>&1 | Tee-Object -FilePath $logFile -Append
$code = $LASTEXITCODE
"=== run-today 終了 ExitCode={0} ({1}) ===" -f $code, (Get-Date -Format "yyyy-MM-dd HH:mm:ss") | Tee-Object -FilePath $logFile -Append

exit $code
