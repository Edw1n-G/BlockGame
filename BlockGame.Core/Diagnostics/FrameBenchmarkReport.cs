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
        if (File.Exists(Path.Combine(path, "calibration.json"))) path = Path.Combine(path, "runs");
        return Directory.GetFiles(path, "summary.json", SearchOption.AllDirectories).Order().Select(Read).ToArray();
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
}
