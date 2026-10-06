param([string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'Builds\NetworkTest-03\NightToyStore.exe' }
$testProcesses = @()
try {
    $hostLog = Join-Path $projectRoot 'Logs\physics-host.log'
    $clientLog = Join-Path $projectRoot 'Logs\physics-client.log'
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7792 -nts-host -nts-physics-test -logFile "' + $hostLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7792 -nts-client -nts-physics-test -logFile "' + $clientLog + '"') -WindowStyle Hidden -PassThru
    if (-not $testProcesses[0].WaitForExit(35000)) { throw 'Physics test timeout.' }
    if ($testProcesses[0].ExitCode -ne 0) { throw 'Physics host failed.' }
    if (-not (Select-String -LiteralPath $hostLog -Pattern 'NTS_PHYSICS_PROBE_PASS' -Quiet)) { throw 'Physics success marker missing.' }
    foreach ($log in $hostLog,$clientLog) {
        if (Select-String -LiteralPath $log -Pattern 'Exception:|Error:' -Quiet) { throw "Runtime error in $log" }
    }
    Write-Output 'PASS: body-contact-only push, proportional wall rebounds at 2/6/14 speeds, camera yaw/pitch and limit.'
} finally {
    foreach ($testProcess in $testProcesses) {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue }
    }
}
