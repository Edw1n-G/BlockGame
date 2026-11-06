#requires -Version 7.0
param(
    [ValidateSet('Smoke','Measure')][string]$Mode = 'Smoke',
    [ValidateNotNullOrEmpty()][string[]]$Case = @('*'),
    [string]$Dotnet = 'dotnet',
    [string]$OutputDirectory = '',
    [ValidateRange(1,1000)][int]$TestRepeats = 1,
    [ValidateRange(1,512)][int]$ParallelThreads = 4,
    [ValidateRange(1,86400)][int]$PreflightTimeoutSeconds = 300,
    [ValidateRange(1,86400)][int]$BenchmarkTimeoutSeconds = 7200,
    [string]$LoadNote = 'Uncontrolled desktop session; competing load not excluded'
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/scripts/ProcessTools.ps1"
Push-Location $PSScriptRoot
$mutex = [Threading.Mutex]::new($false, 'Local\EdwinCraft.CpuBenchmarks.v1')
$locked = $false
$bundleCreated = $false
$oldLoad = $env:EDWINCRAFT_BENCHMARK_LOAD_NOTE
$oldPower = $env:EDWINCRAFT_BENCHMARK_POWER_PLAN
$result = [ordered]@{ schemaVersion = 1; status = 'running'; mode = $Mode; suitableForComparison = $false; startedUtc = [DateTime]::UtcNow }
try {
    try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (-not $locked) { throw 'Another verification/benchmark command owns the measurement lock' }
    if (-not $OutputDirectory) { $OutputDirectory = "out/benchmarks/$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0,8))" }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $OutputDirectory) { throw 'Benchmark bundle directory must be new; existing runs are never overwritten' }
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    $bundleCreated = $true
    $result.outputDirectory = $OutputDirectory
    $result | ConvertTo-Json | Set-Content -LiteralPath "$OutputDirectory/result.json"
    Write-Host 'Restoring solution and verifying correctness before measurement'
    Invoke-LoggedProcess $Dotnet @('restore','Rendering.sln','--disable-parallel') "$OutputDirectory/restore.log" $PreflightTimeoutSeconds | Out-Null
    & "$PSScriptRoot/verify-tests.ps1" -Dotnet $Dotnet -Repeats $TestRepeats -ParallelThreads $ParallelThreads -TimeoutSeconds $PreflightTimeoutSeconds -OutputDirectory "$OutputDirectory/verification" -SkipRestore
    $preflight = Get-Content -LiteralPath "$OutputDirectory/verification/result.json" -Raw | ConvertFrom-Json
    if ($preflight.status -ne 'passed' -or $preflight.runs.Count -lt 3) { throw 'Correctness preflight did not pass' }
    Invoke-LoggedProcess $Dotnet @('build','BlockGame.Benchmarks/BlockGame.Benchmarks.csproj','--no-restore','-c','Release','-m:1','-p:UseSharedCompilation=false') "$OutputDirectory/build-benchmarks.log" $PreflightTimeoutSeconds | Out-Null
    $env:EDWINCRAFT_BENCHMARK_LOAD_NOTE = $LoadNote
    try { $env:EDWINCRAFT_BENCHMARK_POWER_PLAN = (& powercfg /getactivescheme 2>&1 | Out-String).Trim() } catch { $env:EDWINCRAFT_BENCHMARK_POWER_PLAN = 'unavailable' }
    Write-Host "Running $Mode cases '$Case'; artifacts: $OutputDirectory"
    $benchmarkArgs = @('BlockGame.Benchmarks/bin/Release/net10.0/BlockGame.Benchmarks.dll','--mode',$Mode,'--output',"$OutputDirectory/benchmarks")
    foreach ($pattern in $Case) { $benchmarkArgs += @('--case',$pattern) }
    Invoke-LoggedProcess $Dotnet $benchmarkArgs "$OutputDirectory/benchmark-launcher.log" $BenchmarkTimeoutSeconds | Out-Null
    $manifest = Get-Content -LiteralPath "$OutputDirectory/benchmarks/manifest.json" -Raw | ConvertFrom-Json
    if ($manifest.status -ne 'completed' -or $manifest.selectedCases.Count -eq 0 -or $manifest.completedCases.Count -ne $manifest.selectedCases.Count) { throw 'Benchmark selection did not complete' }
    $result.status = 'completed'
    $result.correctness = 'verification/result.json'
    $result.cases = $manifest.completedCases
    $result.note = if ($Mode -eq 'Smoke') { 'Smoke only; timings are unsuitable for comparison' } else { 'Measurement complete; review raw results, identities, repeatability and environment before comparison' }
    Write-Host "Completed $($result.cases.Count) cases. $($result.note)"
}
catch { $result.status = 'failed'; $result.error = $_.Exception.Message; throw }
finally {
    $env:EDWINCRAFT_BENCHMARK_LOAD_NOTE = $oldLoad
    $env:EDWINCRAFT_BENCHMARK_POWER_PLAN = $oldPower
    $result.finishedUtc = [DateTime]::UtcNow
    if ($bundleCreated) { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$OutputDirectory/result.json" }
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
    Pop-Location
}
