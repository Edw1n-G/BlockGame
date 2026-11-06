using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using BlockGame.Workloads;

namespace BlockGame.Benchmarks;

internal static class RunEvidence
{
    internal static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static void WriteJson(string path, object value) => File.WriteAllText(path, JsonSerializer.Serialize(value, Json));

    public static object SourceIdentity()
    {
        string Git(params string[] args)
        {
            var start = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
            foreach (var arg in args) start.ArgumentList.Add(arg);
            using var p = Process.Start(start)!;
            var stdout = p.StandardOutput.ReadToEndAsync(); var stderr = p.StandardError.ReadToEndAsync();
            if (!p.WaitForExit(30_000)) { p.Kill(true); throw new TimeoutException("Git evidence timed out"); }
            if (p.ExitCode != 0) throw new InvalidOperationException("Cannot identify source: " + stderr.Result);
            return stdout.Result.TrimEnd('\r', '\n');
        }
        string root = Git("rev-parse", "--show-toplevel");
        string status = Git("status", "--porcelain=v1", "--untracked-files=all");
        var files = Git("ls-files", "-z", "--cached", "--others", "--exclude-standard").Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(path => new { path, sha256 = File.Exists(Path.Combine(root, path)) ? WorkloadEnvironment.Hash(File.ReadAllBytes(Path.Combine(root, path))) : "DELETED" }).ToArray();
        string Fingerprint(string prefix) => WorkloadEnvironment.Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", files.Where(f => f.path.StartsWith(prefix, StringComparison.Ordinal)).Select(f => f.path + "=" + f.sha256))));
        return new { commit = Git("rev-parse", "HEAD"), branch = Git("branch", "--show-current"), dirty = status.Length > 0, status,
            treeSha256 = Fingerprint(""), engineSourceSha256 = Fingerprint("BlockGame/"), harnessSourceSha256 = Fingerprint("BlockGame.Benchmarks/"),
            workloadSourceSha256 = Fingerprint("BlockGame.Workloads/"), files };
    }

    public static object RuntimeIdentity() => new
    {
        runtime = RuntimeInformation.FrameworkDescription, runtimeVersion = Environment.Version.ToString(),
        architecture = RuntimeInformation.ProcessArchitecture.ToString(), os = RuntimeInformation.OSDescription,
        machine = Environment.MachineName, logicalCores = Environment.ProcessorCount, serverGC = GCSettings.IsServerGC,
        gcLatency = GCSettings.LatencyMode.ToString(), debugger = Debugger.IsAttached,
        settings = Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>()
            .Where(e => ((string)e.Key).StartsWith("DOTNET_", StringComparison.Ordinal) || ((string)e.Key).StartsWith("COMPlus_", StringComparison.Ordinal))
            .ToDictionary(e => (string)e.Key, e => (string?)e.Value),
        assemblies = new[] { typeof(Basics.Game.Logic.TerrainManaging.ChunkProvider).Assembly, typeof(WorkloadCatalog).Assembly, typeof(Program).Assembly }
            .Select(a => new { name = a.GetName().Name, sha256 = WorkloadEnvironment.Hash(File.ReadAllBytes(a.Location)) }).ToArray()
    };

    public static void Child(Type type, string scenario, WorkloadEnvironment? environment = null, object? input = null)
    {
        string directory = Environment.GetEnvironmentVariable("EDWINCRAFT_BENCHMARK_EVIDENCE") ?? throw new InvalidOperationException("Missing evidence output directory");
        Directory.CreateDirectory(directory);
        WriteJson(Path.Combine(directory, $"{type.Name}-{scenario}-{Environment.ProcessId}.json"), new
        {
            schemaVersion = 1, caseId = Program.Id(type, scenario), utc = DateTime.UtcNow,
            runtime = RuntimeIdentity(), dependencies = environment?.Identity(), input
        });
    }
}
