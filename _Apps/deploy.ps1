# 配備先の旧ファイルを消してから publish する (-o は既存ファイルを消さず、旧ビルドの DLL が残るため)
# 出力リテラルは ASCII に限定する (Windows PowerShell 5.1 は BOM 無し .ps1 を cp932 解釈するため)
# 同じ理由で、日本語コメントの行末は ASCII で終える (全角文字の末尾バイトが cp932 の先行バイトだと改行を食う)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appDir   = Join-Path $repoRoot "_Tools\Moviewer"

# linked worktree から実行すると worktree 内の _Tools へ配備してしまう (起動元はメイン作業ツリーの _Tools\Moviewer\Moviewer.exe 固定)
$gitDirs = @(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-dir --git-common-dir)
if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed: ExitCode=$LASTEXITCODE" }
if ($gitDirs[0] -ne $gitDirs[1]) {
    throw "Run deploy.ps1 from the main working tree, not a linked worktree: $repoRoot"
}

# 実行時データは消さずに残す (lib は設定 json と退避フォルダが同居し、log はアプリが日次ログを追記するため)
$keep = @("lib", "cache", "log")
if (Test-Path $appDir) {
    Get-ChildItem $appDir -Force | Where-Object { $keep -notcontains $_.Name } | Remove-Item -Recurse -Force
}

& dotnet publish (Join-Path $PSScriptRoot "Moviewer.csproj") -c Release -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: ExitCode=$LASTEXITCODE" }

Write-Host "Deployed to $appDir"
