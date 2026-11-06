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
                case "seal-controls":
                    foreach(string directory in Directory.EnumerateDirectories(args[1]))PortableBundle.Seal(directory);
                    break;
                case "validate-controls":
                    EvidenceJson.Write(args[2],ControlValidation.Validate(args[1]));Console.WriteLine("Null, positive-model, noisy, early-return and measured-negative controls passed");
                    break;
                case "compare":
                    var validation=ControlValidation.Validate(Path.Combine(AppContext.BaseDirectory,"Controls"));
                    EvidenceJson.Write(Path.ChangeExtension(args[3],"controls.json"),validation);
                    var report = Comparator.Compare(PortableBundle.Verify(args[1]),PortableBundle.Verify(args[2]));
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
}
