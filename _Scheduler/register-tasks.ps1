# Netkeiba.Scheduler をタスクスケジューラへ登録する (同名タスクは上書き)
# 登録先の exe は _Apps\deploy.ps1 で配備した _Tools\Netkeiba\scheduler\Netkeiba.Scheduler.exe
#
# 実行例 (<repo> は実パスに置換):
#   powershell.exe -NoProfile -ExecutionPolicy Bypass -File <repo>\_Scheduler\register-tasks.ps1

$ErrorActionPreference = "Stop"

# スクリプト位置基準で解決 (カレントディレクトリに依存しない)。$PSScriptRoot=_Scheduler
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$schedulerDir = Join-Path $repoRoot "_Tools\Netkeiba\scheduler"
$exe = Join-Path $schedulerDir "Netkeiba.Scheduler.exe"

# linked worktree から実行すると worktree 内の _Tools を指すタスクになる (配備はメイン作業ツリーの _Tools\Netkeiba\scheduler 固定)
$gitDirs = @(& git -C $PSScriptRoot rev-parse --path-format=absolute --git-dir --git-common-dir)
if ($LASTEXITCODE -ne 0) { throw "git rev-parse failed: ExitCode=$LASTEXITCODE" }
if ($gitDirs[0] -ne $gitDirs[1]) {
    throw "Run register-tasks.ps1 from the main working tree, not a linked worktree: $repoRoot"
}

$settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries

$tasks = @(
    @{ Name = "Netkeiba weekly";  Argument = "weekly";  Trigger = (New-ScheduledTaskTrigger -Weekly -DaysOfWeek Friday -At "20:00") },
    @{ Name = "Netkeiba raceday"; Argument = "raceday"; Trigger = (New-ScheduledTaskTrigger -Daily -At "09:00") }
)

foreach ($task in $tasks) {
    $action = New-ScheduledTaskAction -Execute $exe -Argument $task.Argument -WorkingDirectory $schedulerDir
    Register-ScheduledTask -TaskName $task.Name -Action $action -Trigger $task.Trigger -Settings $settings -Force | Out-Null
    Write-Host "Registered: $($task.Name) -> $exe $($task.Argument)"
}
