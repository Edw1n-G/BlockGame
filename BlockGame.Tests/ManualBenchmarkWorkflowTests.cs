using System.Text.Json;
using BlockGame.Benchmarks.Comparison;

namespace BlockGame.Tests;

public class ManualBenchmarkWorkflowTests : IDisposable
{
    private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("manual-benchmark-test-");

    private static BenchmarkRun ManualRun(string fixture, char revision) => ControlFixtures.Run(fixture) with
    {
        SchemaVersion = 3,
        Kind = "revision",
        EngineRevision = new string(revision, 40),
        EngineClean = true,
        HarnessFingerprint = new string(revision, 40),
        SourceUnchanged = false,
        CorrectnessScope = "workload",
        EnvironmentInfo = JsonSerializer.SerializeToElement(new { cpu = "test CPU", os = "test OS" }),
        Cases = ControlFixtures.Run(fixture).Cases.Select(c => c with { OutputFingerprint = "same-output" }).ToArray()
    };

    [Fact]
    public void ManualRunsCompareWithoutScriptAttestationOrSessionDriftChecks()
    {
        var report = Comparator.Compare(ManualRun("positive-before", 'a'), ManualRun("positive-after", 'b'));
        Assert.Equal("improved", Assert.Single(report.Rows).Status);
        Assert.Equal("test CPU", report.BaselineEnvironment!.Value.GetProperty("cpu").GetString());
        Assert.Equal("test OS", report.CandidateEnvironment!.Value.GetProperty("os").GetString());
    }

    [Theory]
    [InlineData("dirty")]
    [InlineData("smoke")]
    [InlineData("environment")]
    [InlineData("output")]
    [InlineData("failed")]
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
            "failed" => candidate with { CorrectnessPassed = false },
            "legacy" => candidate with { SchemaVersion = 2, CorrectnessScope = "full", SourceUnchanged = true },
            _ => throw new ArgumentException(mutation)
        };
        Assert.Equal("not comparable", Assert.Single(Comparator.Compare(baseline, candidate).Rows).Status);
    }

    [Fact]
    public void CompareReadsPlainDirectoriesAndWritesReportsWithoutArchivesOrChecksums()
    {
        var baseline = _directory.CreateSubdirectory("baseline");
        var candidate = _directory.CreateSubdirectory("candidate");
        EvidenceJson.Write(Path.Combine(baseline.FullName, "benchmark-run.json"), ManualRun("positive-before", 'a'));
        EvidenceJson.Write(Path.Combine(candidate.FullName, "benchmark-run.json"), ManualRun("positive-after", 'b'));
        string output = Path.Combine(_directory.FullName, "reports", "comparison.json");

        Assert.Equal(0, ComparisonCli.Run(["compare", baseline.FullName, candidate.FullName, output]));
        var report = JsonSerializer.Deserialize<ComparisonReport>(File.ReadAllText(output), EvidenceJson.Options)!;
        Assert.Equal("improved", Assert.Single(report.Rows).Status);
        Assert.Contains("test CPU", File.ReadAllText(Path.ChangeExtension(output, ".md")));
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
