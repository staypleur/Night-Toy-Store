param([string]$Executable, [int]$Seed = 12345, [switch]$SharedKeys)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'Builds\NetworkTest-08\NightToyStore.exe' }
$testProcesses = @()
$keyFlag = if ($SharedKeys) { ' -nts-shared-keys' } else { '' }
try {
    $hostLog = Join-Path $projectRoot 'Logs\fixed-store-host.log'
    $clientLog = Join-Path $projectRoot 'Logs\fixed-store-client.log'
    $secondLog = Join-Path $projectRoot 'Logs\fixed-store-second.log'
    $lateLog = Join-Path $projectRoot 'Logs\fixed-store-late.log'
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7797 -nts-host -nts-fixed-store-test -nts-seed ' + $Seed + $keyFlag + ' -logFile "' + $hostLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    foreach ($log in $clientLog,$secondLog) {
        $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7797 -nts-client -nts-fixed-store-test -logFile "' + $log + '"') -WindowStyle Hidden -PassThru
    }
    Start-Sleep -Seconds 5
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7797 -nts-client -nts-fixed-store-test -logFile "' + $lateLog + '"') -WindowStyle Hidden -PassThru
    if (-not $testProcesses[0].WaitForExit(35000)) { throw 'Store test timeout.' }
    if ($testProcesses[0].ExitCode -ne 0) { throw 'Store host failed.' }
    if (-not (Select-String -LiteralPath $hostLog -Pattern 'NTS_STORE_HOST_PASS' -Quiet)) { throw 'Host success marker missing.' }
    foreach ($log in $clientLog,$secondLog,$lateLog) {
        if (-not (Select-String -LiteralPath $log -Pattern 'NTS_STORE_CLIENT_PASS' -Quiet)) { throw "Client success marker missing: $log" }
    }
    foreach ($log in $hostLog,$clientLog,$secondLog,$lateLog) {
        if (Select-String -LiteralPath $log -Pattern 'Exception:|Error:' -Quiet) { throw "Runtime error in $log" }
    }
    Write-Output 'PASS: 30 layout seeds, fixed sketch geometry across seeds, connected rooms, reachable matching keys, arbitrary unlock order, shared starting room, ball clearance, four players, replicated doors/keys and late join.'
} finally {
    foreach ($testProcess in $testProcesses) {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue }
    }
}
