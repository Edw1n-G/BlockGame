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
    [ValidateRange(1,86400)][int]$TimeoutSec = 300,
    [string]$LoadNote = 'Uncontrolled desktop session; competing load not excluded',
    [string]$EngineRoot = '',
    [ValidateSet('Full','Family')][string]$Preflight = 'Full',
    [switch]$IncludeControls
)
function Invoke-Logged {
    param([string]$FilePath, [string[]]$ArgumentList, [string]$LogPath, [int]$TimeoutSec = 300)
    $start = [Diagnostics.ProcessStartInfo]@{ FileName = $FilePath; WorkingDirectory = $PWD.Path; UseShellExecute = $false; CreateNoWindow = $true; RedirectStandardOutput = $true; RedirectStandardError = $true }
    foreach ($argument in $ArgumentList) { $start.ArgumentList.Add($argument) }
    $p = [Diagnostics.Process]::Start($start)
    $stdout = [IO.File]::Create($LogPath)
    $stderr = [IO.File]::Create("$LogPath.stderr")
    try {
        $outCopy = $p.StandardOutput.BaseStream.CopyToAsync($stdout)
        $errCopy = $p.StandardError.BaseStream.CopyToAsync($stderr)
        if (-not $p.WaitForExit($TimeoutSec * 1000)) {
            if ($IsWindows) { & taskkill /PID $p.Id /T /F | Out-Null }
            else { Stop-Process -Id $p.Id -Force }
            throw "timed out after $TimeoutSec s"
        }
        [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($outCopy, $errCopy))
        $exitCode = $p.ExitCode
    } finally {
        $stdout.Dispose()
        $stderr.Dispose()
        $p.Dispose()
    }
    if ($exitCode -ne 0) { throw "Process failed with exit ${exitCode}: $LogPath" }
    Get-Content -LiteralPath $LogPath -Raw
}
if ($MyInvocation.InvocationName -eq '.') { return }
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not $EngineRoot) { $EngineRoot = $repoRoot }
if ($PSBoundParameters.ContainsKey('TimeoutSec')) {
    $PreflightTimeoutSeconds = $TimeoutSec
    $BenchmarkTimeoutSeconds = $TimeoutSec
}
Push-Location $repoRoot
$mutex = [Threading.Mutex]::new($false, 'Local\EdwinCraft.CpuBenchmarks.v1')
$locked = $false
$bundleCreated = $false
$oldLoad = $env:EDWINCRAFT_BENCHMARK_LOAD_NOTE
$oldPower = $env:EDWINCRAFT_BENCHMARK_POWER_PLAN
$oldEngine = $env:EDWINCRAFT_ENGINE_ROOT
$oldProject = $env:EngineProjectPath
$oldPreflight = $env:EDWINCRAFT_PREFLIGHT_RESULT
$oldScope = $env:EDWINCRAFT_PREFLIGHT_SCOPE
$result = [ordered]@{ schemaVersion = 1; status = 'running'; mode = $Mode; suitableForComparison = $false; startedUtc = [DateTime]::UtcNow }
try {
    try { $locked = $mutex.WaitOne(0) } catch [Threading.AbandonedMutexException] { $locked = $true }
    if (-not $locked) { throw 'Another verification/benchmark command owns the measurement lock' }
    if (-not $OutputDirectory) { $OutputDirectory = "artifacts/runs/$([DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss'))-$([Guid]::NewGuid().ToString('N').Substring(0,8))" }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $OutputDirectory) { throw 'Benchmark bundle directory must be new; existing runs are never overwritten' }
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    $bundleCreated = $true
    $result.outputDirectory = $OutputDirectory
    $result | ConvertTo-Json | Set-Content -LiteralPath "$OutputDirectory/result.json"
    $EngineRoot = (Resolve-Path -LiteralPath $EngineRoot).Path
    $env:EDWINCRAFT_ENGINE_ROOT = $EngineRoot
    $env:EngineProjectPath = Join-Path $EngineRoot 'BlockGame/BlockGame.csproj'
    if (-not (Test-Path -LiteralPath $env:EngineProjectPath)) { throw 'Engine project missing' }
    $benchProject = Join-Path $repoRoot 'BlockGame.Benchmarks/BlockGame.Benchmarks.csproj'
    Write-Host 'Restoring engine/tests and common harness'
    Invoke-Logged $Dotnet @('restore',(Join-Path $EngineRoot 'BlockGame.Tests/BlockGame.Tests.csproj'),'--disable-parallel') "$OutputDirectory/restore-tests.log" $PreflightTimeoutSeconds | Out-Null
    Invoke-Logged $Dotnet @('restore',$benchProject,'--disable-parallel') "$OutputDirectory/restore.log" $PreflightTimeoutSeconds | Out-Null
    Invoke-Logged $Dotnet @('build',$benchProject,'--no-restore','-c','Release','-m:1','-p:UseSharedCompilation=false') "$OutputDirectory/build-benchmarks.log" $PreflightTimeoutSeconds | Out-Null
    $benchmarkDll = Join-Path $repoRoot 'BlockGame.Benchmarks/bin/Release/net10.0/BlockGame.Benchmarks.dll'
    $selectionArgs = @()
    foreach ($pattern in $Case) { $selectionArgs += @('--case',$pattern) }
    if ($IncludeControls) { $selectionArgs += '--include-controls' }
    $catalog = Invoke-Logged $Dotnet (@($benchmarkDll,'--list-json') + $selectionArgs) "$OutputDirectory/selection.json" $PreflightTimeoutSeconds | ConvertFrom-Json
    $families = @($catalog.family | Sort-Object -Unique)
    Invoke-Logged $Dotnet (@($benchmarkDll,'--preflight-only') + $selectionArgs) "$OutputDirectory/dependency-preflight.log" $PreflightTimeoutSeconds | Out-Null
    & "$PSScriptRoot/verify-tests.ps1" -Dotnet $Dotnet -Repeats $TestRepeats -ParallelThreads $ParallelThreads -TimeoutSeconds $PreflightTimeoutSeconds -OutputDirectory "$OutputDirectory/verification" -SkipRestore -EngineRoot $EngineRoot -Preflight $Preflight -Families $families
    $preflightResult = Get-Content -LiteralPath "$OutputDirectory/verification/result.json" -Raw | ConvertFrom-Json
    if ($preflightResult.status -ne 'passed' -or $preflightResult.runs.Count -lt 3) { throw 'Correctness preflight did not pass' }
    $env:EDWINCRAFT_PREFLIGHT_RESULT = "$OutputDirectory/verification/result.json"
    $env:EDWINCRAFT_PREFLIGHT_SCOPE = $Preflight.ToLowerInvariant()
    $env:EDWINCRAFT_BENCHMARK_LOAD_NOTE = $LoadNote
    try { $env:EDWINCRAFT_BENCHMARK_POWER_PLAN = (& powercfg /getactivescheme 2>&1 | Out-String).Trim() } catch { $env:EDWINCRAFT_BENCHMARK_POWER_PLAN = 'unavailable' }
    Write-Host "Running $Mode cases '$Case'; artifacts: $OutputDirectory"
    $benchmarkArgs = @($benchmarkDll,'--mode',$Mode,'--output',"$OutputDirectory/benchmarks") + $selectionArgs
    Invoke-Logged $Dotnet $benchmarkArgs "$OutputDirectory/benchmark-launcher.log" $BenchmarkTimeoutSeconds | Out-Null
    $manifest = Get-Content -LiteralPath "$OutputDirectory/benchmarks/manifest.json" -Raw | ConvertFrom-Json
    if ($manifest.status -ne 'completed' -or $manifest.selectedCases.Count -eq 0 -or $manifest.completedCases.Count -ne $manifest.selectedCases.Count) { throw 'Benchmark selection did not complete' }
    Move-Item -LiteralPath "$OutputDirectory/benchmarks/benchmark-run.json" -Destination "$OutputDirectory/benchmark-run.json"
    $result.status = 'completed'
    $result.correctness = 'verification/result.json'
    $result.cases = $manifest.completedCases
    $result.correctnessScope = $Preflight.ToLowerInvariant()
    @('# Benchmark evidence', '', "Mode: $Mode; cases: $($result.cases.Count); preflight: $Preflight.", '', 'Use benchmark-run.json for scenario contracts and compare only verified bundles. Smoke and control/model runs are not production baselines.') | Set-Content -LiteralPath "$OutputDirectory/SUMMARY.md"
    $result.note = if ($Mode -eq 'Smoke') { 'Smoke only; timings are unsuitable for comparison' } else { 'Measurement complete; review raw results, identities, repeatability and environment before comparison' }
    Write-Host "Completed $($result.cases.Count) cases. $($result.note)"
}
catch { $result.status = 'failed'; $result.error = $_.Exception.Message; throw }
finally {
    $env:EDWINCRAFT_BENCHMARK_LOAD_NOTE = $oldLoad
    $env:EDWINCRAFT_BENCHMARK_POWER_PLAN = $oldPower
    $env:EDWINCRAFT_ENGINE_ROOT = $oldEngine
    $env:EngineProjectPath = $oldProject
    $env:EDWINCRAFT_PREFLIGHT_RESULT = $oldPreflight
    $env:EDWINCRAFT_PREFLIGHT_SCOPE = $oldScope
    $result.finishedUtc = [DateTime]::UtcNow
    if ($bundleCreated) { $result | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath "$OutputDirectory/result.json" }
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
    Pop-Location
}
if ($result.status -eq 'completed') {
    $tool = Join-Path $repoRoot 'BlockGame.Benchmarking/bin/Release/net10.0/BlockGame.Benchmarking.dll'
    Invoke-Logged $Dotnet @($tool,'pack',$OutputDirectory,"$OutputDirectory.zip") "$OutputDirectory-pack.log" $PreflightTimeoutSeconds | Out-Null
    Write-Host "Verified portable evidence: $OutputDirectory.zip (checksum alongside)"
}
