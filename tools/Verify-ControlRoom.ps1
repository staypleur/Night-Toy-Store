param([string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'Builds\NetworkTest-09\NightToyStore.exe' }
$testProcesses = @()
try {
    $hostLog = Join-Path $projectRoot 'Logs\room-host.log'
    $clientLog = Join-Path $projectRoot 'Logs\room-client.log'
    $lateLog = Join-Path $projectRoot 'Logs\room-late.log'
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7798 -nts-host -nts-room-test -nts-seed 123 -logFile "' + $hostLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 2
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7798 -nts-client -nts-room-test -logFile "' + $clientLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 10
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7798 -nts-client -nts-room-test -nts-room-late -logFile "' + $lateLog + '"') -WindowStyle Hidden -PassThru
    if (-not $testProcesses[0].WaitForExit(35000)) { throw 'Control room test timeout.' }
    if ($testProcesses[0].ExitCode -ne 0) { throw 'Control room host failed.' }
    foreach ($entry in @(@($hostLog,'NTS_ROOM_HOST_PASS'),@($clientLog,'NTS_ROOM_CLIENT_PASS'),@($lateLog,'NTS_ROOM_LATE_PASS'))) {
        if (-not (Select-String -LiteralPath $entry[0] -Pattern $entry[1] -Quiet)) { throw ('Missing success marker in ' + $entry[0]) }
        if (Select-String -LiteralPath $entry[0] -Pattern 'Exception:|NTS_ROOM_FAIL' -Quiet) { throw ('Runtime error in ' + $entry[0]) }
    }
    Write-Output 'PASS: exact battery rates, independent first entry, client door/light RPC, seating, board replication and late join, ball restrictions.'
} finally {
    foreach ($testProcess in $testProcesses) { if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue } }
}
