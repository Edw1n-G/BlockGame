# Optional shortcut for the manual commands in docs/benchmarking.md.
param(
    [ValidateSet('Smoke','Measure')][string]$Mode = 'Measure',
    [string[]]$Case = @(),
    [string]$OutputDirectory = '',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $Dotnet test
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    $benchmarkArgs = @('run', '-c', 'Release', '--project', 'BlockGame.Benchmarks', '--', '--mode', $Mode)
    foreach ($pattern in $Case) { $benchmarkArgs += @('--case', $pattern) }
    if ($OutputDirectory) { $benchmarkArgs += @('--output', $OutputDirectory) }
    & $Dotnet @benchmarkArgs
    exit $LASTEXITCODE
}
finally { Pop-Location }
