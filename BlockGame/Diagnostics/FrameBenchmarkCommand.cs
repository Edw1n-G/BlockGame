using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace Basics.Diagnostics;

public static class FrameBenchmarkCommand
{
    public static bool Handle(string[] args)
    {
        if (args.Length == 0 || args[0] != "--benchmark") return false;
        if (args.Length >= 2 && args[1] == "compare")
        {
            if (args.Length != 4) throw new ArgumentException("Usage: --benchmark compare <base folder or summary.json> <candidate folder or summary.json>");
            Console.WriteLine(FrameBenchmarkReport.Compare(FrameBenchmarkReport.ReadRuns(args[2]), FrameBenchmarkReport.ReadRuns(args[3])));
            return true;
        }
        var options = FrameBenchmarkOptions.Parse(args);
        if (options.Calibrate)
        {
            FrameBenchmarkCalibration.Run(options);
            return true;
        }
        if (options.Repeats == 1)
        {
            FrameBenchmarkRun.Options = options;
            Basics.Settings.GameSettings.SetGlobalDefault(options.Settings());
            return false;
        }
        RunRepeats(options);
        return true;
    }

    internal static FrameBenchmarkSummary[] RunRepeats(FrameBenchmarkOptions options)
    {
        Directory.CreateDirectory(options.Output);
        var runs = new List<FrameBenchmarkSummary>();
        for (int i = 1; i <= options.Repeats; i++)
        {
            var run = RunChild(options with { Output = Path.Combine(options.Output, $"run-{i:00}") });
            runs.Add(run);
            if (!run.Valid) System.Environment.ExitCode = 2;
        }
        string report = "# Frame benchmark repeats\n\n" + FrameBenchmarkReport.DescribeRepeats(runs.ToArray());
        File.WriteAllText(Path.Combine(options.Output, "repeats.md"), report);
        File.WriteAllText(Path.Combine(options.Output, "repeats.json"), JsonSerializer.Serialize(runs, FrameBenchmarkReport.JsonOptions));
        Console.WriteLine(report);
        return runs.ToArray();
    }

    internal static FrameBenchmarkSummary RunChild(FrameBenchmarkOptions options)
    {
        var info = new ProcessStartInfo(System.Environment.ProcessPath!)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(System.Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        string[] childArgs = ["--benchmark", "--render-distance", options.RenderDistance.ToString(CultureInfo.InvariantCulture),
            "--duration", options.Duration.ToString(CultureInfo.InvariantCulture), "--script", options.Script,
            "--world", options.World, "--output", options.Output, "--repeats", "1"];
        foreach (string arg in childArgs) info.ArgumentList.Add(arg);
        // Logs sit beside the child folder: the child requires an empty output directory.
        Directory.CreateDirectory(Path.GetDirectoryName(options.Output)!);
        using var stdout = File.Create(options.Output + ".stdout.log");
        using var stderr = File.Create(options.Output + ".stderr.log");
        using var process = Process.Start(info)!;
        Task output = process.StandardOutput.BaseStream.CopyToAsync(stdout);
        Task error = process.StandardError.BaseStream.CopyToAsync(stderr);
        var clock = Stopwatch.StartNew();
        long memoryLimit = Math.Max(1024L * 1024 * 1024, GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 2);
        string? stopped = null;
        while (!process.WaitForExit(250))
        {
            process.Refresh();
            if (process.WorkingSet64 > memoryLimit) stopped = $"Process exceeded the {memoryLimit / (1024d * 1024 * 1024):F1} GiB resident-memory limit (half of available physical memory)";
            if (clock.Elapsed.TotalSeconds > options.Duration + 120) stopped = "Process exceeded the warm-up/run/shutdown deadline";
            if (stopped != null) { process.Kill(entireProcessTree: true); process.WaitForExit(); break; }
        }
        Task.WaitAll(output, error);
        if (stopped != null) throw new InvalidOperationException(stopped);
        string summary = Path.Combine(options.Output, "summary.json");
        if (!File.Exists(summary) || process.ExitCode is not (0 or 2))
            throw new InvalidOperationException($"Run failed (exit {process.ExitCode}); see {options.Output}.stderr.log");
        return FrameBenchmarkReport.Read(summary);
    }
}
