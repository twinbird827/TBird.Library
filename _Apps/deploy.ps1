# 中継サーバ（NewReleaseChecker.Relay）を _Tools/NewReleaseChecker/bin/ へ配備する。IIS の物理パスは bin/
#   秘密ファイルの正本 : _Tools/NewReleaseChecker/appsettings.Secrets.json (publish が配備物フォルダへ写す)
#   稼働中の上書き     : app_offline.htm を置いてアプリを止めてから入れ替え、成功したら消す (次の要求で起動する)
#
# 管理者権限は不要。実行例 (<repo> は実パスに置換):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File <repo>\_Apps\deploy.ps1
#
# 出力リテラルは ASCII に限定する (Windows PowerShell 5.1 は BOM 無し .ps1 を cp932 解釈するため)
# 同じ理由で、日本語コメントの行末は ASCII で終える (全角文字の末尾バイトが cp932 の先行バイトだと改行を食う)

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決 (カレントディレクトリに依存しない)。$PSScriptRoot=_Apps
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$project  = Join-Path $PSScriptRoot "NewReleaseChecker.Relay\NewReleaseChecker.Relay\NewReleaseChecker.Relay.csproj"
$toolsDir = Join-Path $repoRoot "_Tools\NewReleaseChecker"
$secrets  = Join-Path $toolsDir "appsettings.Secrets.json"
$binDir   = Join-Path $toolsDir "bin"
$offline  = Join-Path $binDir "app_offline.htm"

# 正本が無いまま配備すると IIS では起動失敗が 500 応答としてしか見えないため、publish 前に止める (exit 1)
if (-not (Test-Path $secrets)) {
    throw "Secrets file not found: $secrets"
}

# ANCM は app_offline.htm を検知して非同期に停止するので、DLL のロックが外れるまで待つ (5 sec)
if (Test-Path $binDir) {
    Set-Content -Path $offline -Value "<html><body>Updating...</body></html>" -Encoding ASCII
    Start-Sleep -Seconds 5
}

& dotnet publish $project -c Release -r win-x64 --self-contained false -o $binDir
if ($LASTEXITCODE -ne 0) {
    # app_offline.htm は残す (DLL が入れ替わりかけた状態でアプリを起動させない)
    throw "dotnet publish failed: ExitCode=$LASTEXITCODE"
}

# 削除に失敗したら Stop で例外にする (サイトが止まったまま成功表示で終わらせない)
if (Test-Path $offline) {
    Remove-Item -Path $offline
}
Write-Host "Deployed to $binDir"
