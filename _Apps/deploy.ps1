# 配備先は丸ごと消してから publish する (-o は既存ファイルを消さず、旧ビルドの DLL が残るため)
# 出力リテラルは ASCII に限定する (Windows PowerShell 5.1 は BOM 無し .ps1 を cp932 解釈するため)
# 同じ理由で、日本語コメントの行末は ASCII で終える (全角文字の末尾バイトが cp932 の先行バイトだと改行を食う)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appDir   = Join-Path $repoRoot "_Tools\WhiteCopy"

# linked worktree から実行すると worktree 内の _Tools へ配備してしまう (D&D 先はメイン作業ツリーの _Tools\WhiteCopy 固定)
$gitDirs = @(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-dir --git-common-dir)
if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed: ExitCode=$LASTEXITCODE" }
if ($gitDirs[0] -ne $gitDirs[1]) {
    throw "Run deploy.ps1 from the main working tree, not a linked worktree: $repoRoot"
}

if (Test-Path $appDir) { Remove-Item $appDir -Recurse -Force }

& dotnet publish (Join-Path $PSScriptRoot "WhiteCopy.csproj") -c Release -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: ExitCode=$LASTEXITCODE" }

Write-Host "Deployed to $appDir"
