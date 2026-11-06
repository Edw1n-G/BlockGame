namespace BlockGame.Benchmarking;

public static class Comparator
{
    public static ComparisonReport Compare(BenchmarkRun baseline, BenchmarkRun candidate, bool calibration = false, ComparisonPolicy? policy = null)
    {
        policy ??= new();
        if (policy.PracticalPercent <= 0 || policy.AbsoluteDeltaNs < 0 || policy.NearZeroNs <= 0 || policy.MaxCvPercent <= 0 || policy.MinimumSamples < 2 || policy.UncertaintyMultiplier <= 0)
            throw new ArgumentException("Invalid comparison policy");
        string? global = ValidateRun(baseline,calibration) ?? ValidateRun(candidate,calibration);
        if (baseline.HarnessFingerprint != candidate.HarnessFingerprint) global ??= "Harness differs";
        if (baseline.EnvironmentFingerprint != candidate.EnvironmentFingerprint) global ??= "Environment/job/dependencies differ";
        string Key(Observation c)=>c.Scenario.Id+"|"+c.Scenario.Version+"|"+c.Scenario.Unit+"|"+c.Scenario.InputFingerprint;
        var ids = baseline.Cases.Select(Key).Union(candidate.Cases.Select(Key)).Order(StringComparer.Ordinal);
        var rows = new List<ComparisonRow>();
        foreach (var key in ids)
        {
            var a = baseline.Cases.Where(c => Key(c) == key).ToArray();
            var b = candidate.Cases.Where(c => Key(c) == key).ToArray();
            string id=(a.FirstOrDefault()??b.First()).Scenario.Id;
            var contract=(a.FirstOrDefault()??b.First()).Scenario;
            if (global is not null) { rows.Add(new(id,"not comparable",global,Contract:contract)); continue; }
            if (a.Length != 1 || b.Length != 1) { rows.Add(new(id,"not comparable","Missing or ambiguous scenario; select one implementation per side",Contract:contract)); continue; }
            if(!calibration && (string.IsNullOrWhiteSpace(a[0].OutputFingerprint) || a[0].OutputFingerprint!=b[0].OutputFingerprint))
            {rows.Add(new(id,"not comparable","Missing/different correctness output fingerprint; review semantics before performance",Contract:contract));continue;}
            rows.Add(CompareCase(a[0],b[0],policy) with {Contract=contract});
        }
        if (rows.Count == 0) rows.Add(new("<none>","not comparable","No scenarios discovered"));
        return new(1,calibration ? "calibration fixture validation only" : "advisory revision comparison; policy is provisional",policy,rows.ToArray(),
            baseline.EngineRevision,candidate.EngineRevision,baseline.HarnessFingerprint,baseline.EnvironmentFingerprint,
            EvidenceJson.Hash(File.ReadAllBytes(typeof(Comparator).Assembly.Location)));
    }

    private static string? ValidateRun(BenchmarkRun run, bool calibration)
    {
        if (run.SchemaVersion != 2 || run.Mode != "Measure" || !run.CorrectnessPassed || !run.SourceUnchanged) return "Failed, smoke, unsupported or incomplete run";
        if (!calibration && (run.Kind != "revision" || !run.EngineClean || run.EngineRevision.Length != 40 || !run.EngineRevision.All(Uri.IsHexDigit)))
            return "Requires an actual clean Git revision; models/controls/dirty runs are not production baselines";
        if (!calibration && run.CorrectnessScope != "full") return "Focused preflight is iteration evidence; full correctness required for revision verdicts";
        if (string.IsNullOrWhiteSpace(run.HarnessFingerprint) || string.IsNullOrWhiteSpace(run.EnvironmentFingerprint)) return "Missing harness/environment identity";
        return null;
    }

    private static ComparisonRow CompareCase(Observation a, Observation b, ComparisonPolicy p)
    {
        string id = a.Scenario.Id;
        try { a.Scenario.Validate(); b.Scenario.Validate(); }
        catch (InvalidDataException) { return new(id,"not comparable","Invalid input fingerprint/contract"); }
        if (a.Scenario.Version != b.Scenario.Version || a.Scenario.Unit != b.Scenario.Unit ||
            a.Scenario.InputFingerprint != b.Scenario.InputFingerprint || a.Scenario.TimingClass != b.Scenario.TimingClass)
            return new(id,"not comparable","Scenario version/unit/input/timing contract differs");
        if (!a.Success || !b.Success || a.SamplesNs.Length < p.MinimumSamples || b.SamplesNs.Length < p.MinimumSamples ||
            a.SamplesNs.Concat(b.SamplesNs).Any(v => !double.IsFinite(v) || v < 0) ||
            a.BytesPerOperation is < 0 || b.BytesPerOperation is < 0 ||
            a.BytesPerOperation is { } aa && !double.IsFinite(aa) || b.BytesPerOperation is { } bb && !double.IsFinite(bb))
            return new(id,"not comparable","Failed/missing/invalid measurements or too few samples");
        double meanA = a.SamplesNs.Average(), meanB = b.SamplesNs.Average(), delta = meanB-meanA;
        if(!double.IsFinite(meanA) || !double.IsFinite(meanB))return new(id,"not comparable","Invalid aggregate measurements");
        double? allocationDelta = a.BytesPerOperation is { } ab && b.BytesPerOperation is { } cb ? cb-ab : null;
        string allocationStatus = allocationDelta is null ? "unavailable" : allocationDelta < 0 ? "decreased" : allocationDelta > 0 ? "increased" : "unchanged";
        ComparisonRow Row(string status, string reason, bool ratios = false) => new(id,status,reason,meanA,meanB,delta,
            ratios ? meanB/meanA : null,ratios ? 100*delta/meanA : null,allocationDelta,allocationStatus);
        if (a.Scenario.TimingClass != "normal") return Row("inconclusive","Early-return/diagnostic timing: ratios and percentages suppressed");
        if (meanA < p.NearZeroNs || meanB < p.NearZeroNs) return Row("inconclusive","Near-zero timing: below declared resolution floor; ratios suppressed");
        double sdA = Sd(a.SamplesNs,meanA), sdB = Sd(b.SamplesNs,meanB);
        if (100*sdA/meanA > p.MaxCvPercent || 100*sdB/meanB > p.MaxCvPercent || a.Diagnostics.Concat(b.Diagnostics).Any())
            return Row("inconclusive","Noisy or statistically flagged measurements");
        double uncertainty = p.UncertaintyMultiplier*Math.Sqrt(sdA*sdA/a.SamplesNs.Length+sdB*sdB/b.SamplesNs.Length);
        double practical = Math.Max(p.AbsoluteDeltaNs,meanA*p.PracticalPercent/100);
        if (Math.Abs(delta) <= practical+uncertainty) return Row("inconclusive","Within practical drift/uncertainty budget",true);
        return Row(delta < 0 ? "improved" : "regressed","Exceeds declared practical budget and sampling uncertainty",true);
    }

    private static double Sd(double[] samples, double mean) => Math.Sqrt(samples.Sum(v => (v-mean)*(v-mean))/(samples.Length-1));
}
