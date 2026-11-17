namespace BlockGame.Benchmarks.Comparison;

public static class ComparisonGate
{
    // A pass means no detected regression, not proof of a speedup.
    public static int ExitCode(ComparisonReport report)
    {
        if (report.Rows.Length == 0 || report.Warnings.Length != 0 ||
            report.Rows.Any(r => r.Status == "not comparable")) return 3;
        if (report.Rows.Any(r => r.Status == "regressed" || r.AllocationDeltaBytes > 0)) return 2;
        if (report.Rows.Any(r => r.Reason == "Noisy or statistically flagged measurements")) return 3;
        return report.Rows.Any(r => r.Status == "improved" ||
            r.Reason == "Within practical drift/uncertainty budget") ? 0 : 3;
    }
}
