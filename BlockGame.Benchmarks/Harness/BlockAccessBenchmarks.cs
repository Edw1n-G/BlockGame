using BenchmarkDotNet.Attributes;
using BlockGame.Benchmarks.Comparison;
using BlockGame.Benchmarks.Workloads;

namespace BlockGame.Benchmarks.Harness;

[ScenarioFamily("block-access")]
public class BlockAccessBenchmarks
{
    public IEnumerable<string> Cases => BlockAccessWorkload.Cases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private BlockAccessWorkload _fixture = null!;

    [GlobalSetup] public void Setup()
    {
        _fixture = new(Scenario);
        RunEvidence.Child(GetType(), Scenario, outputFingerprint: EvidenceJson.Fingerprint(_fixture.Validate()));
    }

    [Benchmark] public int Read256WorldBlocks() => _fixture.ReadBatch();

    [GlobalCleanup] public void Cleanup()
    {
        try { _fixture.Validate(); }
        finally { _fixture.Dispose(); }
    }
}
