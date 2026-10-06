param([string]$Executable)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $Executable) {
    $Executable = Join-Path $projectRoot 'Builds\NetworkTest\NightToyStore.exe'
}
if (-not (Test-Path -LiteralPath $Executable)) { throw "Build not found: $Executable" }
$logsRoot = Join-Path $projectRoot 'Logs'
New-Item -ItemType Directory -Path $logsRoot -Force | Out-Null
$testProcesses = @()
try {
    $hostLog = Join-Path $logsRoot 'smoke-host.log'
    $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-host -nts-test -nts-full -logFile "' + $hostLog + '"') -WindowStyle Hidden -PassThru
    Start-Sleep -Seconds 3
    for ($index = 1; $index -le 3; $index++) {
        $clientLog = Join-Path $logsRoot "smoke-client-$index.log"
        $testProcesses += Start-Process -FilePath $Executable -ArgumentList ('-batchmode -nographics -nts-client -nts-test -logFile "' + $clientLog + '"') -WindowStyle Hidden -PassThru
        Start-Sleep -Milliseconds 800
    }
    $completed = $testProcesses[0].WaitForExit(45000)
    if (-not $completed) { throw 'Host test did not exit within 45 seconds.' }
    if ($testProcesses[0].ExitCode -ne 0) { throw "Host exit code: $($testProcesses[0].ExitCode)" }
    if (-not (Select-String -LiteralPath $hostLog -Pattern 'NTS_NETWORK_SMOKE_PASS' -Quiet)) {
        throw 'Host success marker missing.'
    }
    for ($index = 1; $index -le 3; $index++) {
        $clientLog = Join-Path $logsRoot "smoke-client-$index.log"
        if (-not (Select-String -LiteralPath $clientLog -Pattern 'NTS_CLIENT_REPLICATION_PASS' -Quiet)) {
            throw "Client $index did not confirm replicated movement and roles."
        }
    }
    Write-Output 'PASS: host + 3 clients, unique roles, remote input, replicated movement and ball physics.'
} finally {
    foreach ($testProcess in $testProcesses) {
        if (-not $testProcess.HasExited) { Stop-Process -Id $testProcess.Id -ErrorAction SilentlyContinue }
    }
}
