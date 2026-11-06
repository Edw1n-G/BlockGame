namespace BlockGame.Benchmarking;

public static class ControlValidation
{
    public static object Validate(string root)
    {
        BenchmarkRun Read(string name)=>PortableBundle.Verify(Path.Combine(root,name));
        var checks=new[]{
            new {name="null-and-noisy",report=Comparator.Compare(Read("null-a"),Read("null-b"),true)},
            new {name="positive-model",report=Comparator.Compare(Read("positive-before"),Read("positive-after"),true)},
            new {name="early-return",report=Comparator.Compare(Read("early-before"),Read("early-after"),true)},
            new {name="measured-negative",report=Comparator.Compare(Read("negative-before"),Read("negative-after"),true)}};
        if(checks[0].report.Rows.Length!=21 || checks[0].report.Rows.Any(r=>r.Status!="inconclusive") || checks[1].report.Rows.Single().Status!="improved" ||
            checks[2].report.Rows.Single().Ratio is not null || checks[3].report.Rows.Single().Status!="regressed")throw new InvalidDataException("Comparator controls failed; no real verdict allowed");
        return new {status="passed",purpose="Comparator calibration/validation only; copied-model effect is not an engine revision claim",checks,
            fixtureFingerprint=EvidenceJson.Fingerprint(Directory.EnumerateDirectories(root).Order(StringComparer.Ordinal).Select(d=>new {name=Path.GetFileName(d),index=EvidenceJson.Hash(File.ReadAllBytes(Path.Combine(d,"bundle.json")))}).ToArray())};
    }
}
