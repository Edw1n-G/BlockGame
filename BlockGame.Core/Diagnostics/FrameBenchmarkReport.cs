using System.Text.Json;

namespace Basics.Diagnostics;

public sealed record FrameBenchmarkSummary
{
    public int SchemaVersion { get; init; } = 2;
    public int RenderDistance { get; init; }
    public string World { get; init; } = "terrain";
    public required string Workload { get; init; }
    public required string Revision { get; init; }
    public required string Environment { get; init; }
    public required string Graphics { get; init; }
    public double DurationSeconds { get; init; }
    public double MeasuredSeconds { get; init; }
    public double WarmupSeconds { get; init; }
    public int Frames { get; init; }
    public int Updates { get; init; }
    public double FrameP50Ms { get; init; }
    public double FrameP95Ms { get; init; }
    public double FrameP99Ms { get; init; }
    public double FrameMaxMs { get; init; }
    public double RenderCpuP95Ms { get; init; }
    public double MaxUpdateGapMs { get; init; }
    public double LongestUpdateHitchMs { get; init; }
    public double Distance { get; init; }
    public double ExpectedDistance { get; init; }
    public PipelineCounts Counts { get; init; }
    public double MeanDrawnFraction { get; init; }
    public double MeanResidentFraction { get; init; }
    public required string[] InvalidReasons { get; init; }
    public string[] DegradedReasons { get; init; } = [];
    public bool Valid => InvalidReasons.Length == 0;
}

public static class FrameBenchmarkReport
{
    public static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    public static FrameBenchmarkSummary Read(string path) =>
        JsonSerializer.Deserialize<FrameBenchmarkSummary>(File.ReadAllText(path), JsonOptions)
        ?? throw new InvalidDataException($"Empty summary: {path}");

    public static string[] CompareWarnings(FrameBenchmarkSummary baseline, FrameBenchmarkSummary candidate)
    {
        var warnings = new List<string>();
        if (baseline.SchemaVersion != candidate.SchemaVersion || baseline.Workload != candidate.Workload)
            warnings.Add("INCOMPATIBLE: workload/settings/schema differ");
        if (baseline.Revision.Contains("dirty") || candidate.Revision.Contains("dirty") ||
            baseline.Revision.Contains("unknown") || candidate.Revision.Contains("unknown"))
            warnings.Add("UNVERIFIED REVISION: dirty or unknown source; treat as a functional smoke run");
        if (baseline.Environment != candidate.Environment || baseline.Graphics != candidate.Graphics)
            warnings.Add("Environment or graphics hardware differs");
        if (!baseline.Valid || !candidate.Valid) warnings.Add("INVALID: at least one run failed validity checks");
        if (baseline.DegradedReasons.Length > 0 || candidate.DegradedReasons.Length > 0)
            warnings.Add("DEGRADED: at least one run reports update-loop degradation; inspect its summary");
        if (candidate.Updates < baseline.Updates * .9) warnings.Add("DEGRADED: candidate has over 10% fewer updates");
        bool Diff(double a, double b) => Math.Abs(b-a) > Math.Max(1, Math.Abs(a)*.05);
        if (Diff(baseline.Counts.Requested, candidate.Counts.Requested) ||
            Diff(baseline.Counts.Generated, candidate.Counts.Generated) ||
            Diff(baseline.Counts.Meshed, candidate.Counts.Meshed) ||
            Diff(baseline.Counts.Uploaded, candidate.Counts.Uploaded) ||
            Math.Abs(baseline.MeanResidentFraction-candidate.MeanResidentFraction) > .05 ||
            Math.Abs(baseline.MeanDrawnFraction-candidate.MeanDrawnFraction) > .05 ||
            Diff(baseline.Distance, candidate.Distance))
            warnings.Add("WORK DIFFERS: counts, distance or coverage differ by over 5%; frame deltas alone cannot establish speedup");
        return warnings.ToArray();
    }

    public static FrameBenchmarkSummary[] ReadRuns(string path)
    {
        if (File.Exists(path)) return [Read(path)];
        if (File.Exists(Path.Combine(path, "sweep.json")))
            throw new ArgumentException("This is a sweep folder with several render distances; pass one of its rd-NN folders.");
        string summary = Path.Combine(path, "summary.json");
        if (File.Exists(summary)) return [Read(summary)];
        var runs = Directory.GetDirectories(path, "run-*").Order()
            .Select(directory => Read(Path.Combine(directory, "summary.json"))).ToArray();
        if (runs.Length == 0) throw new InvalidDataException("No direct run summaries found; pass a fixed-distance run or repeats folder.");
        return runs;
    }

    public static string DescribeRepeats(FrameBenchmarkSummary[] runs)
    {
        if (runs.Length == 0) throw new InvalidDataException("No summary.json files found.");
        var text = new System.Text.StringBuilder($"Runs: {runs.Length}; valid: {runs.Count(r => r.Valid)}\n\n| Metric | Median | Min | Max | Spread |\n|---|---:|---:|---:|---:|\n");
        foreach (var (name, selector) in Metrics)
        {
            double[] values = runs.Select(selector).ToArray();
            double spread = FrameMetrics.RelativeSpread(values);
            text.AppendLine(FormattableString.Invariant($"| {name} | {FrameMetrics.Percentile(values,.5):F3} | {values.Min():F3} | {values.Max():F3} | {spread:P1}{(spread > .05 ? " NOISY" : "")} |"));
        }
        if (runs.Length < 3) text.AppendLine("Fewer than three repeats: exploratory result.");
        text.AppendLine("\nRevisions: " + string.Join(", ", runs.Select(r => r.Revision).Distinct()));
        foreach (string warning in runs.Skip(1).SelectMany(run => CompareWarnings(runs[0], run)).Distinct())
            text.AppendLine(warning);
        return text.ToString();
    }

