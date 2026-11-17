using System.Text.Json;
using BlockGame.Benchmarks.Comparison;
using BlockGame.Benchmarks.Harness;

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
    public void CompareReportsHarnessChangesAndBlocksGate()
    {
        var baseline = _directory.CreateSubdirectory("baseline");
        var candidate = _directory.CreateSubdirectory("candidate");
        var before = ManualRun("positive-before", 'a');
        var after = ManualRun("positive-after", 'b') with { HarnessTree = new string('d', 40) };
        EvidenceJson.Write(Path.Combine(baseline.FullName, "benchmark-run.json"), before);
        EvidenceJson.Write(Path.Combine(candidate.FullName, "benchmark-run.json"), after);
        string output = Path.Combine(_directory.FullName, "reports", "comparison.json");

        Assert.Equal(3, ComparisonCli.Run(["compare", baseline.FullName, candidate.FullName, output]));
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

    [Theory]
    [InlineData("positive-before", "positive-after", 0)]
    [InlineData("positive-after", "positive-before", 2)]
    [InlineData("null-a", "null-b", 0)]
    [InlineData("early-before", "early-after", 3)]
    public void ComparisonCliReturnsGateStatus(string before, string after, int code)
    {
        string baseline = _directory.CreateSubdirectory("base").FullName;
        string candidate = _directory.CreateSubdirectory("cand").FullName;
        EvidenceJson.Write(Path.Combine(baseline, "benchmark-run.json"), ManualRun(before, 'a'));
        EvidenceJson.Write(Path.Combine(candidate, "benchmark-run.json"), ManualRun(after, 'b'));
        Assert.Equal(code, ComparisonCli.Run(["compare", baseline, candidate, Path.Combine(_directory.FullName, "report.json")]));
    }

    [Theory]
    [InlineData("noise")]
    [InlineData("output")]
    [InlineData("allocation")]
    public void GateRejectsUnusableEvidenceAndAllocationRegressions(string mutation)
    {
        var before = ManualRun("positive-before", 'a');
        var after = ManualRun("positive-after", 'b');
        var row = after.Cases[0];
        row = mutation switch
        {
            "noise" => row with { Diagnostics = ["multimodal"] },
            "output" => row with { OutputFingerprint = "different" },
            _ => row with { BytesPerOperation = before.Cases[0].BytesPerOperation + 100 }
        };
        Assert.Equal(mutation == "allocation" ? 2 : 3, ComparisonGate.ExitCode(Comparator.Compare(before, after with { Cases = [row] })));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RevisionWorkflowTestsBeforeMeasuringAndStopsOnFailure(bool failTests)
    {
        string output = Path.Combine(_directory.FullName, "revision-run");
        var commands = new List<string>();
        string Fake(string executable, string directory, string? log, params string[] args)
        {
            commands.Add(string.Join(" ", args));
            if (args[0] == "rev-parse") return args.Contains("--show-toplevel") ? _directory.FullName : new string(args[^1].StartsWith("base") ? 'a' : 'b', 40);
            if (args[0] == "test" && failTests) throw new InvalidOperationException("test failure");
            if (args[0] == "run")
            {
                string destination = args[Array.IndexOf(args, "--output") + 1];
                Directory.CreateDirectory(destination);
                bool baseline = destination.EndsWith("baseline");
                EvidenceJson.Write(Path.Combine(destination, "benchmark-run.json"), ManualRun(baseline ? "positive-before" : "positive-after", baseline ? 'a' : 'b'));
            }
            return "";
        }
        int result = RevisionComparison.Run(["revisions", "--baseline", "base", "--candidate", "cand", "--case", "mesh/*", "--output", output], Fake);
        Assert.Equal(failTests ? 1 : 0, result);
        var stages = commands.Where(c => c.StartsWith("test ") || c.StartsWith("run ")).ToArray();
        Assert.Equal(failTests ? 1 : 4, stages.Length);
        Assert.StartsWith("test ", stages[0]);
        if (!failTests)
        {
            Assert.StartsWith("run ", stages[1]); Assert.StartsWith("test ", stages[2]); Assert.StartsWith("run ", stages[3]);
            Assert.All(stages.Where(c => c.StartsWith("run ")), c => Assert.Contains("--case mesh/*", c));
            Assert.True(File.Exists(Path.Combine(output, "comparison.json")));
        }
    }

    public void Dispose() => _directory.Delete(recursive: true);
}
