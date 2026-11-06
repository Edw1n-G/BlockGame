using System.Diagnostics;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BlockGame.Workloads;

namespace BlockGame.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        string? manifestPath = null;
        Dictionary<string, object?>? manifest = null;
        try
        {
            string mode = "Smoke", output = ""; bool list = false;
            List<string> filters = new();
            for (int i = 0; i < args.Length; i++)
                switch (args[i])
                {
                    case "--mode": mode = args[++i]; break;
                    case "--case": filters.Add(args[++i]); break;
                    case "--output": output = args[++i]; break;
                    case "--list": list = true; break;
                    default: throw new ArgumentException($"Unknown argument: {args[i]}");
                }
            if (mode is not ("Smoke" or "Measure")) throw new ArgumentException("Mode must be Smoke or Measure");
            var all = new Dictionary<Type, IEnumerable<string>> {
                [typeof(MeshingBenchmarks)] = WorkloadCatalog.MeshCases,
                [typeof(TerrainBenchmarks)] = WorkloadCatalog.TerrainCases,
                [typeof(NoiseBenchmarks)] = WorkloadCatalog.NoiseCases,
                [typeof(SelectionBenchmarks)] = WorkloadCatalog.SelectionCases,
                [typeof(SelectionRebuildBenchmarks)] = SelectionMovementWorkload.Cases,
                [typeof(SelectionDeltaBenchmarks)] = SelectionMovementWorkload.Cases,
                [typeof(SelectionOriginalBenchmarks)] = WorkloadCatalog.SelectionCases };
            if (filters.Count == 0) filters.Add("*");
            var patterns = filters.Select(filter => new Regex("^" + Regex.Escape(filter).Replace("\\*", ".*").Replace("\\?", ".") + "$", RegexOptions.CultureInvariant)).ToArray();
            var ids = all.SelectMany(kv => kv.Value.Select(s => Id(kv.Key,s))).ToArray();
            for (int i = 0; i < patterns.Length; i++)
                if (!ids.Any(id => patterns[i].IsMatch(id))) throw new ArgumentException($"No benchmark cases match '{filters[i]}'");
            bool Selected(string id) => patterns.Any(pattern => pattern.IsMatch(id));
            var selected = ids.Where(Selected).ToArray();
            if (list) { foreach (string id in selected) Console.WriteLine(id); return 0; }
#if DEBUG
            bool debugBuild = true;
#else
            bool debugBuild = false;
#endif
            if (debugBuild) throw new InvalidOperationException("Benchmark execution requires a Release build");
            if (Debugger.IsAttached) throw new InvalidOperationException("Detach debugger before benchmarking");
            if (string.IsNullOrWhiteSpace(output)) throw new ArgumentException("--output is required for execution");
            output = Path.GetFullPath(output);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Output must be a new/empty directory");
            Directory.CreateDirectory(output);
            manifestPath = Path.Combine(output, "manifest.json");
            object dependencies;
            using (var env = new WorkloadEnvironment()) { dependencies = env.Identity(); }
            string dotnet = Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(), "../../..", "dotnet.exe"));
            var toolchain = CsProjCoreToolchain.From(new NetCoreAppSettings("net10.0", null, ".NET 10", customDotNetCliPath: dotnet));
            var job = (mode == "Smoke" ? Job.Dry : Job.Default).WithId(mode).WithPlatform(Platform.X64).WithJit(Jit.RyuJit).WithGcServer(false).WithToolchain(toolchain).DontEnforcePowerPlan()
                .WithEnvironmentVariable("EDWINCRAFT_BENCHMARK_EVIDENCE", Path.Combine(output, "children"));
            manifest = new()
            {
                ["schemaVersion"] = 1, ["runId"] = Guid.NewGuid().ToString("N"), ["startedUtc"] = DateTime.UtcNow,
                ["status"] = "running", ["mode"] = mode, ["suitableForComparison"] = false,
                ["source"] = RunEvidence.SourceIdentity(), ["host"] = RunEvidence.RuntimeIdentity(), ["dependencies"] = dependencies,
                ["workloadVersion"] = WorkloadCatalog.Version, ["commandArguments"] = args, ["selectedCases"] = selected,
                ["benchmarkDotNetVersion"] = typeof(BenchmarkSwitcher).Assembly.GetName().Version?.ToString(),
                ["job"] = new { mode, targetFramework = "net10.0", dotnet, platform = "X64", jit = "RyuJit", serverGC = false, powerPlan = "preserve current", sampling = mode == "Smoke" ? "Dry, not a baseline" : "BenchmarkDotNet defaults" },
                ["scope"] = "single-threaded CPU; managed allocation only; operation unit is one call, meshing includes returning buffers",
                ["preflight"] = "External correctness evidence required: see wrapper verification/result.json",
                ["loadNote"] = Environment.GetEnvironmentVariable("EDWINCRAFT_BENCHMARK_LOAD_NOTE") ?? "Uncontrolled desktop session; competing load not excluded",
                ["powerPlan"] = Environment.GetEnvironmentVariable("EDWINCRAFT_BENCHMARK_POWER_PLAN") ?? "unavailable",
                ["rawArtifacts"] = new[] { "results/*-report-full.json", "results/*-measurements.csv", "children/*.json", "*.log" }
            };
            RunEvidence.WriteJson(manifestPath, manifest);
            var config = DefaultConfig.Instance.WithArtifactsPath(output)
                .AddJob(job).AddDiagnoser(MemoryDiagnoser.Default)
                .AddExporter(JsonExporter.Full, CsvMeasurementsExporter.Default)
                .AddFilter(new SimpleFilter(c => Selected(Id(c.Descriptor.Type, (string)c.Parameters.Items.Single().Value))));
            var summaries = BenchmarkSwitcher.FromTypes(all.Keys.ToArray()).Run(["--filter", "*"], config);
            var reports = summaries.SelectMany(s => s.Reports).ToArray();
            if (reports.Length != selected.Length || reports.Any(r => !r.Success || r.ResultStatistics is null))
                throw new InvalidOperationException($"Incomplete run: {reports.Count(r => r.Success)}/{selected.Length} successful cases");
            var actual = reports.Select(r => Id(r.BenchmarkCase.Descriptor.Type, (string)r.BenchmarkCase.Parameters.Items.Single().Value)).Order(StringComparer.Ordinal).ToArray();
            if (!actual.SequenceEqual(selected.Order(StringComparer.Ordinal))) throw new InvalidOperationException("Executed case identities do not match selection");
            object finalSource = RunEvidence.SourceIdentity();
            string? InitialHash() => System.Text.Json.JsonSerializer.SerializeToElement(manifest["source"]).GetProperty("treeSha256").GetString();
            string? finalHash = System.Text.Json.JsonSerializer.SerializeToElement(finalSource).GetProperty("treeSha256").GetString();
            if (InitialHash() != finalHash) throw new InvalidOperationException("Source tree changed during measurement; rerun from stable inputs");
            manifest["sourceUnchangedDuringRun"] = true;
            manifest["status"] = "completed"; manifest["finishedUtc"] = DateTime.UtcNow;
            manifest["completedCases"] = actual;
            manifest["warnings"] = summaries.SelectMany(s => s.ValidationErrors.Select(v => v.Message)).ToArray();
            RunEvidence.WriteJson(manifestPath, manifest);
            return 0;
        }
        catch (Exception ex)
        {
            if (manifestPath is not null && manifest is not null) { manifest["status"] = "failed"; manifest["error"] = ex.Message; RunEvidence.WriteJson(manifestPath, manifest); }
            Console.Error.WriteLine(ex); return 1;
        }
    }

    public static string Id(Type type, string scenario) => $"{type.Name.Replace("Benchmarks", "", StringComparison.Ordinal).ToLowerInvariant()}/{scenario}/v{WorkloadCatalog.Version}";
}
