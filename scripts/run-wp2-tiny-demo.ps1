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
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $runnerPath
    $startInfo.Arguments = "--mode online"
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
            ($inputLines -join "`n") + "`n")
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
