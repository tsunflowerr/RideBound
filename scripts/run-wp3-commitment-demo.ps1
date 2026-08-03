[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$scenarioRoot = Join-Path $repositoryRoot "benchmarks/scenarios/wp3-commitment-tiny"
$inputPath = Join-Path $scenarioRoot "commitment-demo.input.ndjson"
$expectedHashesPath = Join-Path $scenarioRoot "commitment-demo.expected-decision-hashes.txt"
$expectedStateHashPath = Join-Path $scenarioRoot "commitment-demo.expected-final-state-hash.txt"
$configurationPath = Join-Path $repositoryRoot "benchmarks/configurations/wp3-boundary-test-v1.json"
$publishRoot = Join-Path $repositoryRoot "artifacts/wp3-commitment-demo-runner"
$isWindowsHost = $IsWindows -or $env:OS -eq "Windows_NT"
$osPrefix = if ($isWindowsHost) {
    "win"
}
elseif ($IsMacOS) {
    "osx"
}
else {
    "linux"
}
$architecture = (
    [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
).ToString().ToLowerInvariant()
$runtimeIdentifier = "$osPrefix-$architecture"
$runnerName = if ($isWindowsHost) {
    "RideBound.Runner.exe"
}
else {
    "RideBound.Runner"
}

dotnet publish `
    (Join-Path $repositoryRoot "src/RideBound.Runner/RideBound.Runner.csproj") `
    --configuration Release `
    --runtime $runtimeIdentifier `
    --self-contained `
    -p:PublishSingleFile=true `
    -p:ContinuousIntegrationBuild=true `
    --output $publishRoot

if ($LASTEXITCODE -ne 0) {
    throw "Runner publish failed with exit code $LASTEXITCODE."
}

$runnerPath = Join-Path $publishRoot $runnerName
$inputLines = Get-Content -LiteralPath $inputPath -Encoding utf8

function Invoke-RunnerReplay([string[]] $lines) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $runnerPath
    $escapedConfigurationPath = $configurationPath.Replace('"', '\"')
    $startInfo.Arguments =
        "--mode commitment --policy-config `"$escapedConfigurationPath`""
    $startInfo.RedirectStandardInput = $true
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.UseShellExecute = $false
    $startInfo.CreateNoWindow = $true

    $originalInputEncoding = [Console]::InputEncoding
    try {
        [Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
        $process = [System.Diagnostics.Process]::Start($startInfo)
    }
    finally {
        [Console]::InputEncoding = $originalInputEncoding
    }
    if ($null -eq $process) {
        throw "Runner process could not be started."
    }

    try {
        $inputBytes = [System.Text.UTF8Encoding]::new($false).GetBytes(
            ($lines -join "`n") + "`n")
        $process.StandardInput.BaseStream.Write(
            $inputBytes,
            0,
            $inputBytes.Length)
        $process.StandardInput.BaseStream.Flush()
        $process.StandardInput.BaseStream.Close()
        $stdout = $process.StandardOutput.ReadToEnd().Replace("`r`n", "`n")
        $stderr = $process.StandardError.ReadToEnd().Replace("`r`n", "`n")
        $process.WaitForExit()

        if ($process.ExitCode -ne 0) {
            throw "Runner replay failed with exit code $($process.ExitCode)."
        }

        if ($stderr -cne "") {
            throw "Runner replay wrote unexpected diagnostics: $stderr"
        }

        return $stdout
    }
    finally {
        $process.Dispose()
    }
}

$first = Invoke-RunnerReplay $inputLines
$second = Invoke-RunnerReplay $inputLines

if ($first -cne $second) {
    throw "WP3 commitment demo differs across two clean processes."
}

$messages = $first -split "`n" |
    Where-Object { $_ -ne "" } |
    ForEach-Object { $_ | ConvertFrom-Json }
$decisions = @($messages | Where-Object { $_.messageType -eq "decision" })
$expectedHashes = @(
    Get-Content -LiteralPath $expectedHashesPath -Encoding utf8 |
        Where-Object { $_ -ne "" }
)

if ($decisions.Count -ne 4) {
    throw "WP3 commitment demo must produce exactly four decisions."
}

for ($index = 0; $index -lt $decisions.Count; $index++) {
    $decision = $decisions[$index]

    if ($decision.payload.certificate.status -cne "produced" -or
        $decision.payload.certificate.body.normalOperation -ne $true -or
        $decision.payload.decisionHash -cne $expectedHashes[$index]) {
        throw "WP3 decision/certificate mismatch at epoch $($index + 1)."
    }
}

$published = @(
    $decisions |
        ForEach-Object { $_.payload.actions } |
        Where-Object { $_.decisionType -eq "promisePublished" }
)

if ($published.Count -ne 2 -or
    $published[0].payload.promiseVersion -ne 1 -or
    $published[1].payload.promiseVersion -ne 2 -or
    $published[1].payload.exogenousDelta.dropEtaTotalMs -ne 50 -or
    $published[1].payload.decisionDelta.dropEtaTotalMs -ne 0 -or
    $published[1].payload.budgetAfter.dropEtaTotalMs -ne 0) {
    throw "WP3 promise publication or three-way budget semantics changed."
}

$expectedStateHash = (
    Get-Content -Raw -LiteralPath $expectedStateHashPath -Encoding utf8
).Trim()

if ($decisions[-1].payload.stateAfterHash -cne $expectedStateHash) {
    throw "WP3 demo final state hash differs from the published hash."
}

$checkpointRequest =
    '{"schemaVersion":"1.0.0","messageType":"checkpoint",' +
    '"runId":"wp3-demo-run","scenarioId":"wp3-commitment-tiny",' +
    '"payload":{}}'
$prefixOutput = Invoke-RunnerReplay @(
    $inputLines[0..3]
    $checkpointRequest
    $inputLines[-1]
)
$checkpointMessages = @(
    $prefixOutput -split "`n" |
        Where-Object { $_ -ne "" } |
        ForEach-Object { $_ | ConvertFrom-Json } |
        Where-Object { $_.messageType -eq "checkpoint" }
)

if ($checkpointMessages.Count -ne 1) {
    throw "WP3 prefix replay must produce exactly one checkpoint."
}

$checkpoint = $checkpointMessages[0]
$checkpointPayload = $checkpoint.payload |
    ConvertTo-Json -Compress -Depth 100
$restoreRequest =
    '{"schemaVersion":"1.0.0","messageType":"restore",' +
    '"runId":"wp3-demo-run","scenarioId":"wp3-commitment-tiny",' +
    '"payload":' + $checkpointPayload + '}'
$restoredOutput = Invoke-RunnerReplay @(
    $inputLines[0..1]
    $restoreRequest
    $inputLines[4..($inputLines.Count - 1)]
)
$uninterruptedSuffix = @(
    $first -split "`n" |
        Where-Object { $_ -match '"messageType":"decision"' } |
        Select-Object -Skip 1
)
$restoredSuffix = @(
    $restoredOutput -split "`n" |
        Where-Object { $_ -match '"messageType":"decision"' }
)

if (($uninterruptedSuffix -join "`n") -cne ($restoredSuffix -join "`n")) {
    throw "Checkpoint restore suffix differs from genesis replay."
}

Write-Host "WP3 commitment demo PASS: two byte-exact clean-process replays and checkpoint restore."
Write-Host "Final decision hash: $($expectedHashes[-1])"
Write-Host "Final state hash: $expectedStateHash"
