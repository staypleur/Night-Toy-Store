param([string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'Builds\NetworkTest-03\NightToyStore.exe' }
$testProcesses = @()
try {
    $hostLog = Join-Path $projectRoot 'Logs\two-host.log'
    $clientLog = Join-Path $projectRoot 'Logs\two-client.log'
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7789 -nts-host -nts-test -nts-two-test -logFile "' + $hostLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7789 -nts-client -nts-test -nts-two-test -logFile "' + $clientLog + '"') -WindowStyle Hidden -PassThru
    if (-not $testProcesses[0].WaitForExit(40000)) { throw 'Two-player role test timeout.' }
    if ($testProcesses[0].ExitCode -ne 0) { throw 'Two-player host failed.' }
    if (-not (Select-String -LiteralPath $hostLog -Pattern 'NTS_TWO_HOST_PASS' -Quiet)) { throw 'Host role test marker missing.' }
    if (-not (Select-String -LiteralPath $clientLog -Pattern 'NTS_TWO_CLIENT_PASS' -Quiet)) { throw 'Client role test marker missing.' }
    foreach ($role in 0,2,3) {
        if (-not (Select-String -LiteralPath $clientLog -Pattern "NTS_CAMERA_HEIGHT role=$role " -Quiet)) { throw "Role $role camera update missing." }
    }
    foreach ($log in $hostLog,$clientLog) {
        if (Select-String -LiteralPath $log -Pattern 'Exception:|Error:' -Quiet) { throw "Runtime error in $log" }
    }
    Write-Output 'PASS: two-player role selection, occupied-role swap, camera height updates and replication.'
} finally {
    foreach ($testProcess in $testProcesses) {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue }
    }
}
