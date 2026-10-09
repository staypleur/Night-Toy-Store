param([string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'Builds\NetworkTest-09\NightToyStore.exe' }
$testProcesses = @()
try {
    $hostLog = Join-Path $projectRoot 'Logs\jump-host.log'
    $clientLog = Join-Path $projectRoot 'Logs\jump-client.log'
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7796 -nts-host -nts-jump-test -logFile "' + $hostLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7796 -nts-client -nts-jump-test -logFile "' + $clientLog + '"') -WindowStyle Hidden -PassThru
    if (-not $testProcesses[0].WaitForExit(35000)) { throw 'Jump test timeout.' }
    if ($testProcesses[0].ExitCode -ne 0) { throw 'Jump host failed.' }
    if (-not (Select-String -LiteralPath $hostLog -Pattern 'NTS_JUMP_HOST_PASS' -Quiet)) { throw 'Jump success marker missing.' }
    if (-not (Select-String -LiteralPath $clientLog -Pattern 'NTS_JUMP_CLIENT_PASS' -Quiet)) { throw 'Remote jump/death observation marker missing.' }
    foreach ($log in $hostLog,$clientLog) {
        if (Select-String -LiteralPath $log -Pattern 'Exception:|Error:' -Quiet) { throw "Runtime error in $log" }
    }
    Write-Output 'PASS: three jumping roles, airborne rejection, ball immunity and instant capture, replicated jumps and death flags.'
} finally {
    foreach ($testProcess in $testProcesses) {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue }
    }
}
