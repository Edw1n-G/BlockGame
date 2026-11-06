#requires -Version 7.0
param([string]$Dotnet = 'dotnet')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
. "$PSScriptRoot/ProcessTools.ps1"
Push-Location $repo
$testRoot = Join-Path $repo "out/harness-checks/$([Guid]::NewGuid().ToString('N'))"
New-Item -ItemType Directory $testRoot | Out-Null
$checks = @()
function Expect-Failure([string]$Name, [scriptblock]$Action, [string]$Pattern) {
    try { & $Action; throw "Expected rejection did not occur: $Name" }
    catch { if ($_.Exception.Message -notmatch $Pattern) { throw }; Write-Host "Passed rejection: $Name" }
}
$oldResult = $env:EDWINCRAFT_SCRIPT_TEST_RESULT
$oldMarker = $env:EDWINCRAFT_SCRIPT_TEST_MARKER
try {
    Invoke-LoggedProcess $Dotnet @('build','scripts/fixtures/FakeDotnet/FakeDotnet.csproj','-c','Release','-m:1','-p:UseSharedCompilation=false','--output',"$testRoot/fake") "$testRoot/build-fake.log" 300 | Out-Null
    $fake = "$testRoot/fake/FakeDotnet.exe"
    $env:EDWINCRAFT_SCRIPT_TEST_MARKER = "$testRoot/benchmark-reached.txt"
    foreach ($mode in @('failure','zero','skip')) {
        $env:EDWINCRAFT_SCRIPT_TEST_RESULT = $mode
        Expect-Failure "preflight-$mode" { & "$repo/run-benchmarks.ps1" -Dotnet $fake -OutputDirectory "$testRoot/$mode" } 'Failed, skipped or zero-discovery'
        if (Test-Path $env:EDWINCRAFT_SCRIPT_TEST_MARKER) { throw 'Benchmark launched despite failed preflight' }
        $state = Get-Content "$testRoot/$mode/result.json" -Raw | ConvertFrom-Json
        if ($state.status -ne 'failed') { throw 'Failed preflight was not retained as failed' }
    }
    Expect-Failure 'zero repetitions' { & "$repo/verify-tests.ps1" -Repeats 0 } 'range'
    Expect-Failure 'zero workers' { & "$repo/verify-tests.ps1" -ParallelThreads 0 } 'range'
    Expect-Failure 'empty case list' { & "$repo/run-benchmarks.ps1" -Case @() } 'null|empty'
    Expect-Failure 'existing output' { & "$repo/run-benchmarks.ps1" -Dotnet $fake -OutputDirectory "$testRoot/failure" } 'must be new'
    $start = [Diagnostics.ProcessStartInfo]::new($fake)
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.ArgumentList.Add('lock'); $start.ArgumentList.Add("$testRoot/lock-ready")
    $holder = [Diagnostics.Process]::Start($start)
    try {
        $deadline = [DateTime]::UtcNow.AddSeconds(5)
        while (-not (Test-Path "$testRoot/lock-ready")) {
            if ([DateTime]::UtcNow -gt $deadline) { throw 'Lock fixture did not start' }
            Start-Sleep -Milliseconds 50
        }
        Expect-Failure 'concurrent run' { & "$repo/run-benchmarks.ps1" -Dotnet $fake -OutputDirectory "$testRoot/concurrent" } 'measurement lock'
    }
    finally { if (-not $holder.HasExited) { $holder.Kill($true); $holder.WaitForExit() }; $holder.Dispose() }
    Expect-Failure 'nonzero child exit' { Invoke-LoggedProcess $fake @('fail') "$testRoot/exit.log" 10 } 'exit 7'
    Expect-Failure 'watchdog process tree' { Invoke-LoggedProcess $fake @('spawn',"$testRoot/child.pid") "$testRoot/timeout.log" 2 } 'Timed out'
    $childPid = [int](Get-Content "$testRoot/child.pid")
    if (Get-Process -Id $childPid -ErrorAction SilentlyContinue) { throw 'Watchdog left child process alive' }
    $bench = "$repo/BlockGame.Benchmarks/bin/Release/net10.0/BlockGame.Benchmarks.dll"
    Expect-Failure 'invalid case' { Invoke-LoggedProcess $Dotnet @($bench,'--list','--case','does-not-exist') "$testRoot/filter.log" 30 } 'exit 1'
    if ((Get-Content "$testRoot/filter.log" -Raw) -notmatch 'No benchmark cases match') { throw 'Unexpected filter rejection' }
    $union = Invoke-LoggedProcess $Dotnet @($bench,'--list','--case','selectiondelta/walk-x/*','--case','selection/typical/*') "$testRoot/filter-union.log" 30
    if (@($union -split "`n" | Where-Object { $_ -match '^selection(delta)?/' }).Count -ne 2) { throw 'Union filter did not select both contracts' }
    Expect-Failure 'mixed valid/invalid cases' { Invoke-LoggedProcess $Dotnet @($bench,'--list','--case','selectiondelta/walk-x/*','--case','typo') "$testRoot/filter-mixed.log" 30 } 'exit 1'
    # Copy the launcher output to an isolated directory; never rename/delete the real engine binary or content.
    Copy-Item -LiteralPath "$repo/BlockGame.Benchmarks/bin/Release/net10.0" -Destination "$testRoot/missing-native" -Recurse
    Remove-Item -LiteralPath "$testRoot/missing-native/FastNoise.dll"
    $cpuBlock=Get-ChildItem "$testRoot/missing-native/Content" -Recurse -Filter stone.json | Select-Object -First 1
    Remove-Item -LiteralPath $cpuBlock.FullName
    Invoke-LoggedProcess $Dotnet @("$testRoot/missing-native/BlockGame.Benchmarks.dll",'--preflight-only','--case','selection/zero/*') "$testRoot/cpu-only-preflight.log" 30 | Out-Null
    Expect-Failure 'missing native' { Invoke-LoggedProcess $Dotnet @("$testRoot/missing-native/BlockGame.Benchmarks.dll",'--output',"$testRoot/native-output") "$testRoot/missing-native.log" 30 } 'exit 1'
    if ((Get-Content "$testRoot/missing-native.log" -Raw) -notmatch 'Required native binary missing') { throw 'Unexpected native rejection' }
    Copy-Item -LiteralPath "$repo/BlockGame.Benchmarks/bin/Release/net10.0" -Destination "$testRoot/missing-content" -Recurse
    $block = Get-ChildItem "$testRoot/missing-content/Content" -Recurse -Filter stone.json | Select-Object -First 1
    Remove-Item -LiteralPath $block.FullName
    Expect-Failure 'missing content' { Invoke-LoggedProcess $Dotnet @("$testRoot/missing-content/BlockGame.Benchmarks.dll",'--output',"$testRoot/content-output") "$testRoot/missing-content.log" 30 } 'exit 1'
    if ((Get-Content "$testRoot/missing-content.log" -Raw) -notmatch 'Required content missing') { throw 'Unexpected content rejection' }
    $flood=Invoke-LoggedProcess $fake @('flood') "$testRoot/flood.log" 30
    if($flood -notmatch 'out-1999' -or $flood -notmatch 'err-1999'){throw 'Pipe streaming lost output'}
    $escapedTools=(Join-Path $PSScriptRoot 'ProcessTools.ps1').Replace("'","''")
    $escapedFake=$fake.Replace("'","''")
    $escapedLog="$testRoot/live.log".Replace("'","''")
    ". '$escapedTools'; Invoke-LoggedProcess '$escapedFake' @('stream') '$escapedLog' 10 | Out-Null" | Set-Content "$testRoot/probe.ps1"
    $probeStart=[Diagnostics.ProcessStartInfo]::new((Get-Process -Id $PID).Path)
    $probeStart.UseShellExecute=$false;$probeStart.CreateNoWindow=$true
    $probeStart.ArgumentList.Add('-NoProfile');$probeStart.ArgumentList.Add('-File');$probeStart.ArgumentList.Add("$testRoot/probe.ps1")
    $probe=[Diagnostics.Process]::Start($probeStart)
    try {
        $deadline=[DateTime]::UtcNow.AddSeconds(5)
        while(-not (Test-Path "$testRoot/live.log") -or (Get-Content "$testRoot/live.log" -Raw) -notmatch 'STREAM-READY') {
            if([DateTime]::UtcNow -gt $deadline){throw 'Live log did not flush'}
            Start-Sleep -Milliseconds 25
        }
        if($probe.HasExited){throw 'Output was not visible while process was alive'}
        if(-not $probe.WaitForExit(10000) -or $probe.ExitCode -ne 0){throw 'Streaming probe failed'}
    }
    finally{if(-not $probe.HasExited){$probe.Kill($true);$probe.WaitForExit()};$probe.Dispose()}
    @{ status = 'passed'; checks = @('failed/zero/skipped preflight','positive repetition/worker validation','empty case list','no overwrite','concurrency lock','nonzero exit','watchdog tree kill','invalid case','filter union and per-pattern rejection','CPU preflight without native/content','native family missing dependencies','live output before exit','both-pipe flood'); utc = [DateTime]::UtcNow } | ConvertTo-Json | Set-Content "$testRoot/result.json"
    Write-Host "Harness checks passed: $testRoot"
}
finally {
    $env:EDWINCRAFT_SCRIPT_TEST_RESULT = $oldResult
    $env:EDWINCRAFT_SCRIPT_TEST_MARKER = $oldMarker
    Pop-Location
}
