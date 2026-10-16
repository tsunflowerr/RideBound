# Resource-only successor to the original launcher; same steering environment,
# interpreter and working directory. All new files/logs remain on C.
param(
    [Parameter(Mandatory=$true)][ValidateSet('selftest','run')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$Log,
    [Parameter(Mandatory=$true)][ValidatePattern('^[0-9a-f]{64}$')][string]$PolicyManifestSha256
)
$ErrorActionPreference='Stop'
$harness='E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness'
$python='E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe'
$policyRoot='C:\RideBoundContinuation\2026-10-15\resource30'
$prefixes=@('GIT_','PYTHON','DOTNET_','COREHOST_','COMPLUS_','OMP_','MKL_','OPENBLAS_','RB_')
foreach($item in @(Get-ChildItem Env:)) {
    foreach($prefix in $prefixes) {
        if($item.Name.ToUpperInvariant().StartsWith($prefix)) {
            Remove-Item -LiteralPath "Env:$($item.Name)"
            break
        }
    }
}
foreach($name in @('NoDefaultCurrentDirectoryInExePath','SETUPTOOLS_USE_DISTUTILS')) {
    if(Test-Path -LiteralPath "Env:$name") { Remove-Item -LiteralPath "Env:$name" }
}
$env:PATH='C:\Windows\system32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0\;C:\Program Files\dotnet\;C:\Program Files\Git\cmd'
$env:PATHEXT='.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.CPL'
Set-Location -LiteralPath $harness
if((Get-FileHash -LiteralPath (Join-Path $policyRoot 'resource30.sha256') -Algorithm SHA256).Hash.ToLowerInvariant() -ne $PolicyManifestSha256) {
    throw 'resource policy manifest hash mismatch'
}
$header="launch $(Get-Date -Format o) resource-policy=$PolicyManifestSha256 mode=$Mode phase=main C-floor=30GiB E-floor=40GiB"
$logStream=[System.IO.File]::Open($Log,[System.IO.FileMode]::CreateNew,[System.IO.FileAccess]::Write,[System.IO.FileShare]::Read)
try {
    $writer=[System.IO.StreamWriter]::new($logStream,[System.Text.UTF8Encoding]::new($false))
    try { $writer.WriteLine($header) } finally { $writer.Dispose() }
} finally { $logStream.Dispose() }
& $python -B (Join-Path $policyRoot 'resource30_driver.py') --policy-sha $PolicyManifestSha256 $Mode *>> $Log
$code=$LASTEXITCODE
"exit=$code at $(Get-Date -Format o)" | Out-File -LiteralPath $Log -Encoding utf8 -Append
exit $code
