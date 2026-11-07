using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using BlockGame.Workloads;
using BlockGame.Benchmarking;
using System.IO.Compression;

namespace BlockGame.Benchmarks;

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
        var files = Git(root,"ls-files", "-z", "--cached", "--others", "--exclude-standard").Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
            .Select(path => new { path, sha256 = File.Exists(Path.Combine(root, path)) ? WorkloadEnvironment.Hash(File.ReadAllBytes(Path.Combine(root, path))) : "DELETED" }).ToArray();
        string Fingerprint(string prefix) => WorkloadEnvironment.Hash(System.Text.Encoding.UTF8.GetBytes(string.Join("\n", files.Where(f => f.path.StartsWith(prefix, StringComparison.Ordinal)).Select(f => f.path + "=" + f.sha256))));
        string engineRoot=Path.GetFullPath(Environment.GetEnvironmentVariable("EDWINCRAFT_ENGINE_ROOT") ?? root);
        string engineRevision=Git(engineRoot,"rev-parse","HEAD");
        bool engineClean=Git(engineRoot,"status","--porcelain=v1","--untracked-files=all").Length==0;
        var engineFiles=Git(engineRoot,"ls-files","-z").Split('\0',StringSplitOptions.RemoveEmptyEntries).Order(StringComparer.Ordinal)
            .Select(path=>new {path,sha256=File.Exists(Path.Combine(engineRoot,path)) ? WorkloadEnvironment.Hash(File.ReadAllBytes(Path.Combine(engineRoot,path))) : "DELETED"}).ToArray();
        string engineFingerprint=EvidenceJson.Fingerprint(engineFiles);
        string assetsPath=Path.Combine(engineRoot,"BlockGame.Core/obj/project.assets.json");
        string[] managedDependencies=File.Exists(assetsPath) ? JsonDocument.Parse(File.ReadAllText(assetsPath)).RootElement.GetProperty("libraries").EnumerateObject().Select(p=>p.Name).Order(StringComparer.Ordinal).ToArray() : [];
        var runnerFiles=new[]{"global.json","scripts/run-benchmarks.ps1","scripts/verify-tests.ps1","Directory.Build.props","Directory.Build.targets","Directory.Packages.props","NuGet.Config"};
        var harnessFiles=files.Where(f=>runnerFiles.Contains(f.path) ||
            new[]{"BlockGame.Benchmarks/","BlockGame.Workloads/","BlockGame.Benchmarking/"}.Any(prefix=>f.path.StartsWith(prefix,StringComparison.Ordinal)) &&
            new[]{".cs",".csproj",".json"}.Contains(Path.GetExtension(f.path))).ToArray();
        return new { root, commit = Git(root,"rev-parse", "HEAD"), branch = Git(root,"branch", "--show-current"), dirty = status.Length > 0, status,
            engineRoot,engineRevision,engineClean,engineFingerprint,engineFiles,managedDependencies,harnessFingerprint=EvidenceJson.Fingerprint(harnessFiles),
            treeSha256 = Fingerprint(""), engineSourceSha256 = Fingerprint("BlockGame.Core/"), harnessSourceSha256 = Fingerprint("BlockGame.Benchmarks/"),
            workloadSourceSha256 = Fingerprint("BlockGame.Workloads/"), files };
    }

    public static void Snapshot(string output, object identity)
    {
        var source=JsonSerializer.SerializeToElement(identity);
        string root=source.GetProperty("root").GetString()!;
        using var zip=ZipFile.Open(Path.Combine(output,"source.zip"),ZipArchiveMode.Create);
        foreach(var file in source.GetProperty("files").EnumerateArray())
        {
            string path=file.GetProperty("path").GetString()!, hash=file.GetProperty("sha256").GetString()!;
            if(hash=="DELETED") continue;
            string absolute=Path.GetFullPath(Path.Combine(root,path));
            if(!absolute.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || WorkloadEnvironment.Hash(File.ReadAllBytes(absolute))!=hash)
                throw new InvalidDataException("Source changed or escaped archive root: "+path);
            ZipFileExtensions.CreateEntryFromFile(zip,absolute,path,CompressionLevel.Optimal);
        }
        string engineRoot=source.GetProperty("engineRoot").GetString()!;
        // Clean revision source is independently portable, even when harness and engine are sibling checkouts.
        if(engineRoot!=root)
        {
            foreach(var file in source.GetProperty("engineFiles").EnumerateArray())
            {
                string path=file.GetProperty("path").GetString()!,hash=file.GetProperty("sha256").GetString()!;
                if(hash=="DELETED")continue;
                string absolute=Path.GetFullPath(Path.Combine(engineRoot,path));
                if(!absolute.StartsWith(engineRoot+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || WorkloadEnvironment.Hash(File.ReadAllBytes(absolute))!=hash)throw new InvalidDataException("Engine source changed while archiving");
                ZipFileExtensions.CreateEntryFromFile(zip,absolute,"engine/"+path,CompressionLevel.Optimal);
            }
        }
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
        assemblies = new[] { typeof(Basics.Game.Logic.TerrainManaging.ChunkProvider).Assembly, typeof(WorkloadCatalog).Assembly, typeof(ScenarioContract).Assembly, typeof(Program).Assembly }
            .Select(a => new { name = a.GetName().Name, sha256 = WorkloadEnvironment.Hash(File.ReadAllBytes(a.Location)) }).ToArray()
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
            schemaVersion = 2, caseId = Program.Id(type, scenario), benchmarkName, utc = DateTime.UtcNow,
            runtime = RuntimeIdentity(), dependencies = environment?.Identity(), input,outputFingerprint
        });
    }
}
