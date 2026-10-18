# ComicViewer の Release 版を、接続中の実機へアプリのデータ (DB) を残したまま入れる (adb install -r)
#   VS の実行ボタンはデプロイ時にアンインストールを挟むことがあり、そうなると AppDataDirectory の DB が消える (2026-10-16 実測)
#   adb install -r は上書きのみでアンインストールしないため、署名不一致なら DB を残したまま失敗する (exit 1)
#
# 実行例 (<repo> は実パスに置換。実機は USB か無線 adb で接続しておく):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File <repo>\_Apps\install-android.ps1
#
# 出力リテラルは ASCII に限定する (Windows PowerShell 5.1 は BOM 無し .ps1 を cp932 解釈するため)
# 同じ理由で、日本語コメントの行末は ASCII で終える (全角文字の末尾バイトが cp932 の先行バイトだと改行を食う)

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決 (カレントディレクトリに依存しない)。$PSScriptRoot=_Apps
$project    = Join-Path $PSScriptRoot "ComicViewer.csproj"
$publishDir = Join-Path $PSScriptRoot "bin\Release\net10.0-android\publish"

# adb は PATH に無いことが多いので、ANDROID_HOME と VS 既定の SDK も探す (先勝ち)
$adb = (Get-Command adb -ErrorAction SilentlyContinue).Source
if (-not $adb) {
    $adb = @(
        $(if ($env:ANDROID_HOME) { Join-Path $env:ANDROID_HOME "platform-tools\adb.exe" }),
        "${env:ProgramFiles(x86)}\Android\android-sdk\platform-tools\adb.exe"
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $adb) {
    throw "adb.exe not found (PATH / ANDROID_HOME / Program Files (x86)\Android\android-sdk)"
}

# ビルドに数分かかるので、実機未接続はその前に止める (exit 1)
$state = & $adb get-state
if ($LASTEXITCODE -ne 0 -or "$state" -ne "device") {
    throw "Device is not ready (state='$state'). See the adb output above for the reason."
}

& dotnet publish $project -c Release -f net10.0-android
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed: ExitCode=$LASTEXITCODE"
}

$apk = Get-ChildItem -Path $publishDir -Filter "*-Signed.apk" | Select-Object -First 1
if (-not $apk) {
    throw "Signed APK not found in $publishDir"
}

& $adb install -r $apk.FullName
if ($LASTEXITCODE -ne 0) {
    # アンインストールして入れ直すと DB が消えるため、自動では行わずに止める (exit 1)
    throw "adb install failed: ExitCode=$LASTEXITCODE. Do NOT uninstall the app (the DB would be lost)."
}
Write-Host "Installed $($apk.Name)"
