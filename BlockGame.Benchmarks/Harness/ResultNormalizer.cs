using BlockGame.Benchmarks.Comparison;
using System.Text.Json;
using BenchmarkDotNet.Running;

namespace BlockGame.Benchmarks.Harness;

internal static class ResultNormalizer
{
    public static BenchmarkRun Create(string output, BenchmarkCase[] selected, object source, object? dependencies, string mode)
    {
        var identity=JsonSerializer.SerializeToElement(source);
        var records=new List<Observation>(); JsonElement? environment=null;
        var raw=Directory.EnumerateFiles(Path.Combine(output,"results"),"*full.json").Select(path=>JsonDocument.Parse(File.ReadAllText(path))).ToArray();
        try
        {
            foreach(var c in selected)
            {
                var file=raw.Single(doc=>doc.RootElement.GetProperty("Benchmarks").EnumerateArray().Any(r=>r.GetProperty("Type").GetString()==c.Descriptor.Type.Name));
                var row=file.RootElement.GetProperty("Benchmarks").EnumerateArray().Single(r=>r.GetProperty("Type").GetString()==c.Descriptor.Type.Name &&
                    r.GetProperty("Method").GetString()==c.Descriptor.WorkloadMethod.Name && r.GetProperty("Parameters").GetString()==c.Parameters.PrintInfo);
                var stats=row.GetProperty("Statistics");
                double[] samples=stats.GetProperty("OriginalValues").EnumerateArray().Select(v=>v.GetDouble()).ToArray();
                var diagnostics=new List<string>();
                // BDN uses Perfolizer's m-value; retain the affected case explicitly.
                if(Perfolizer.Mathematics.Multimodality.MValueCalculator.Calculate(samples)>2.8) diagnostics.Add("MultimodalDistribution");
                double? bytes=row.TryGetProperty("Memory",out var memory) && memory.TryGetProperty("BytesAllocatedPerOperation",out var allocation) && allocation.ValueKind==JsonValueKind.Number ? allocation.GetDouble() : null;
                var outputs=Directory.EnumerateFiles(Path.Combine(output,"children"),"*.json")
                    .Select(p=>JsonDocument.Parse(File.ReadAllText(p))).ToArray();
                string? outputHash;object? childRuntime=null;
                try {
                    var matching=outputs.Where(d=>d.RootElement.GetProperty("benchmarkName").GetString()==row.GetProperty("FullName").GetString()).ToArray();
                    outputHash=matching.Select(d=>d.RootElement.GetProperty("outputFingerprint").GetString()).Distinct().SingleOrDefault();
                    var actual=matching.Select(d=>d.RootElement.GetProperty("runtime")).Select(r=>new {
                        runtime=r.GetProperty("runtime").GetString(),architecture=r.GetProperty("architecture").GetString(),
                        serverGC=r.GetProperty("serverGC").GetBoolean(),latency=r.GetProperty("gcLatency").GetString(),debugger=r.GetProperty("debugger").GetBoolean()
                    }).ToArray();
                    if(actual.Any(r=>r.debugger) || actual.Select(EvidenceJson.Fingerprint).Distinct().Count()>1)throw new InvalidDataException("Child runtime/debugger mismatch");
                    childRuntime=actual.FirstOrDefault();
                }
                finally {foreach(var d in outputs)d.Dispose();}
                if(CaseDiscovery.Family(c.Descriptor.Type).Kind=="engine" && outputHash is null)throw new InvalidDataException("Missing case-attributed output evidence: "+CaseDiscovery.Id(c));
                records.Add(new(CaseDiscovery.Id(c),CaseDiscovery.Contract(c,dependencies),c.Descriptor.Type.FullName+"."+c.Descriptor.WorkloadMethod.Name,samples,bytes,diagnostics.ToArray(),OutputFingerprint:outputHash));
                var host=file.RootElement.GetProperty("HostEnvironmentInfo");
                var env=JsonSerializer.SerializeToElement(new {
                    cpu=host.GetProperty("ProcessorName").GetString(),os=host.GetProperty("OsVersion").GetString(),runtime=host.GetProperty("RuntimeVersion").GetString(),
                    architecture=host.GetProperty("Architecture").GetString(),sdk=host.GetProperty("DotNetCliVersion").GetString(),childRuntime,
                    logicalCores=host.GetProperty("LogicalCoreCount"),physicalCores=host.GetProperty("PhysicalCoreCount"),
                    machine=Environment.MachineName,bdn=host.GetProperty("BenchmarkDotNetVersion").GetString(),
                    intrinsics=row.GetProperty("HardwareIntrinsics").GetString(), gc="concurrent workstation",jit="RyuJit",dependencies,
                    settings=Environment.GetEnvironmentVariables().Cast<System.Collections.DictionaryEntry>().Where(e=>((string)e.Key).StartsWith("COMPlus_",StringComparison.Ordinal) || ((string)e.Key).StartsWith("DOTNET_",StringComparison.Ordinal) && (string)e.Key!="DOTNET_HOST_PATH")
                        .OrderBy(e=>(string)e.Key,StringComparer.Ordinal).ToDictionary(e=>(string)e.Key,e=>e.Value)
                });
                if(environment is { } previous && EvidenceJson.Fingerprint(previous)!=EvidenceJson.Fingerprint(env)) throw new InvalidDataException("Child environment differs across cases");
                environment=env;
            }
            bool controls=selected.Any(c=>CaseDiscovery.Family(c.Descriptor.Type).Kind!="engine");
            return new BenchmarkRun(BenchmarkRun.CurrentSchemaVersion, controls ? "calibration" : "revision", mode,
                identity.GetProperty("engineRevision").GetString()!, identity.GetProperty("engineClean").GetBoolean(),
                identity.GetProperty("harnessTree").GetString()!, EvidenceJson.Fingerprint(environment!.Value), records.ToArray(),
                controls ? "Control/model evidence" : "Manual revision comparison") { EnvironmentInfo = environment };
        }
        finally {foreach(var doc in raw)doc.Dispose();}
    }
}
