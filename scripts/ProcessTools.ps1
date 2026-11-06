# PowerShell 7: ArgumentList and Kill(entireProcessTree) avoid shell quoting and orphaned native workers.
function Invoke-LoggedProcess {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList,
        [Parameter(Mandatory)][string]$LogPath,
        [ValidateRange(1,86400)][int]$TimeoutSeconds = 300
    )
    $LogPath = [IO.Path]::GetFullPath($LogPath)
    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($LogPath)) | Out-Null
    $start = [Diagnostics.ProcessStartInfo]::new()
    $start.FileName = $FilePath
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.WorkingDirectory = (Get-Location).Path
    foreach ($argument in $ArgumentList) { $start.ArgumentList.Add($argument) }
    $process = [Diagnostics.Process]::new()
    $process.StartInfo = $start
    $timedOut = $false
    try {
        if (-not $process.Start()) { throw "Cannot start $FilePath" }
        $stdout = $process.StandardOutput.ReadToEndAsync()
        $stderr = $process.StandardError.ReadToEndAsync()
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (-not $process.WaitForExit(200)) {
            if ([DateTime]::UtcNow -ge $deadline) {
                $timedOut = $true
                $process.Kill($true)
                if (-not $process.WaitForExit(5000)) { throw "Process tree did not exit after timeout; PID=$($process.Id)" }
                break
            }
        }
        if (-not [Threading.Tasks.Task]::WaitAll([Threading.Tasks.Task[]]@($stdout, $stderr), 5000)) {
            [IO.File]::WriteAllText($LogPath, "Pipe drain timed out; PID=$($process.Id)")
            throw "Process output did not close: $LogPath"
        }
        $output = $stdout.Result + "`n[stderr]`n" + $stderr.Result
        if ($timedOut) { $output += "`nWATCHDOG TIMEOUT: $TimeoutSeconds seconds; process tree terminated." }
        [IO.File]::WriteAllText($LogPath, $output)
        if ($timedOut) { throw "Timed out after $TimeoutSeconds seconds: $LogPath" }
        if ($process.ExitCode -ne 0) { throw "Process failed with exit $($process.ExitCode): $LogPath" }
        return $output
    }
    finally { $process.Dispose() }
}
