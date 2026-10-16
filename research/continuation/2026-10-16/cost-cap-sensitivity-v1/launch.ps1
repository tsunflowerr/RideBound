param(
    [Parameter(Mandatory=$true)][ValidateSet('freeze','selftest','run','status')][string]$Mode,
    [Parameter(Mandatory=$true)][string]$Log,
    [string]$ManifestSha256
)
$ErrorActionPreference='Stop'
foreach($item in @(Get-ChildItem Env:)) {
    foreach($prefix in @('GIT_','PYTHON','DOTNET_','COREHOST_','COMPLUS_','OMP_','MKL_','OPENBLAS_','RB_')) {
        if($item.Name.ToUpperInvariant().StartsWith($prefix)) { Remove-Item -LiteralPath "Env:$($item.Name)"; break }
    }
}
foreach($name in @('NoDefaultCurrentDirectoryInExePath','SETUPTOOLS_USE_DISTUTILS')) {
    if(Test-Path -LiteralPath "Env:$name") { Remove-Item -LiteralPath "Env:$name" }
}
$env:PATH='C:\Windows\system32;C:\Windows;C:\Windows\System32\Wbem;C:\Windows\System32\WindowsPowerShell\v1.0\;C:\Program Files\dotnet\;C:\Program Files\Git\cmd'
$env:PATHEXT='.COM;.EXE;.BAT;.CMD;.VBS;.VBE;.JS;.JSE;.WSF;.WSH;.MSC;.CPL'
Set-Location -LiteralPath 'E:\Code\Report_INT3508\ridebound-scratchpad\tang3\stage1b\harness'
$stream=[System.IO.File]::Open($Log,[System.IO.FileMode]::CreateNew,[System.IO.FileAccess]::Write,[System.IO.FileShare]::Read)
$writer=[System.IO.StreamWriter]::new($stream,[System.Text.UTF8Encoding]::new($false))
try {
    $writer.WriteLine("launch mode=$Mode manifest=$ManifestSha256 at $(Get-Date -Format o)");$writer.Flush()
    $programArgs=@('-B',(Join-Path $PSScriptRoot 'driver.py'),$Mode)
    if($ManifestSha256){$programArgs+=@('--manifest-sha',$ManifestSha256)}
    $ErrorActionPreference='Continue'
    & 'E:\RideBoundData\wp7\envs\fleetpy-1.0.2\python.exe' @programArgs 2>&1 | ForEach-Object { $writer.WriteLine([string]$_);$writer.Flush() }
    $nativeCode=$LASTEXITCODE
    $ErrorActionPreference='Stop'
    $writer.WriteLine("exit=$nativeCode at $(Get-Date -Format o)");$writer.Flush()
} finally { $writer.Dispose();$stream.Dispose() }
exit $nativeCode
