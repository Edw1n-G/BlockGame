#requires -Version 7.0
param(
    [string]$Dotnet = 'dotnet',
    [ValidateRange(1,1000)][int]$Repeats = 20,
    [ValidateRange(1,512)][int]$ParallelThreads = 4,
    [ValidateRange(1,86400)][int]$TimeoutSeconds = 300,
    [string]$OutputDirectory = '',
    [switch]$SkipRestore,
    [string]$EngineRoot = '',
    [ValidateSet('Full','Family')][string]$Preflight = 'Full',
    [string[]]$Families = @()
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $EngineRoot) { $EngineRoot = $repoRoot }
Set-Item Function:Invoke-Logged -Value (& { . "$PSScriptRoot/run-benchmarks.ps1"; (Get-Command Invoke-Logged).ScriptBlock })
$project = Join-Path $EngineRoot 'BlockGame.Tests/BlockGame.Tests.csproj'
$filters = @()
if ($Preflight -eq 'Family') {
    if ($Families.Count -eq 0) { throw 'Family preflight requires selected families' }
    foreach ($family in $Families) {
        $filters += switch ($family) {
            'selection-full' { @('*ChunkSelectionTests','*IncrementalSelectionTests','*SelectionMovementWorkloadTests') }
            'selection-movement' { @('*IncrementalSelectionTests','*SelectionMovementWorkloadTests','*ChunkRequestorMovementTests','*GameLifecycleTests') }
            'mesh' { @('*MesherTests','*WorkloadTests','*StaleUploadTests') }
            'terrain' { @('*TerrainGenerationTests','*WorkloadTests') }
            'noise' { @('*FastNoiseConcurrencyTests','*WorkloadTests') }
            default { throw "Unknown preflight family: $family" }
        }
    }
}
$filterArgs = @($filters | Sort-Object -Unique | ForEach-Object { '-class'; $_ })
Push-Location $repoRoot
$created = $false
$result = [ordered]@{ schemaVersion = 1; status = 'running'; startedUtc = [DateTime]::UtcNow; runs = @() }
try {
    if (-not $OutputDirectory) { $OutputDirectory = "out/test-verification/$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0,8))" }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if ((Test-Path -LiteralPath $OutputDirectory) -and (Get-ChildItem -LiteralPath $OutputDirectory -Force)) { throw 'Verification output directory must be new/empty' }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $created = $true
    if (-not $SkipRestore) {
        Invoke-Logged $Dotnet @('restore',$project,'--disable-parallel') "$OutputDirectory/restore.log" $TimeoutSeconds | Out-Null
    }
    foreach ($configuration in @('Debug','Release')) {
        Write-Host "Building correctness suite: $configuration"
        Invoke-Logged $Dotnet @('build',$project,'--no-restore','-c',$configuration,'-m:1','-p:UseSharedCompilation=false') "$OutputDirectory/build-$configuration.log" $TimeoutSeconds | Out-Null
    }
    foreach ($mode in @('none','collections')) {
        for ($i = 1; $i -le $Repeats; $i++) {
            $log = "$OutputDirectory/Debug-$mode-$i.log"
            $output = Invoke-Logged $Dotnet (@((Join-Path $EngineRoot 'BlockGame.Tests/bin/Debug/net10.0/BlockGame.Tests.dll'),'-parallelMode',$mode,'-maxThreads',"$ParallelThreads") + $filterArgs) $log $TimeoutSeconds
            if ($output -notmatch 'Total:\s*(?<total>[1-9][0-9]*),\s*Errors:\s*0,\s*Failed:\s*0,\s*Skipped:\s*0,\s*Not Run:\s*0,') { throw "Failed, skipped or zero-discovery tests: $log" }
            $result.runs += @{ configuration = 'Debug'; mode = $mode; repetition = $i; total = [int]$Matches.total; status = 'passed'; log = $log }
            Write-Host "Passed Debug $mode $i/$Repeats ($($Matches.total) tests)"
        }
    }
    $log = "$OutputDirectory/Release-collections.log"
    $output = Invoke-Logged $Dotnet (@((Join-Path $EngineRoot 'BlockGame.Tests/bin/Release/net10.0/BlockGame.Tests.dll'),'-parallelMode','collections','-maxThreads',"$ParallelThreads") + $filterArgs) $log $TimeoutSeconds
    if ($output -notmatch 'Total:\s*(?<total>[1-9][0-9]*),\s*Errors:\s*0,\s*Failed:\s*0,\s*Skipped:\s*0,\s*Not Run:\s*0,') { throw "Release tests failed, skipped or discovered zero: $log" }
    $result.runs += @{ configuration = 'Release'; mode = 'collections'; total = [int]$Matches.total; status = 'passed'; log = $log }
    if (($result.runs.total | Sort-Object -Unique).Count -ne 1) { throw 'Test discovery counts differ across runs/configurations' }
    $result.status = 'passed'
    $result.scope = $Preflight.ToLowerInvariant()
}
catch { $result.status = 'failed'; $result.error = $_.Exception.Message; throw }
finally {
    $result.finishedUtc = [DateTime]::UtcNow
    if ($created) { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$OutputDirectory/result.json" }
    Pop-Location
}
