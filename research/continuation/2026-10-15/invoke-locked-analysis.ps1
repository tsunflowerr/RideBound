# Orchestration only: run the two exact frozen commands from analysis amendment E.
param(
    [Parameter(Mandatory=$true)][ValidateSet('extract','report')][string]$Step,
    [Parameter(Mandatory=$true)][string]$Log
)
$ErrorActionPreference='Stop'
$root='C:\RideBoundContinuation\2026-10-15'
$harness='E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness'
$python='E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe'
$admission=Get-Content -LiteralPath (Join-Path $root 'evidence\main-final-admission.json') -Raw | ConvertFrom-Json
if(-not $admission.AnalysisPermitted -or $admission.Completed -ne 68 -or $admission.FinalDriverExit -ne 0) {
    throw 'Full main completion/post-phase admission is required'
}
$prefixes=@('GIT_','PYTHON','DOTNET_','COREHOST_','COMPLUS_','OMP_','MKL_','OPENBLAS_','RB_')
foreach($item in @(Get-ChildItem Env:)) {
    foreach($prefix in $prefixes) {
        if($item.Name.ToUpperInvariant().StartsWith($prefix)) { Remove-Item -LiteralPath "Env:$($item.Name)"; break }
    }
}
foreach($name in @('NoDefaultCurrentDirectoryInExePath','SETUPTOOLS_USE_DISTUTILS')) {
    if(Test-Path -LiteralPath "Env:$name") { Remove-Item -LiteralPath "Env:$name" }
}
$env:PATH='C:\Windows\system32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0\;C:\Program Files\dotnet\;C:\Program Files\Git\cmd'
$env:PATHEXT='.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.CPL'
Set-Location -LiteralPath $harness
$scriptName=if($Step -eq 'extract'){'extract_stage1b.py'}else{'report_stage1b.py'}
$expected=if($Step -eq 'extract'){'6ff7f303f51a36d30afd22ea2c40c66290e8b9c1ee5a361461e2dca57083429c'}else{'ecbec711715a75f70a4dc5632b7978821e7c9b9e9767edfa84c5f07d00bdfc12'}
$hasher=[System.Security.Cryptography.SHA256]::Create()
try { $hash=([System.BitConverter]::ToString($hasher.ComputeHash([System.IO.File]::ReadAllBytes((Join-Path $harness $scriptName))))).Replace('-','').ToLowerInvariant() } finally { $hasher.Dispose() }
if($hash -ne $expected){throw 'Frozen analysis source mismatch'}
$programArgs=if($Step -eq 'extract'){@('-B',$scriptName,'main','C:/RideBoundContinuation/2026-10-15/results/stage1b')}else{@('-B',$scriptName,'C:/RideBoundContinuation/2026-10-15/results/stage1b','C:/RideBoundContinuation/2026-10-15/results/stage1b-report.txt')}
function Write-ExclusiveJson($Path,$Value) {
    $stream=[System.IO.File]::Open($Path,[System.IO.FileMode]::CreateNew,[System.IO.FileAccess]::Write,[System.IO.FileShare]::Read)
    try { $writer=[System.IO.StreamWriter]::new($stream,[System.Text.UTF8Encoding]::new($false)); try{$writer.WriteLine(($Value|ConvertTo-Json -Depth 6))}finally{$writer.Dispose()} }finally{$stream.Dispose()}
}
Write-ExclusiveJson (Join-Path $root ('evidence\analysis-'+$Step+'-started.json')) ([ordered]@{StartedAt=(Get-Date -Format o);LauncherPid=$PID;Step=$Step;Interpreter=$python;Arguments=$programArgs;SourceSha256=$hash;WorkingDirectory=$harness;Log=$Log})
$stream=[System.IO.File]::Open($Log,[System.IO.FileMode]::CreateNew,[System.IO.FileAccess]::Write,[System.IO.FileShare]::Read)
try {
    $writer=[System.IO.StreamWriter]::new($stream,[System.Text.UTF8Encoding]::new($false))
    try {
        $writer.WriteLine("analysis $Step source-sha256=$hash at $(Get-Date -Format o)");$writer.Flush()
        # Native stderr is recorded; it must not terminate the shell before its actual exit code is saved.
        $ErrorActionPreference='Continue'
        & $python @programArgs 2>&1 | ForEach-Object { $writer.WriteLine([string]$_);$writer.Flush();Write-Output $_ }
        $nativeCode=$LASTEXITCODE
        $ErrorActionPreference='Stop'
        $writer.WriteLine("exit=$nativeCode at $(Get-Date -Format o)");$writer.Flush()
    }finally{$writer.Dispose()}
}finally{$stream.Dispose()}
Write-ExclusiveJson (Join-Path $root ('evidence\analysis-'+$Step+'-finished.json')) ([ordered]@{FinishedAt=(Get-Date -Format o);Step=$Step;NativeExit=$nativeCode;SourceSha256=$hash;Log=$Log})
exit $nativeCode
