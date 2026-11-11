# Optional shortcut: compare saved result directories without creating checkouts.
param(
    [Parameter(Mandatory)][string]$Baseline,
    [Parameter(Mandatory)][string]$Candidate,
    [string]$Report = '',
    [string]$Dotnet = 'dotnet'
)
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    $compareArgs = @('run','-c', 'Release', '--project', 'BlockGame.Benchmarks', '--', 'compare', $Baseline, $Candidate)
    if ($Report) { $compareArgs += $Report }
    & $Dotnet @compareArgs
    exit $LASTEXITCODE
}
finally { Pop-Location }
