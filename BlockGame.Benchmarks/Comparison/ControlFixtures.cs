namespace BlockGame.Benchmarks.Comparison;

public static class ControlFixtures
{
    public static BenchmarkRun Run(string name)
    {
        Observation[] cases = name switch
        {
            "null-a" => NullCases(2_000, 400_000, 50_000, 50_000),
            "null-b" => NullCases(2_012, 397_000, 54_000, 46_000),
            "positive-before" => [Case("ctrl/positive", "Old.Run", 1_000_000, 0.01, 4_096)],
            "positive-after" => [Case("ctrl/positive", "New.Run", 30_000, 0.01, 0)],
            "early-before" => [Case("ctrl/early-return", "Old.Run", 4_000, 0.02, 0, "early-return")],
            "early-after" => [Case("ctrl/early-return", "New.Run", 28, 0.02, 0, "early-return")],
            _ => throw new ArgumentException($"Unknown control fixture: {name}", nameof(name))
        };

        return new(BenchmarkRun.CurrentSchemaVersion, "calibration", "Measure", name, false,
            EvidenceJson.Fingerprint("control harness"), EvidenceJson.Fingerprint("control environment"),
            cases, "Deterministic comparator control");
    }

    private static Observation[] NullCases(double fast, double slow, double outsideOne, double outsideTwo) =>
    [
        Case("ctrl/stable-fast", "StableFast.Run", fast, 0.01, 0),
        Case("ctrl/stable-slow", "StableSlow.Run", slow, 0.015, 432),
        Case("ctrl/outside-1", "OutsideOne.Run", outsideOne, 0.08, 0, "diagnostic"),
        Case("ctrl/outside-2", "OutsideTwo.Run", outsideTwo, 0.08, 0, "diagnostic")
    ];

    private static Observation Case(string id, string caseId, double mean, double cv, double allocation, string timingClass = "normal") =>
        new(caseId, ScenarioContract.Create(id, 1, "operation", new { scenario = id }, timingClass),
            caseId, Samples(mean, cv), allocation, []);

    private static double[] Samples(double mean, double cv)
    {
        double[] z = [-1.4142, -0.7071, 0, 0.7071, 1.4142];
        return Enumerable.Range(0, 30).Select(i => mean * (1 + cv * z[i % z.Length])).ToArray();
    }
}
