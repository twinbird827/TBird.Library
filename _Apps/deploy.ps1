# 配備先の旧ファイルを消してから publish する (-o は既存ファイルを消さず、旧ビルドの DLL が残るため)
# 出力リテラルは ASCII に限定する (Windows PowerShell 5.1 は BOM 無し .ps1 を cp932 解釈するため)
# 同じ理由で、日本語コメントの行末は ASCII で終える (全角文字の末尾バイトが cp932 の先行バイトだと改行を食う)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appDir   = Join-Path $repoRoot "_Tools\EBook2PDF"

# linked worktree から実行すると worktree 内の _Tools へ配備してしまう (D&D 先はメイン作業ツリーの _Tools\EBook2PDF 固定)
$gitDirs = @(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-dir --git-common-dir)
if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed: ExitCode=$LASTEXITCODE" }
if ($gitDirs[0] -ne $gitDirs[1]) {
    throw "Run deploy.ps1 from the main working tree, not a linked worktree: $repoRoot"
}

# 配備先に実行時データが同居しているため、これだけは消さずに残す (D&D 先と PDF2JPG の相対パスを変えないため)
$keep = @("app-setting.json", "log")
if (Test-Path $appDir) {
    Get-ChildItem $appDir -Force | Where-Object { $keep -notcontains $_.Name } | Remove-Item -Recurse -Force
}

& dotnet publish (Join-Path $PSScriptRoot "EBook2PDF.csproj") -c Release -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: ExitCode=$LASTEXITCODE" }

Write-Host "Deployed to $appDir"
