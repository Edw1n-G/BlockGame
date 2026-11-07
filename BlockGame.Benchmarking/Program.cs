using System.IO.Compression;
using System.Text.Json;

namespace BlockGame.Benchmarking;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            switch (args.ElementAtOrDefault(0))
            {
                case "pack": PortableBundle.Pack(args[1],args[2]); Console.WriteLine("Verified portable bundle: "+args[2]); break;
                case "verify": PortableBundle.Verify(args[1]); Console.WriteLine("Bundle hashes verified"); break;
                case "validate-controls":
                    if (args.Length != 1) throw new ArgumentException("validate-controls takes no arguments");
                    var checks = ControlValidation.Run();
                    Console.WriteLine($"controls: {checks.Checks}/{checks.Checks} checks passed");
                    break;
                case "compare":
                    var validation = ControlValidation.Run();
                    PortableBundle.Verify(args[1]);
                    PortableBundle.Verify(args[2]);
                    var report = Comparator.Compare(ReadRun(args[1]), ReadRun(args[2])) with { Controls = validation };
                    EvidenceJson.Write(args[3],report);
                    File.WriteAllLines(Path.ChangeExtension(args[3],".md"), new[] {"# Advisory benchmark comparison","", $"Baseline: {report.BaselineRevision}; candidate: {report.CandidateRevision}.","Policy: provisional 5% practical budget, 100 ns timing floor; no automatic performance gate.","", "| Scenario | Unit | Result | Baseline ns | Candidate ns | Reason |","|---|---|---|---:|---:|---|"}
                        .Concat(report.Rows.Select(r => $"| {r.ScenarioId} | {r.Contract?.Unit} | {r.Status} | {r.BaselineNs?.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)} | {r.CandidateNs?.ToString("F3",System.Globalization.CultureInfo.InvariantCulture)} | {r.Reason} |")));
                    Console.WriteLine(string.Join(Environment.NewLine,report.Rows.Select(r => $"{r.ScenarioId}: {r.Status} — {r.Reason}")));
                    break;
                default: throw new ArgumentException("Use pack <directory> <zip>, verify <bundle>, or compare <baseline-bundle> <candidate-bundle> <report.json>");
            }
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    private static BenchmarkRun ReadRun(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var entry = zip.GetEntry("benchmark-run.json") ?? throw new InvalidDataException("Missing benchmark-run.json");
        using var stream = entry.Open();
        return JsonSerializer.Deserialize<BenchmarkRun>(stream, EvidenceJson.Options)
            ?? throw new InvalidDataException("Missing run");
    }
}
