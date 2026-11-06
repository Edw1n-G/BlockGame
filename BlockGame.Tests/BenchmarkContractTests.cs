using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BlockGame.Benchmarking;
using BlockGame.Benchmarks;
using BlockGame.Workloads;

namespace BlockGame.Tests;

[ScenarioFamily("selection-full","control")]
public class DiscoveryFixture
{
    [Params("zero")] public string Scenario {get;set;}="zero";
    [Params(1,2)] public int Factor {get;set;}
    [Benchmark] public int First()=>Factor;
    [Benchmark] public int Second()=>Factor+1;
}

public class BenchmarkContractTests
{
    [Fact] public void DiscoveryIncludesMethodsAndAllParametersWithoutManualRegistration()
    {
        var cases=CaseDiscovery.Discover(typeof(DiscoveryFixture).Assembly,DefaultConfig.Instance,true);
        Assert.Equal(4,cases.Length);Assert.Equal(4,cases.Select(CaseDiscovery.Id).Distinct().Count());
        Assert.Equal(2,cases.Select(c=>CaseDiscovery.Contract(c,null).InputFingerprint).Distinct().Count());
        Assert.Empty(CaseDiscovery.Discover(typeof(DiscoveryFixture).Assembly,DefaultConfig.Instance,false));
    }
    [Fact] public void MovementContractContainsEveryTargetAndResizeRadius()
    {
        var contract=ScenarioContracts.Create("selection-movement","resize");
        var path=contract.Inputs.GetProperty("definition").GetProperty("path");
        Assert.Equal(2,path.GetArrayLength());Assert.Equal(24,path[0].GetProperty("horizontal").GetInt32());
        Assert.Equal(18,path[0].GetProperty("vertical").GetInt32());Assert.Equal(20,path[1].GetProperty("horizontal").GetInt32());
    }
    [Fact] public void FreshAndPathContractsNeverPair()
    {
        var fresh=ScenarioContracts.Create("selection-full","typical");var path=ScenarioContracts.Create("selection-movement","walk-x");
        Assert.NotEqual(fresh.Id,path.Id);Assert.NotEqual(fresh.Unit,path.Unit);Assert.NotEqual(fresh.InputFingerprint,path.InputFingerprint);
    }
    [Fact] public void CpuOnlyContractsDoNotRequireNativeContentAndEarlyReturnIsExplicit()
    {
        ScenarioContracts.Create("selection-full","typical").Validate();
        Assert.Equal("early-return",ScenarioContracts.Create("selection-movement","same").TimingClass);
        Assert.Throws<InvalidOperationException>(()=>ScenarioContracts.Create("mesh","floor"));
    }
    [Fact] public void CanonicalInputHashIgnoresPropertyOrder()
    {
        Assert.Equal(EvidenceJson.Fingerprint(new {x=1,y=2}),EvidenceJson.Fingerprint(new {y=2,x=1}));
    }
}
