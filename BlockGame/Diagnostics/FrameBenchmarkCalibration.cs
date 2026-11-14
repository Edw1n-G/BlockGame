using System.Text;
using System.Text.Json;

namespace Basics.Diagnostics;

public sealed record CalibrationAttempt(int Distance, double? FrameP50Ms, bool Valid, string Detail);
public sealed record CalibrationResult(int Version, bool TargetReached, int RenderDistance, double Duration,
    string World, string Script, double TargetMs, double? VerifiedMedianMs, string Status,
    CalibrationAttempt[] Attempts, string WorldCode = "");

public static class FrameBenchmarkCalibration
{
    public static CalibrationResult Read(string path)
    {
        if (Directory.Exists(path)) path = Path.Combine(path, "calibration.json");
        var saved = JsonSerializer.Deserialize<CalibrationResult>(File.ReadAllText(path))
            ?? throw new InvalidDataException("Empty calibration file.");
        if (saved.Version != 2 || !saved.TargetReached || !FrameCalibrationSearch.OnTarget(saved.VerifiedMedianMs ?? 0))
            throw new ArgumentException("Baseline needs a successful v2 calibration near 16 ms; failed or old calibrations cannot be replayed.");
        var expected = new FrameBenchmarkOptions(saved.RenderDistance, saved.Duration, saved.Script, "", 1, saved.World).Settings().Worldtype;
        if (saved.WorldCode != expected) throw new ArgumentException("Baseline world graph differs from this build; recalibrate the baseline.");
        return saved;
    }

    public static void Run(FrameBenchmarkOptions options)
    {
        Directory.CreateDirectory(options.Output);
        var attempts = new List<CalibrationAttempt>();
        var search = new FrameCalibrationSearch(options.MaxRenderDistance);
        int distance = options.RenderDistance;
        double? verified = null;
        bool reached = false;
        string status = "Target not reached within the distance bounds.";
        try
        {
            while (true)
            {
                Console.WriteLine($"Calibration ({options.World}): radius {distance}, target 16 ms median...");
                var probe = FrameBenchmarkCommand.RunChild(options with
                {
                    RenderDistance = distance, Output = Path.Combine(options.Output, "probes", $"rd-{distance:00}"), Calibrate = false
                });
                attempts.Add(new(distance, probe.FrameP50Ms, probe.Valid, string.Join("; ", probe.InvalidReasons.Concat(probe.DegradedReasons))));
                Console.WriteLine($"Radius {distance}: {probe.FrameP50Ms:F2} ms; valid={probe.Valid}");
                Save();
                if (!probe.Valid)
                {
                    status = "Calibration stopped: invalid probe. Inspect path, pipeline and timing validity before retrying.";
                    break;
                }
                int? next = search.Record(distance, probe.FrameP50Ms);
                if (next is { } nextDistance) { distance = nextDistance; continue; }
                distance = search.ClosestDistance;
                // Always verify the selected load with three fresh processes, independently of probes.
                var runs = FrameBenchmarkCommand.RunRepeats(options with
                {
                    RenderDistance = distance, Repeats = Math.Max(3, options.Repeats),
                    Output = Path.Combine(options.Output, "runs"), Calibrate = false
                });
                verified = FrameMetrics.Percentile(runs.Select(r => r.FrameP50Ms), .5);
                reached = runs.All(r => r.Valid) && FrameCalibrationSearch.OnTarget(verified.Value);
                status = reached ? "Target reached: reuse this distance and world for candidate runs." :
                    "Target not reached: closest tested whole-chunk radius did not verify within 16 ms ±10%.";
                if (runs.Any(r => r.DegradedReasons.Length > 0))
                    status += " DEGRADED: update rate is below 90% of nominal 60 Hz; preserve this baseline update count in comparisons.";
                if (FrameMetrics.RelativeSpread(runs.Select(r => r.FrameP50Ms)) > .05)
                    status += " NOISY: verification median-frame-time spread exceeds 5%.";
                break;
            }
        }
        catch (Exception ex)
        {
            status = "Calibration stopped: " + ex.Message;
            attempts.Add(new(distance, null, false, ex.Message));
        }
        Save();
        Console.WriteLine(status);
        if (!reached) System.Environment.ExitCode = 2;

        void Save()
        {
            var result = new CalibrationResult(2, reached, distance, options.Duration, options.World,
                options.Script, FrameCalibrationSearch.TargetMs, verified, status, attempts.ToArray(), options.Settings().Worldtype);
            File.WriteAllText(Path.Combine(options.Output, "calibration.json"), JsonSerializer.Serialize(result, FrameBenchmarkReport.JsonOptions));
            var text = new StringBuilder($"# Baseline calibration\n\n{status}\n\nWorld: {options.World}; target: 16 ms ±10%; selected radius: {distance}; verified median: {verified?.ToString("F2") ?? "not verified"} ms.\n\n| Radius | Median frame ms | Valid | Detail |\n|---:|---:|---|---|\n");
            foreach (var attempt in attempts)
                text.AppendLine(FormattableString.Invariant($"| {attempt.Distance} | {attempt.FrameP50Ms:F3} | {attempt.Valid} | {attempt.Detail} |"));
            text.AppendLine("\nProbe runs are excluded from baseline comparisons. Compare the `runs` folder with candidate runs. A failed calibration must not be presented as a 16 ms baseline.");
            File.WriteAllText(Path.Combine(options.Output, "calibration.md"), text.ToString());
        }
    }
}
