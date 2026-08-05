[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'tools\RideBound.Wp4Microbenchmark\RideBound.Wp4Microbenchmark.csproj'

dotnet run --configuration Release --project $project
