using BlockGame.Benchmarks.Workloads;
using BlockGame.Benchmarks.Comparison;
using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace BlockGame.Benchmarks.Harness;

internal static class RunEvidence
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));

    public static object SourceIdentity()
    {
        string Git(string workingDirectory, params string[] args)
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            start.WorkingDirectory=workingDirectory;
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var p = Process.Start(start)!;
            var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30_000)) { p.Kill(true); throw new TimeoutException("Git evidence timed out"); }
            if (p.ExitCode != 0) throw new InvalidOperationException("Cannot identify source: " + stderr.Result);
            return stdout.Result.TrimEnd('\r', '\n');
        }
        string root = Path.GetFullPath(Git(Environment.CurrentDirectory,"rev-parse", "--show-toplevel"));
        string status = Git(root,"status", "--porcelain=v1", "--untracked-files=all");
        string engineRoot = Path.GetFullPath(Environment.GetEnvironmentVariable("EDWINCRAFT_ENGINE_ROOT") ?? root);
        return new
        {
            root, commit = Git(root, "rev-parse", "HEAD"), branch = Git(root, "branch", "--show-current"),
            dirty = status.Length > 0, status, engineRoot,
            engineRevision = Git(engineRoot, "rev-parse", "HEAD"),
            engineClean = status.Length == 0 && Git(engineRoot, "status", "--porcelain=v1", "--untracked-files=all").Length == 0
        };
    }
    public static object RuntimeIdentity() => new
    {
        runtime = RuntimeInformation.FrameworkDescription, runtimeVersion = Environment.Version.ToString(),
        architecture = RuntimeInformation.ProcessArchitecture.ToString(), os = RuntimeInformation.OSDescription,
        machine = Environment.MachineName, logicalCores = Environment.ProcessorCount, serverGC = GCSettings.IsServerGC,
        gcLatency = GCSettings.LatencyMode.ToString(), debugger = Debugger.IsAttached,
        settings = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(e => ((string)e.Key).StartsWith("DOTNET_", StringComparison.Ordinal) || ((string)e.Key).StartsWith("COMPlus_", StringComparison.Ordinal))
            .ToDictionary(e => (string)e.Key, e => (string?)e.Value)
    };

    public static void Child(Type type, string scenario, WorkloadEnvironment? environment = null, object? input = null, string? outputFingerprint = null)
    {
        // BDN executes generated Runnable_N subclasses. Attribute evidence to the declared benchmark.
        while(!type.IsDefined(typeof(ScenarioFamilyAttribute),inherit:false) && type.BaseType is {} parent)type=parent;
        string[] arguments=Environment.GetCommandLineArgs();int nameIndex=Array.IndexOf(arguments,"--benchmarkName");
        string? benchmarkName=nameIndex>=0 && nameIndex+1<arguments.Length ? arguments[nameIndex+1] : null;
        string directory = Environment.GetEnvironmentVariable("EDWINCRAFT_BENCHMARK_EVIDENCE") ?? throw new InvalidOperationException("Missing evidence output directory");
        Directory.CreateDirectory(directory);
        WriteJson(Path.Combine(directory, $"{type.Name}-{scenario}-{Environment.ProcessId}.json"), new
        {
            schemaVersion = 2, caseId = HarnessRunner.Id(type, scenario), benchmarkName, utc = DateTime.UtcNow,
            runtime = RuntimeIdentity(), dependencies = environment?.Identity(), input,outputFingerprint
        });
    }
}
