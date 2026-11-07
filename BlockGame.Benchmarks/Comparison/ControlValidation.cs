namespace BlockGame.Benchmarks.Comparison;

public sealed record ControlValidationResult(bool Passed, int Checks);

public static class ControlValidation
{
    public static ControlValidationResult Run()
    {
        ComparisonRow[] Compare(string before, string after) =>
            Comparator.Compare(ControlFixtures.Run(before), ControlFixtures.Run(after), calibration: true).Rows;

        var nullRows = Compare("null-a", "null-b");
        var positiveRows = Compare("positive-before", "positive-after");
        var earlyRows = Compare("early-before", "early-after");
        var negativeRows = Compare("positive-after", "positive-before");

        if (nullRows.Length != 4 || nullRows.Any(row => row.Status != "inconclusive") ||
            positiveRows is not [{ Status: "improved" }] ||
            earlyRows is not [{ Status: "inconclusive", Ratio: null, PercentChange: null }] ||
            negativeRows is not [{ Status: "regressed" }])
            throw new InvalidDataException("Comparator controls failed; no real verdict allowed");

        return new(Passed: true, Checks: 4);
    }
}
