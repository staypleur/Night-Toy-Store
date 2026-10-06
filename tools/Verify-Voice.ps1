param([string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) { $Executable = Join-Path $projectRoot 'Builds\NetworkTest-06\NightToyStore.exe' }
$testProcesses = @()
try {
    $hostLog = Join-Path $projectRoot 'Logs\voice-host.log'
    $clientLog = Join-Path $projectRoot 'Logs\voice-client.log'
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7793 -nts-host -nts-voice-test -logFile "' + $hostLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-port 7793 -nts-client -nts-voice-test -logFile "' + $clientLog + '"') -WindowStyle Hidden -PassThru
    if (-not $testProcesses[0].WaitForExit(35000)) { throw 'Voice test timeout.' }
    if ($testProcesses[0].ExitCode -ne 0) { throw 'Voice host failed.' }
    if (-not (Select-String -LiteralPath $hostLog -Pattern 'NTS_VOICE_PROBE_PASS' -Quiet)) { throw 'Voice success marker missing.' }
    foreach ($log in $hostLog,$clientLog) {
        if (Select-String -LiteralPath $log -Pattern 'Exception:|Error:' -Quiet) { throw "Runtime error in $log" }
    }
    Write-Output 'PASS: PCM round trip, network voice, grandmother own-voice wave, 3m walking pulse, rabbit voice rejection, cane cooldown. No microphone capture.'
} finally {
    foreach ($testProcess in $testProcesses) {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue }
    }
}
