using BlockGame.Benchmarks.Comparison;
using BlockGame.Benchmarks.Workloads;
using System.Reflection;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

namespace BlockGame.Benchmarks.Harness;

public static class CaseDiscovery
{
    public static BenchmarkCase[] Discover(Assembly assembly, IConfig config, bool includeControls) => assembly.GetTypes()
        .Where(t=>t.IsPublic && !t.IsAbstract && t.GetMethods().Any(m=>m.GetCustomAttribute<BenchmarkAttribute>() is not null))
        .Where(t=>Family(t).Kind=="engine" || includeControls)
        .SelectMany(t=>BenchmarkConverter.TypeToBenchmarks(t,config).BenchmarksCases).ToArray();
    public static ScenarioFamilyAttribute Family(Type type) => type.GetCustomAttribute<ScenarioFamilyAttribute>()
        ?? throw new InvalidOperationException("Benchmark type needs a scenario family: "+type.Name);
    public static string Scenario(BenchmarkCase c) => (string)c.Parameters.Items.Single(p=>p.Name=="Scenario").Value;
    public static string Id(BenchmarkCase c) => Program.Id(c.Descriptor.Type,Scenario(c))+"/"+c.Descriptor.WorkloadMethod.Name+"/"+
        EvidenceJson.Fingerprint(c.Parameters.Items.ToDictionary(p=>p.Name,p=>p.Value))[..12];
    public static ScenarioContract Contract(BenchmarkCase c, object? dependencies) => ScenarioContracts.Create(Family(c.Descriptor.Type).Family,Scenario(c),dependencies,
        c.Parameters.Items.Where(p=>p.Name!="Scenario").OrderBy(p=>p.Name,StringComparer.Ordinal).ToDictionary(p=>p.Name,p=>p.Value));
    public static BenchmarkCase[] Select(BenchmarkCase[] cases, string[] filters)
    {
        if(filters.Length==0) filters=["*"];
        var patterns=filters.Select(f=>new Regex("^"+Regex.Escape(f).Replace("\\*",".*").Replace("\\?",".")+"$",RegexOptions.CultureInvariant)).ToArray();
        for(int i=0;i<patterns.Length;i++) if(!cases.Any(c=>patterns[i].IsMatch(Id(c)))) throw new ArgumentException("No benchmark cases match '"+filters[i]+"'");
        return cases.Where(c=>patterns.Any(p=>p.IsMatch(Id(c)))).ToArray();
    }
}
