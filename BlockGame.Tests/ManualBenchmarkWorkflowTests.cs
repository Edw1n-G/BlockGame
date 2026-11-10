using System.Text.Json;
using BlockGame.Benchmarks.Comparison;

namespace BlockGame.Tests;

public class ManualBenchmarkWorkflowTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("manual-benchmark-test-");

    private static BenchmarkRun ManualRun(string fixture, char revision) => ControlFixtures.Run(fixture) with
    {
        Kind = "revision",
        EngineRevision = new string(revision, 40),
        EngineClean = true,
        HarnessTree = new string('c', 40),
        EnvironmentInfo = JsonSerializer.SerializeToElement(new { cpu = "test CPU", os = "test OS" }),
        Cases = ControlFixtures.Run(fixture).Cases.Select(c => c with { OutputFingerprint = "same-output" }).ToArray()
    };

    [Fact]
    public void ManualRunsCompareWithoutScriptAttestationOrSessionDriftChecks()
    {
        var report = Comparator.Compare(ManualRun("positive-before", 'a'), ManualRun("positive-after", 'b'));
        Assert.Equal("improved", Assert.Single(report.Rows).Status);
        Assert.Empty(report.Warnings);
        Assert.Equal("test CPU", report.BaselineEnvironment!.Value.GetProperty("cpu").GetString());
        Assert.Equal("test OS", report.CandidateEnvironment!.Value.GetProperty("os").GetString());
    }

    [Theory]
    [InlineData("dirty")]
    [InlineData("smoke")]
    [InlineData("environment")]
    [InlineData("output")]
    [InlineData("missing-harness")]
    [InlineData("legacy")]
    public void ManualComparisonStillRejectsInvalidMeasurements(string mutation)
    {
        var baseline = ManualRun("positive-before", 'a');
        var candidate = ManualRun("positive-after", 'b');
        candidate = mutation switch
        {
            "dirty" => candidate with { EngineClean = false },
            "smoke" => candidate with { Mode = "Smoke" },
            "environment" => candidate with { EnvironmentFingerprint = "another CPU" },
            "output" => candidate with { Cases = [candidate.Cases[0] with { OutputFingerprint = "different" }] },
            "missing-harness" => candidate with { HarnessTree = "" },
            "legacy" => candidate with { SchemaVersion = 3 },
            _ => throw new ArgumentException(mutation)
        };
        Assert.Equal("not comparable", Assert.Single(Comparator.Compare(baseline, candidate).Rows).Status);
    }

    [Fact]
    public void CompareReportsHarnessChangesWithoutBlockingTheVerdict()
    {
        var baseline = _directory.CreateSubdirectory("baseline");
        var candidate = _directory.CreateSubdirectory("candidate");
        var before = ManualRun("positive-before", 'a');
        var after = ManualRun("positive-after", 'b') with { HarnessTree = new string('d', 40) };
        EvidenceJson.Write(Path.Combine(baseline.FullName, "benchmark-run.json"), before);
        EvidenceJson.Write(Path.Combine(candidate.FullName, "benchmark-run.json"), after);
        string output = Path.Combine(_directory.FullName, "reports", "comparison.json");

        Assert.Equal(0, ComparisonCli.Run(["compare", baseline.FullName, candidate.FullName, output]));
        var report = JsonSerializer.Deserialize<ComparisonReport>(File.ReadAllText(output), EvidenceJson.Options)!;
        Assert.Equal("improved", Assert.Single(report.Rows).Status);
        Assert.Equal(before.HarnessTree, report.BaselineHarnessTree);
        Assert.Equal(after.HarnessTree, report.CandidateHarnessTree);
        string warning = Assert.Single(report.Warnings);
        Assert.Contains(warning, File.ReadAllText(Path.ChangeExtension(output, ".md")));
        Assert.Empty(_directory.GetFiles("*.zip", SearchOption.AllDirectories));
        Assert.Empty(_directory.GetFiles("*.sha256", SearchOption.AllDirectories));
    }

    [Fact]
    public void CompareRejectsMissingRunInsteadOfProducingAnEmptyReport()
    {
        var missing = _directory.CreateSubdirectory("missing");
        string output = Path.Combine(_directory.FullName, "comparison.json");
        Assert.Equal(1, ComparisonCli.Run(["compare", missing.FullName, missing.FullName, output]));
        Assert.False(File.Exists(output));
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
