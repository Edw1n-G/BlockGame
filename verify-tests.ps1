param(
    [string]$Dotnet = 'dotnet',
    [int]$Repeats = 20,
    [int]$ParallelThreads = 4
)

$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
    & $Dotnet restore BlockGame.Tests/BlockGame.Tests.csproj --disable-parallel
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
    & $Dotnet build BlockGame.Tests/BlockGame.Tests.csproj --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Debug build failed' }
    & $Dotnet build BlockGame/BlockGame.csproj --no-restore -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Release build failed' }
    $assembly = 'BlockGame.Tests/bin/Debug/net10.0/BlockGame.Tests.dll'
    foreach ($mode in @('none', 'collections')) {
        for ($i = 1; $i -le $Repeats; $i++) {
            Write-Host "$mode run $i/$Repeats"
            $output = & $Dotnet $assembly -parallelMode $mode -maxThreads $ParallelThreads 2>&1
            $exit = $LASTEXITCODE
            $output | ForEach-Object { Write-Host $_ }
            if ($exit -ne 0 -or -not ($output -match 'Total: [1-9][0-9]*,')) {
                throw "Test run failed or discovered no tests: $mode run $i"
            }
        }
    }
}
finally { Pop-Location }
