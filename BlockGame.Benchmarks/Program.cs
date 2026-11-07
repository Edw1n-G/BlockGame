using BlockGame.Benchmarking;
using BlockGame.Workloads;
using System.Diagnostics;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Exporters.Csv;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;

namespace BlockGame.Benchmarks;

public static class Program
{
    public static int Main(string[] args)
    {
        string? manifestPath=null; Dictionary<string,object?>? manifest=null;
        try
        {
            string mode="Smoke",output=""; bool list=false,includeControls=false,preflightOnly=false,listJson=false;
            List<string> filters=new();
            for(int i=0;i<args.Length;i++)
                switch(args[i])
                {
                    case "--mode": mode=args[++i];break;
                    case "--case": filters.Add(args[++i]);break;
                    case "--output": output=args[++i];break;
                    case "--list": list=true;break;
                    case "--list-json": listJson=true;break;
                    case "--include-controls": includeControls=true;break;
                    case "--preflight-only": preflightOnly=true;break;
                    default: throw new ArgumentException("Unknown argument: "+args[i]);
                }
            if(mode is not ("Smoke" or "Measure")) throw new ArgumentException("Mode must be Smoke or Measure");
            var selected=CaseDiscovery.Select(CaseDiscovery.Discover(typeof(Program).Assembly,DefaultConfig.Instance,includeControls),filters.ToArray());
            if(list || listJson)
            {
                if(listJson) Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(selected.Select(c=>new {
                    caseId=CaseDiscovery.Id(c),family=CaseDiscovery.Family(c.Descriptor.Type).Family,kind=CaseDiscovery.Family(c.Descriptor.Type).Kind,
                    scenario=CaseDiscovery.Scenario(c),method=c.Descriptor.WorkloadMethod.Name,parameters=c.Parameters.DisplayInfo,needsNative=ScenarioContracts.NeedsNative(CaseDiscovery.Family(c.Descriptor.Type).Family)
                }),EvidenceJson.Options));
                else foreach(var c in selected)Console.WriteLine(CaseDiscovery.Id(c));
                return 0;
            }
#if DEBUG
            bool debugBuild=true;
#else
            bool debugBuild=false;
#endif
            if(debugBuild)throw new InvalidOperationException("Benchmark execution requires a Release build");
            if(Debugger.IsAttached)throw new InvalidOperationException("Detach debugger before benchmarking");
            object? dependencies=null;
            if(selected.Any(c=>ScenarioContracts.NeedsNative(CaseDiscovery.Family(c.Descriptor.Type).Family)))
                using(var environment=new WorkloadEnvironment())dependencies=environment.Identity();
            foreach(var c in selected)CaseDiscovery.Contract(c,dependencies).Validate();
            if(preflightOnly){Console.WriteLine("Family dependency/contract preflight passed");return 0;}
            if(string.IsNullOrWhiteSpace(output))throw new ArgumentException("--output is required");
            output=Path.GetFullPath(output);
            if(Directory.Exists(output)&&Directory.EnumerateFileSystemEntries(output).Any())throw new IOException("Output must be a new/empty directory");
            Directory.CreateDirectory(output);manifestPath=Path.Combine(output,"manifest.json");
            object source=RunEvidence.SourceIdentity();
            RunEvidence.Snapshot(output,source);
            string dotnet=Path.GetFullPath(Path.Combine(System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory(),"../../..","dotnet.exe"));
            var toolchain=CsProjCoreToolchain.From(new NetCoreAppSettings("net10.0",null,".NET 10",customDotNetCliPath:dotnet));
            var job=(mode=="Smoke"?Job.Dry:Job.Default).WithId(mode).WithPlatform(Platform.X64).WithJit(Jit.RyuJit).WithGcServer(false)
                .WithToolchain(toolchain).DontEnforcePowerPlan().WithEnvironmentVariable("EDWINCRAFT_BENCHMARK_EVIDENCE",Path.Combine(output,"children"));
            manifest=new(){["schemaVersion"]=2,["status"]="running",["mode"]=mode,["source"]=source,["dependencies"]=dependencies,
                ["startedUtc"]=DateTime.UtcNow,["selectedCases"]=selected.Select(CaseDiscovery.Id).ToArray(),["commandArguments"]=args,
                ["contracts"]=selected.Select(c=>new {caseId=CaseDiscovery.Id(c),contract=CaseDiscovery.Contract(c,dependencies),kind=CaseDiscovery.Family(c.Descriptor.Type).Kind}).ToArray(),
                ["host"]=RunEvidence.RuntimeIdentity(),["suitableForComparison"]=false,["powerPlan"]=Environment.GetEnvironmentVariable("EDWINCRAFT_BENCHMARK_POWER_PLAN"),
                ["loadNote"]=Environment.GetEnvironmentVariable("EDWINCRAFT_BENCHMARK_LOAD_NOTE")??"Uncontrolled desktop session",
                ["job"]=new {mode,dotnet,configuration="Release",platform="X64",jit="RyuJit",serverGC=false,powerPlan="preserve current",sampling=mode=="Smoke"?"Dry":"BenchmarkDotNet defaults"}};
            RunEvidence.WriteJson(manifestPath,manifest);
            var selectedIds=selected.Select(CaseDiscovery.Id).ToHashSet(StringComparer.Ordinal);
            var config=DefaultConfig.Instance.WithArtifactsPath(output).AddJob(job).AddDiagnoser(MemoryDiagnoser.Default)
                .AddExporter(JsonExporter.Full,CsvMeasurementsExporter.Default).AddFilter(new SimpleFilter(c=>selectedIds.Contains(CaseDiscovery.Id(c))));
            var summaries=BenchmarkSwitcher.FromTypes(selected.Select(c=>c.Descriptor.Type).Distinct().ToArray()).Run(["--filter","*"],config);
            var reports=summaries.SelectMany(s=>s.Reports).ToArray();
            if(reports.Length!=selected.Length || reports.Any(r=>!r.Success || r.ResultStatistics is null) ||
                !reports.Select(r=>CaseDiscovery.Id(r.BenchmarkCase)).ToHashSet().SetEquals(selectedIds))
                throw new InvalidOperationException("Incomplete/failed benchmark selection");
            object finalSource=RunEvidence.SourceIdentity();
            string Fingerprint(object value)=>EvidenceJson.Fingerprint(value);
            if(Fingerprint(source)!=Fingerprint(finalSource))throw new InvalidOperationException("Source changed during measurement");
            var normalized=ResultNormalizer.Create(output,selected,source,dependencies,mode);
            EvidenceJson.Write(Path.Combine(output,"benchmark-run.json"),normalized);
            manifest["status"]="completed";manifest["finishedUtc"]=DateTime.UtcNow;manifest["sourceUnchangedDuringRun"]=true;
            manifest["completedCases"]=reports.Select(r=>CaseDiscovery.Id(r.BenchmarkCase)).ToArray();
            manifest["validationMessages"]=summaries.SelectMany(s=>s.ValidationErrors.Select(v=>v.Message)).ToArray();
            manifest["statisticalDiagnostics"]=normalized.Cases.Where(c=>c.Diagnostics.Length>0).Select(c=>new {c.CaseId,c.Diagnostics}).ToArray();
            RunEvidence.WriteJson(manifestPath,manifest);
            return 0;
        }
        catch(Exception ex)
        {
            if(manifestPath is not null && manifest is not null){manifest["status"]="failed";manifest["error"]=ex.Message;RunEvidence.WriteJson(manifestPath,manifest);}
            Console.Error.WriteLine(ex);return 1;
        }
    }
    // Case identity is informational. Pairing uses ScenarioContract, never this type/method label.
    public static string Id(Type type,string scenario)=>type.Name.Replace("Benchmarks","",StringComparison.Ordinal).ToLowerInvariant()+"/"+scenario+"/v1";
}
