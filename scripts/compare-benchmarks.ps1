#requires -Version 7.0
param(
    [Parameter(Mandatory)][string]$Baseline,
    [Parameter(Mandatory)][string]$Candidate,
    [Parameter(Mandatory)][ValidateNotNullOrEmpty()][string[]]$Case,
    [string]$HarnessRevision = 'HEAD',
    [ValidateSet('Smoke','Measure')][string]$Mode = 'Measure',
    [string]$Dotnet = 'dotnet',
    [string]$OutputDirectory = '',
    [string]$CheckoutDirectory = '',
    [ValidateRange(1,1000)][int]$TestRepeats = 1
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
Set-Item Function:Invoke-Logged -Value (& { . "$PSScriptRoot/run-benchmarks.ps1"; (Get-Command Invoke-Logged).ScriptBlock })
Push-Location $repoRoot
$experimentLock = [Threading.Mutex]::new($false,'Local\EdwinCraft.CpuBenchmarks.v1')
$experimentLocked = $false
$experimentState = $null
try {
    try { $experimentLocked=$experimentLock.WaitOne(0) } catch [Threading.AbandonedMutexException] { $experimentLocked=$true }
    if (-not $experimentLocked) { throw 'Another experiment owns the measurement lock' }
    $harnessStatus = @(& git status --porcelain -- BlockGame.Benchmarks scripts)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot check the benchmark harness working tree' }
    if ($harnessStatus.Count) { throw 'Harness has uncommitted changes; commit them first. The harness must be a revision.' }
    function Resolve-Revision([string]$Revision) {
        $resolved = (& git rev-parse --verify --end-of-options "$Revision^{commit}" 2>&1 | Out-String).Trim()
        if ($LASTEXITCODE -ne 0 -or $resolved -notmatch '^[0-9a-f]{40}$') { throw "Unknown Git revision: $Revision" }
        return $resolved
    }
    $baselineRevision = Resolve-Revision $Baseline
    $candidateRevision = Resolve-Revision $Candidate
    $harnessCommit = Resolve-Revision $HarnessRevision
    if (-not $OutputDirectory) { $OutputDirectory = "artifacts/comparisons/$([Guid]::NewGuid().ToString('N'))" }
    $OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
    if (Test-Path -LiteralPath $OutputDirectory) { throw 'Comparison output must be new' }
    $comparisonId = [Guid]::NewGuid().ToString('N')
    if (-not $CheckoutDirectory) {
        $CheckoutDirectory = Join-Path (Split-Path $repoRoot -Parent) "BlockGame.bench/$comparisonId"
    }
    $CheckoutDirectory = [IO.Path]::GetFullPath($CheckoutDirectory)
    $separator = [IO.Path]::DirectorySeparatorChar
    $insideRepository = $CheckoutDirectory.StartsWith($repoRoot+$separator,[StringComparison]::OrdinalIgnoreCase)
    if ($CheckoutDirectory -eq $repoRoot -or $insideRepository) {
        throw 'Checkouts must be outside the working repository'
    }
    if (Test-Path -LiteralPath $CheckoutDirectory) { throw 'Checkout directory must be new' }
    New-Item -ItemType Directory -Path $CheckoutDirectory | Out-Null
    $checkoutLogs = Join-Path $repoRoot "artifacts/logs/$comparisonId"
    New-Item -ItemType Directory -Path $checkoutLogs | Out-Null
    $revisions = [ordered]@{ harness = $harnessCommit; baseline = $baselineRevision; candidate = $candidateRevision }
    foreach ($name in $revisions.Keys) {
        $target = Join-Path $CheckoutDirectory $name
        Invoke-Logged git @('clone','--quiet','--no-hardlinks','--no-checkout',$repoRoot,$target) "$checkoutLogs/clone-$name.log" 120 | Out-Null
        Invoke-Logged git @('-C',$target,'checkout','--quiet','--detach',$revisions[$name]) "$checkoutLogs/checkout-$name.log" 120 | Out-Null
    }
    $harnessRoot = Join-Path $CheckoutDirectory 'harness'
    $harnessProject = Join-Path $harnessRoot 'BlockGame.Benchmarks/BlockGame.Benchmarks.csproj'
    foreach ($name in @('baseline','candidate')) {
        $engineProject = Join-Path $CheckoutDirectory "$name/BlockGame.Core/BlockGame.Core.csproj"
        $buildLog = Join-Path $checkoutLogs "build-$name.log"
        try {
            if (-not (Test-Path -LiteralPath $engineProject)) {
                "Engine project missing: $engineProject" | Set-Content -LiteralPath $buildLog
                throw 'Missing engine project'
            }
            Push-Location $harnessRoot
            try {
                Invoke-Logged $Dotnet @('build',$harnessProject,'-c','Release',"-p:EngineProjectPath=$engineProject") $buildLog | Out-Null
            }
            finally { Pop-Location }
        }
        catch {
            $tail = @(foreach ($path in @($buildLog, "$buildLog.stderr")) {
                if (Test-Path -LiteralPath $path) { Get-Content -LiteralPath $path -Tail 20 }
            }) | Select-Object -Last 20
            throw "Revision $($revisions[$name]) does not build with the benchmark harness; see docs/benchmarking.md (seam policy).`n$($tail -join "`n")"
        }
    }
    New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
    $experimentState=@{status='running';baselineRevision=$baselineRevision;candidateRevision=$candidateRevision;harnessRevision=$harnessCommit;checkouts=$CheckoutDirectory;checkoutLogs=$checkoutLogs}
    $experimentState | ConvertTo-Json | Set-Content "$OutputDirectory/experiment.json"
    foreach ($name in @('baseline','candidate')) {
        if ((& git -C "$CheckoutDirectory/$name" status --porcelain | Out-String).Trim()) { throw 'Engine checkout is dirty before measurement' }
        & "$harnessRoot/scripts/run-benchmarks.ps1" -Dotnet $Dotnet -Mode $Mode -Case $Case -EngineRoot "$CheckoutDirectory/$name" -OutputDirectory "$OutputDirectory/$name" -TestRepeats $TestRepeats
        if ((& git -C "$CheckoutDirectory/$name" status --porcelain | Out-String).Trim()) { throw 'Engine checkout changed during measurement' }
    }
    $tool = "$harnessRoot/BlockGame.Benchmarks/bin/Release/net10.0/BlockGame.Benchmarks.dll"
    Invoke-Logged $Dotnet @($tool,'compare',"$OutputDirectory/baseline.zip","$OutputDirectory/candidate.zip","$OutputDirectory/comparison.json") "$OutputDirectory/compare.log" 120 | Out-Null
    Write-Host "Comparison report: $OutputDirectory/comparison.md"
    Write-Host "Clean sibling checkouts retained: $CheckoutDirectory"
    $experimentState.status='completed'
}
catch { if($experimentState){$experimentState.status='failed';$experimentState.error=$_.Exception.Message};throw }
finally { if($experimentState){$experimentState | ConvertTo-Json | Set-Content "$OutputDirectory/experiment.json"};if($experimentLocked){$experimentLock.ReleaseMutex()};$experimentLock.Dispose();Pop-Location }
