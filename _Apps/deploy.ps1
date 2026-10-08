# Netkeiba の実行物を _Tools\Netkeiba\app\ へ配備する (起動は _Tools\Netkeiba\app\Netkeiba.exe から)
# DB・モデル・lib\app-setting.json・ログは lib\path-setting.json の RootDirectory (_Tools\Netkeiba) 側にあり、配備では触れない
# ブランチ切替では配備しない (app-netkeiba のメイン作業ツリーから明示的に実行する)
#
# 実行例 (<repo> は実パスに置換):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File <repo>\_Apps\deploy.ps1

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決 (カレントディレクトリに依存しない)。$PSScriptRoot=_Apps
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$appDir   = Join-Path $repoRoot "_Tools\Netkeiba\app"

# linked worktree から実行すると worktree 内の _Tools へ配備してしまう (起動はメイン作業ツリーの _Tools\Netkeiba\app 固定)
$gitDirs = @(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-dir --git-common-dir)
if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed: ExitCode=$LASTEXITCODE" }
if ($gitDirs[0] -ne $gitDirs[1]) {
    throw "Run deploy.ps1 from the main working tree, not a linked worktree: $repoRoot"
}

& dotnet publish (Join-Path $PSScriptRoot "Netkeiba.csproj") -c Release -o $appDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: ExitCode=$LASTEXITCODE" }

Write-Host "Deployed to $appDir"
