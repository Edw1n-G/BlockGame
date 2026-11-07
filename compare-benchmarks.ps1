#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$Baseline,
    [Parameter(Mandatory)][string]$Candidate,
    [string[]]$Case = @('selection/typical/*'),
    [ValidateSet('Smoke','Measure')][string]$Mode = 'Measure',
    [string]$Dotnet = 'dotnet',
    [string]$OutputDirectory = '',
    [string]$CheckoutDirectory = '',
    [ValidateRange(1,1000)][int]$TestRepeats = 1
)
$ErrorActionPreference = 'Stop'
. "$PSScriptRoot/scripts/ProcessTools.ps1"
Push-Location $PSScriptRoot
$experimentLock = [Threading.Mutex]::new($false,'Local\EdwinCraft.CpuBenchmarks.v1')
$experimentLocked = $false
$experimentState = $null
try {
    try { $experimentLocked=$experimentLock.WaitOne(0) } catch [Threading.AbandonedMutexException] { $experimentLocked=$true }
    if (-not $experimentLocked) { throw 'Another experiment owns the measurement lock' }
    function Resolve-Revision([string]$Revision) {
        $resolved = (& git rev-parse --verify --end-of-options "$Revision^{commit}" 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $resolved -notmatch '^[0-9a-f]{40}$') { throw "Unknown Git revision: $Revision" }
        return $resolved
    }
    $baselineRevision = Resolve-Revision $Baseline
    $candidateRevision = Resolve-Revision $Candidate
    if (-not $OutputDirectory) { $OutputDirectory = "artifacts/comparisons/$([Guid]::NewGuid().ToString('N'))" }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $OutputDirectory) { throw 'Comparison output must be new' }
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    if (-not $CheckoutDirectory) { $CheckoutDirectory = "artifacts/checkouts/$([Guid]::NewGuid().ToString('N'))" }
    $CheckoutDirectory = [IO.Path]::GetFullPath($CheckoutDirectory)
    $separator = [IO.Path]::DirectorySeparatorChar
    $insideRepository = $CheckoutDirectory.StartsWith($PSScriptRoot+$separator,[StringComparison]::OrdinalIgnoreCase)
    $artifactPrefix = (Join-Path $PSScriptRoot 'artifacts')+$separator
    if ($CheckoutDirectory -eq $PSScriptRoot -or ($insideRepository -and -not $CheckoutDirectory.StartsWith($artifactPrefix,[StringComparison]::OrdinalIgnoreCase))) {
        throw 'Checkouts must be under artifacts/ or outside the working repository'
    }
    if (Test-Path -LiteralPath $CheckoutDirectory) { throw 'Checkout directory must be new' }
    New-Item -ItemType Directory -Path $CheckoutDirectory | Out-Null
    foreach ($name in @('harness','baseline','candidate')) {
        $target = Join-Path $CheckoutDirectory $name
        Invoke-LoggedProcess git @('clone','--quiet','--no-hardlinks','--no-checkout',$PSScriptRoot,$target) "$OutputDirectory/clone-$name.log" 120 | Out-Null
        $revision = switch ($name) { 'baseline' {$baselineRevision} 'candidate' {$candidateRevision} default {Resolve-Revision 'HEAD'} }
        if ($name -ne 'harness') {
            Invoke-LoggedProcess git @('-C',$target,'checkout','--quiet','--detach',$revision) "$OutputDirectory/checkout-$name.log" 120 | Out-Null
        }
    }
    # One immutable common harness snapshot, separate from both untouched engine revision checkouts.
    foreach ($directory in @('BlockGame.Benchmarks','BlockGame.Workloads','BlockGame.Benchmarking','scripts')) {
        $target = Join-Path "$CheckoutDirectory/harness" $directory
        New-Item -ItemType Directory -Path $target -Force | Out-Null
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $directory) -Recurse -File) {
            $relative = [IO.Path]::GetRelativePath((Join-Path $PSScriptRoot $directory),$file.FullName)
            if ($relative -split '[\\/]' | Where-Object { $_ -in @('bin','obj') }) { continue }
            $destination = Join-Path $target $relative
            [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($destination)) | Out-Null
            Copy-Item -LiteralPath $file.FullName -Destination $destination
        }
    }
    foreach ($file in @('run-benchmarks.ps1','verify-tests.ps1','global.json','.gitignore','AGENTS.md','Documentation.md','BENCHMARK_WORKER_TRIAL.md','BENCHMARK_MILESTONE_DRAFT.md','Directory.Build.props','Directory.Build.targets','Directory.Packages.props','NuGet.Config')) {
        if (Test-Path -LiteralPath "$PSScriptRoot/$file") { Copy-Item -LiteralPath "$PSScriptRoot/$file" -Destination "$CheckoutDirectory/harness/$file" }
    }
    if(Test-Path -LiteralPath "$PSScriptRoot/docs"){Copy-Item -LiteralPath "$PSScriptRoot/docs" -Destination "$CheckoutDirectory/harness/docs" -Recurse}
    $controlTarget="$CheckoutDirectory/harness/BlockGame.Tests/TestData"
    New-Item -ItemType Directory -Path $controlTarget -Force | Out-Null
    Copy-Item -LiteralPath "$PSScriptRoot/BlockGame.Tests/TestData/BenchmarkControls" -Destination $controlTarget -Recurse
    $experimentState=@{status='running';baselineRevision=$baselineRevision;candidateRevision=$candidateRevision;harnessSource='captured current harness; same fingerprint required';checkouts=$CheckoutDirectory}
    $experimentState | ConvertTo-Json | Set-Content "$OutputDirectory/experiment.json"
    foreach ($name in @('baseline','candidate')) {
        if ((& git -C "$CheckoutDirectory/$name" status --porcelain | Out-String).Trim()) { throw 'Engine checkout is dirty before measurement' }
        if (-not (Test-Path -LiteralPath "$CheckoutDirectory/$name/BlockGame/Game/Logic/TerrainManaging/ChunkSelectionPlan.cs")) { throw "Revision lacks the common harness selection interface. Add a reviewed adapter/contract before measuring this revision; never substitute copied reference code." }
        & "$CheckoutDirectory/harness/run-benchmarks.ps1" -Dotnet $Dotnet -Mode $Mode -Case $Case -EngineRoot "$CheckoutDirectory/$name" -OutputDirectory "$OutputDirectory/$name" -TestRepeats $TestRepeats
        if ((& git -C "$CheckoutDirectory/$name" status --porcelain | Out-String).Trim()) { throw 'Engine checkout changed during measurement' }
    }
    $tool = "$CheckoutDirectory/harness/BlockGame.Benchmarking/bin/Release/net10.0/BlockGame.Benchmarking.dll"
    Invoke-LoggedProcess $Dotnet @($tool,'compare',"$OutputDirectory/baseline.zip","$OutputDirectory/candidate.zip","$OutputDirectory/comparison.json") "$OutputDirectory/compare.log" 120 -StreamOutput | Out-Null
    Write-Host "Comparison report: $OutputDirectory/comparison.md"
    Write-Host "Clean sibling checkouts retained: $CheckoutDirectory"
    $experimentState.status='completed'
}
catch { if($experimentState){$experimentState.status='failed';$experimentState.error=$_.Exception.Message};throw }
finally { if($experimentState){$experimentState | ConvertTo-Json | Set-Content "$OutputDirectory/experiment.json"};if($experimentLocked){$experimentLock.ReleaseMutex()};$experimentLock.Dispose();Pop-Location }
