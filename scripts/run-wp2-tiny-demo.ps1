[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$scenarioRoot = Join-Path $repositoryRoot "benchmarks/scenarios/wp2-tiny"
$inputPath = Join-Path $scenarioRoot "online-demo.input.ndjson"
$expectedPath = Join-Path $scenarioRoot "online-demo.expected.ndjson"
$expectedHashPath = Join-Path $scenarioRoot "online-demo.expected-final-hash.txt"
$publishRoot = Join-Path $repositoryRoot "artifacts/wp2-tiny-demo-runner"
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
$expected = (Get-Content -Raw -LiteralPath $expectedPath -Encoding utf8).
    Replace("`r`n", "`n")

function Invoke-DemoReplay {
    $outputLines = $inputLines | & $runnerPath --mode online

    if ($LASTEXITCODE -ne 0) {
        throw "Runner replay failed with exit code $LASTEXITCODE."
    }

    return (($outputLines -join "`n") + "`n")
}

$first = Invoke-DemoReplay
$second = Invoke-DemoReplay

if ($first -cne $expected -or $second -cne $expected -or $first -cne $second) {
    throw "WP2 demo output differs from the source-controlled golden transcript."
}

$lastDecision = ($first -split "`n" |
        Where-Object { $_ -ne "" } |
        Select-Object -Last 1 |
        ConvertFrom-Json)
$expectedHash =
    (Get-Content -Raw -LiteralPath $expectedHashPath -Encoding utf8).Trim()

if ($lastDecision.payload.decisionHash -cne $expectedHash) {
    throw "WP2 demo final decision hash differs from the published hash."
}

Write-Host "WP2 tiny demo PASS: two byte-exact clean-process replays."
Write-Host "Final decision hash: $expectedHash"
