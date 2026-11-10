using System.Text.Json;

namespace BlockGame.Benchmarks.Comparison;

public static class ComparisonCli
{
    public static int Run(string[] args)
    {
        try
        {
            switch (args.ElementAtOrDefault(0))
            {
                case "validate-controls":
                    if (args.Length != 1) throw new ArgumentException("validate-controls takes no arguments");
                    var checks = ControlValidation.Run();
                    Console.WriteLine($"controls: {checks.Checks}/{checks.Checks} checks passed");
                    break;
                case "compare":
                    if (args.Length is not (3 or 4)) throw new ArgumentException("Use compare <baseline-directory> <candidate-directory> [report.json]");
                    var validation = ControlValidation.Run();
                    var report = Comparator.Compare(ReadRun(args[1]), ReadRun(args[2])) with { Controls = validation };
                    string reportPath=Path.GetFullPath(args.Length==4 ? args[3] : Path.Combine("artifacts","comparisons",$"{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}","comparison.json"));
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
                    EvidenceJson.Write(reportPath,report);
                    File.WriteAllLines(Path.ChangeExtension(reportPath,".md"), new[] {"# Advisory benchmark comparison","", $"Baseline: {report.BaselineRevision}; candidate: {report.CandidateRevision}.",
                        $"Harness tree: baseline {report.BaselineHarnessTree}; candidate {report.CandidateHarnessTree}.", ""}
                        .Concat(report.Warnings.Select(warning => $"> **Warning:** {warning}"))
                        .Concat(new[] {"", "Policy: provisional 5% practical budget, 100 ns timing floor; no automatic performance gate.",
                        "Full dotnet test runs are a manual prerequisite. Background load and power plan are uncontrolled noise; inspect the BenchmarkDotNet reports and repeat uncertain measurements.",
                        "",$"Baseline environment: {report.BaselineEnvironment}",$"Candidate environment: {report.CandidateEnvironment}","", "| Scenario | Unit | Result | Baseline ns | Candidate ns | Reason |","|---|---|---|---:|---:|---|"})
                        .Concat(report.Rows.Select(r => $"| {r.ScenarioId} | {r.Contract?.Unit} | {r.Status} | {r.BaselineNs?.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)} | {r.CandidateNs?.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)} | {r.Reason} |")));
                    foreach (string warning in report.Warnings) Console.WriteLine($"Warning: {warning}");
                    Console.WriteLine(string.Join(Environment.NewLine,report.Rows.Select(r => $"{r.ScenarioId}: {r.Status} — {r.Reason}")));
                    Console.WriteLine($"Comparison report: {Path.ChangeExtension(reportPath,".md")}");
                    break;
                default: throw new ArgumentException("Use compare <baseline-directory> <candidate-directory> [report.json] or validate-controls");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    private static BenchmarkRun ReadRun(string path)
    {
        using var stream = File.OpenRead(Path.Combine(path, "benchmark-run.json"));
        return JsonSerializer.Deserialize<BenchmarkRun>(stream, EvidenceJson.Options)
            ?? throw new InvalidDataException("Missing run");
    }
}
