# PowerShell 7: stream both pipes without runspace callbacks; keep the watchdog and complete log.
function Invoke-LoggedProcess {
    param(
        [Parameter(Mandatory)][string]$FilePath,
        [Parameter(Mandatory)][string[]]$ArgumentList,
        [Parameter(Mandatory)][string]$LogPath,
        [ValidateRange(1,86400)][int]$TimeoutSeconds = 300,
        [switch]$StreamOutput
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
    $capture = [Text.StringBuilder]::new()
    $writer = [IO.StreamWriter]::new($LogPath, $false)
    $writer.AutoFlush = $true
    try {
        if (-not $process.Start()) { throw "Cannot start $FilePath" }
        $outRead = $process.StandardOutput.ReadLineAsync()
        $errRead = $process.StandardError.ReadLineAsync()
        $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while ($null -ne $outRead -or $null -ne $errRead -or -not $process.HasExited) {
            if ([DateTime]::UtcNow -ge $deadline) {
                if (-not $process.HasExited) { $process.Kill($true); $null = $process.WaitForExit(5000) }
                $writer.WriteLine("WATCHDOG TIMEOUT: $TimeoutSeconds seconds; owned process tree terminated.")
                throw "Timed out after $TimeoutSeconds seconds: $LogPath"
            }
            $drained = $false
            if ($null -ne $outRead -and $outRead.IsCompleted) {
                $line = $outRead.GetAwaiter().GetResult()
                if ($null -eq $line) { $outRead = $null } else {
                    $writer.WriteLine($line); $null = $capture.AppendLine($line)
                    if ($StreamOutput -and $line -match '^// (Benchmark:|\*\* Remained|\*\*\*\*\* Found)|Build Error|^.*: error ') { Write-Host $line }
                    $outRead = $process.StandardOutput.ReadLineAsync()
                }
                $drained = $true
            }
            if ($null -ne $errRead -and $errRead.IsCompleted) {
                $line = $errRead.GetAwaiter().GetResult()
                if ($null -eq $line) { $errRead = $null } else {
                    $writer.WriteLine("[stderr] $line"); $null = $capture.AppendLine($line)
                    if ($StreamOutput) { Write-Host "[stderr] $line" }
                    $errRead = $process.StandardError.ReadLineAsync()
                }
                $drained = $true
            }
            if (-not $drained) {
                $pending = @($outRead, $errRead, [Threading.Tasks.Task]::Delay(50)) | Where-Object { $null -ne $_ }
                $null = [Threading.Tasks.Task]::WhenAny([Threading.Tasks.Task[]]$pending).GetAwaiter().GetResult()
            }
        }
        if ($process.ExitCode -ne 0) { throw "Process failed with exit $($process.ExitCode): $LogPath" }
        return $capture.ToString()
    }
    finally { $writer.Dispose(); $process.Dispose() }
}
