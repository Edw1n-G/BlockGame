# Optional shortcut for the full test suite.
param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
Push-Location (Join-Path $PSScriptRoot '..')
try {
    & $Dotnet test
    exit $LASTEXITCODE
}
finally { Pop-Location }
