using BlockGame.Benchmarking;

namespace BlockGame.Tests;

public class BenchmarkComparatorTests
{
    private static BenchmarkRun Fixture(string name) => EvidenceJson.Read<BenchmarkRun>(Path.Combine(AppContext.BaseDirectory,"TestData/BenchmarkControls",name,"benchmark-run.json"));
    [Fact] public void IdenticalCodeNullControlNeverClaimsImprovementOrRegression()
    {
        var rows=Comparator.Compare(Fixture("null-a"),Fixture("null-b"),calibration:true).Rows;
        Assert.Equal(21,rows.Length); Assert.All(rows,r=>Assert.Equal("inconclusive",r.Status));
    }
    [Fact] public void PositiveControlDetectsLargeEffectButIsNotAProductionBaseline()
    {
        var a=Fixture("positive-before"); var b=Fixture("positive-after");
        var row=Assert.Single(Comparator.Compare(a,b,calibration:true).Rows);
        Assert.Equal("improved",row.Status); Assert.InRange(row.PercentChange!.Value,-98,-96);
        Assert.Equal("not comparable",Assert.Single(Comparator.Compare(a,b).Rows).Status);
    }
    [Fact] public void NoisyOutsideMapControlsAreInconclusive()
    {
        var rows=Comparator.Compare(Fixture("null-a"),Fixture("null-b"),calibration:true).Rows.Where(r=>r.ScenarioId.Contains("outside-"));
        Assert.Equal(2,rows.Count()); Assert.All(rows,r=> {Assert.Equal("inconclusive",r.Status);Assert.Null(r.Ratio);Assert.Null(r.PercentChange);});
    }
    [Fact] public void EarlyReturnTo28NsDoesNotProduce9999PercentHeadline()
    {
        var row=Assert.Single(Comparator.Compare(Fixture("early-before"),Fixture("early-after"),calibration:true).Rows);
        Assert.Equal("inconclusive",row.Status); Assert.Null(row.Ratio); Assert.Null(row.PercentChange); Assert.NotNull(row.AbsoluteDeltaNs);
    }
    [Fact] public void ScenarioIdentityDoesNotDependOnTypeOrMethodNames()
    {
        var a=Fixture("positive-before"); var b=Fixture("positive-after");
        Assert.NotEqual(a.Cases[0].CaseId,b.Cases[0].CaseId);
        Assert.Equal("improved",Assert.Single(Comparator.Compare(a,b,calibration:true).Rows).Status);
    }
    [Fact] public void ReversedPositiveControlDetectsRegressionAndAllocationIncrease()
    {
        var row=Assert.Single(Comparator.Compare(Fixture("positive-after"),Fixture("positive-before"),calibration:true).Rows);
        Assert.Equal("regressed",row.Status);Assert.Equal("increased",row.AllocationStatus);
    }
    [Fact] public void MeasuredDeliberateSlowdownIsANegativeControl()
    {
        var row=Assert.Single(Comparator.Compare(Fixture("negative-before"),Fixture("negative-after"),calibration:true).Rows);
        Assert.Equal("regressed",row.Status);Assert.True(row.PercentChange>50);Assert.Equal("increased",row.AllocationStatus);
    }
    [Fact] public void AllCommittedCalibrationEvidenceHasValidPortableHashes()
    {
        string root=Path.Combine(AppContext.BaseDirectory,"TestData/BenchmarkControls");
        foreach(string directory in Directory.EnumerateDirectories(root))Assert.Equal("calibration",PortableBundle.Verify(directory).Kind);
    }
    [Fact] public void PackagedControlSuiteValidatesBeforeProductionComparisons()
    { Assert.NotNull(ControlValidation.Validate(Path.Combine(AppContext.BaseDirectory,"TestData/BenchmarkControls"))); }
    [Fact] public void ProductionVerdictRequiresFullCorrectnessAndMatchingOutputEvidence()
    {
        var a=Fixture("positive-before") with {Kind="revision",EngineClean=true,EngineRevision=new string('a',40)};
        var b=Fixture("positive-after") with {Kind="revision",EngineClean=true,EngineRevision=new string('b',40)};
        Assert.Equal("not comparable",Assert.Single(Comparator.Compare(a,b).Rows).Status);
        a=a with {Cases=[a.Cases[0] with {OutputFingerprint="same-output"}]};
        b=b with {Cases=[b.Cases[0] with {OutputFingerprint="same-output"}]};
        Assert.Equal("improved",Assert.Single(Comparator.Compare(a,b).Rows).Status);
        Assert.Equal("not comparable",Assert.Single(Comparator.Compare(a,b with {CorrectnessScope="family"}).Rows).Status);
        Assert.Equal("not comparable",Assert.Single(Comparator.Compare(a,b with {Cases=[b.Cases[0] with {OutputFingerprint="changed-output"}]}).Rows).Status);
    }
    [Theory]
    [InlineData("unit")][InlineData("version")][InlineData("input")][InlineData("environment")][InlineData("harness")]
    [InlineData("missing")][InlineData("failed")][InlineData("smoke")][InlineData("duplicate")]
    public void IncompatibleEvidenceCannotWin(string mutation)
    {
        var a=Fixture("positive-before"); var b=Fixture("positive-after"); var c=b.Cases[0];
        b=mutation switch {
            "unit"=>b with {Cases=[c with {Scenario=c.Scenario with {Unit="fresh-set"}}]},
            "version"=>b with {Cases=[c with {Scenario=c.Scenario with {Version=2}}]},
            "input"=>b with {Cases=[c with {Scenario=ScenarioContract.Create(c.Scenario.Id,1,c.Scenario.Unit,new {different=true})}]},
            "environment"=>b with {EnvironmentFingerprint="another machine"},
            "harness"=>b with {HarnessFingerprint="another harness"},
            "missing"=>b with {Cases=[]}, "failed"=>b with {CorrectnessPassed=false}, "smoke"=>b with {Mode="Smoke"},
            "duplicate"=>b with {Cases=[c,c]}, _=>throw new Exception()};
        Assert.All(Comparator.Compare(a,b,calibration:true).Rows,r=>Assert.Equal("not comparable",r.Status));
    }
    [Fact] public void NearZeroAndZeroAllocationAreHandledSeparately()
    {
        var a=Fixture("positive-before"); var b=Fixture("positive-after");
        a=a with {Cases=[a.Cases[0] with {SamplesNs=Enumerable.Repeat(0d,15).ToArray(),BytesPerOperation=0}]};
        b=b with {Cases=[b.Cases[0] with {BytesPerOperation=0}]};
        var row=Assert.Single(Comparator.Compare(a,b,calibration:true).Rows);
        Assert.Equal("inconclusive",row.Status); Assert.Null(row.Ratio);Assert.Null(row.PercentChange);Assert.Equal(0,row.AllocationDeltaBytes);
    }
    [Fact] public void MissingAllocationIsUnavailableRatherThanZero()
    {
        var a=Fixture("positive-before"); var b=Fixture("positive-after"); b=b with {Cases=[b.Cases[0] with {BytesPerOperation=null}]};
        var row=Assert.Single(Comparator.Compare(a,b,calibration:true).Rows);
        Assert.Null(row.AllocationDeltaBytes);Assert.Equal("unavailable",row.AllocationStatus);
    }
    [Fact] public void StatisticalWarningsAndNoiseSuppressVerdicts()
    {
        var a=Fixture("positive-before"); var b=Fixture("positive-after");
        foreach (var observation in new[]{b.Cases[0] with {Diagnostics=["MultimodalDistribution"]},b.Cases[0] with {SamplesNs=Enumerable.Range(0,20).Select(i=>i%2==0?1d:100000d).ToArray()}})
            Assert.Equal("inconclusive",Assert.Single(Comparator.Compare(a,b with {Cases=[observation]},calibration:true).Rows).Status);
    }
    [Fact] public void PortableBundleSurvivesRelocationAndRejectsTampering()
    {
        string root=Path.Combine(Path.GetTempPath(),"benchmark-bundle-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            string source=Path.Combine(root,"source"); Directory.CreateDirectory(source);
            EvidenceJson.Write(Path.Combine(source,"benchmark-run.json"),Fixture("positive-before"));
            File.WriteAllText(Path.Combine(source,"summary.md"),"Portable evidence");
            string zip=Path.Combine(root,"evidence.zip");PortableBundle.Pack(source,zip);
            string moved=Path.Combine(root,"moved.zip");File.Copy(zip,moved);
            Assert.Equal("calibration",PortableBundle.Verify(moved).Kind);
            File.AppendAllText(Path.Combine(source,"summary.md"),"tampered");
            Assert.Throws<InvalidDataException>(()=>PortableBundle.Verify(source));
        }
        finally {
            string expected=Path.Combine(Path.GetTempPath(),Path.GetFileName(root));
            if(Path.GetFullPath(root)!=Path.GetFullPath(expected) || !Path.GetFileName(root).StartsWith("benchmark-bundle-test-",StringComparison.Ordinal))throw new InvalidOperationException("Unsafe test cleanup target");
            Directory.Delete(root,true);
        }
    }
}