    public static string DescribeSweep(IReadOnlyList<(int Distance, FrameBenchmarkSummary[] Runs)> sweep)
    {
        var text = new System.Text.StringBuilder("| Render distance | Valid runs | Frame p50 (ms) | Frame p95 (ms) | Frame p99 (ms) | Updates | Max update gap (ms) | Generated/s | Drawn fraction | Flags |\n|---:|---:|---:|---:|---:|---:|---:|---:|---:|---|\n");
        foreach (var (distance, runs) in sweep)
        {
            if (runs.Length == 0) continue;
            double Median(Func<FrameBenchmarkSummary, double> selector) => FrameMetrics.Percentile(runs.Select(selector), .5);
            var flags = new List<string>();
            if (runs.Any(r => !r.Valid)) flags.Add("INVALID");
            if (runs.Any(r => r.DegradedReasons.Length > 0)) flags.Add("DEGRADED");
            if (FrameMetrics.RelativeSpread(runs.Select(r => r.FrameP50Ms)) > .05) flags.Add("NOISY");
            text.AppendLine(FormattableString.Invariant(
                $"| {distance} | {runs.Count(r => r.Valid)}/{runs.Length} | {Median(r => r.FrameP50Ms):F2} | {Median(r => r.FrameP95Ms):F2} | {Median(r => r.FrameP99Ms):F2} | {Median(r => r.Updates):F0} | {Median(r => r.MaxUpdateGapMs):F1} | {Median(r => r.Counts.Generated / Math.Max(.001, r.MeasuredSeconds)):F1} | {Median(r => r.MeanDrawnFraction):F3} | {string.Join(", ", flags)} |"));
        }
        return text.ToString();
    }

    public static readonly (string Name, Func<FrameBenchmarkSummary,double> Get)[] Metrics =
    [
        ("Frame p50 (ms)", r => r.FrameP50Ms), ("Frame p95 (ms)", r => r.FrameP95Ms),
        ("Frame p99 (ms)", r => r.FrameP99Ms), ("Max update gap (ms)", r => r.MaxUpdateGapMs),
        ("Updates", r => r.Updates), ("Requested/s", r => r.Counts.Requested / Math.Max(.001,r.MeasuredSeconds)),
        ("Generated/s", r => r.Counts.Generated / Math.Max(.001,r.MeasuredSeconds)),
        ("Meshed/s", r => r.Counts.Meshed / Math.Max(.001,r.MeasuredSeconds)),
        ("Uploaded/s", r => r.Counts.Uploaded / Math.Max(.001,r.MeasuredSeconds)),
        ("Resident fraction", r => r.MeanResidentFraction), ("Drawn fraction", r => r.MeanDrawnFraction)
    ];

    public static string Compare(FrameBenchmarkSummary[] baseline, FrameBenchmarkSummary[] candidate)
    {
        var text = new System.Text.StringBuilder("# Frame benchmark comparison\n\n");
        text.AppendLine("## Baseline\n" + DescribeRepeats(baseline));
        text.AppendLine("## Candidate\n" + DescribeRepeats(candidate));
        foreach (string warning in baseline.SelectMany(b => candidate.SelectMany(c => CompareWarnings(b,c))).Distinct())
            text.AppendLine(warning);
        text.AppendLine("\n| Metric | Base median | Candidate median | Delta |\n|---|---:|---:|---:|");
        foreach (var (name, selector) in Metrics)
        {
            double b = FrameMetrics.Percentile(baseline.Select(selector), .5), c = FrameMetrics.Percentile(candidate.Select(selector), .5);
            string delta = b == 0 ? "n/a (zero baseline)" : ((c/b-1)*100).ToString("+0.0;-0.0;0.0", System.Globalization.CultureInfo.InvariantCulture) + "%";
            text.AppendLine(FormattableString.Invariant($"| {name} | {b:F3} | {c:F3} | {delta} |"));
        }
        return text.ToString();
    }

    public static int ComparisonExitCode(FrameBenchmarkSummary[] baseline, FrameBenchmarkSummary[] candidate)
    {
        if (baseline.Length < 3 || candidate.Length < 3) return 3;
        var all = baseline.Concat(candidate).ToArray();
        if (all.Any(r => r.SchemaVersion != 2 || r.Revision.Length != 40 || !r.Revision.All(Uri.IsHexDigit) ||
            r.MeasuredSeconds <= 0 || Metrics.Any(m => !double.IsFinite(m.Get(r)) || m.Get(r) < 0))) return 3;
        if (baseline.Select(r => r.Revision).Distinct().Count() != 1 || candidate.Select(r => r.Revision).Distinct().Count() != 1) return 3;
        if (all.Any(r => CompareWarnings(baseline[0], r).Length > 0)) return 3;
        foreach (var (_, selector) in Metrics.Take(3))
        {
            if (baseline.Any(r => selector(r) <= 0) || candidate.Any(r => selector(r) <= 0) ||
                FrameMetrics.RelativeSpread(baseline.Select(selector)) > .05 ||
                FrameMetrics.RelativeSpread(candidate.Select(selector)) > .05) return 3;
        }
        return Metrics.Take(3).Any(m => FrameMetrics.Percentile(candidate.Select(m.Get), .5) >
            FrameMetrics.Percentile(baseline.Select(m.Get), .5) * 1.05) ? 2 : 0;
    }
}
