using BenchmarkDotNet.Attributes;
using BlockGame.Workloads;

namespace BlockGame.Benchmarks;

public class SelectionRebuildBenchmarks
{
    public IEnumerable<string> Cases => SelectionMovementWorkload.Cases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private SelectionRebuildWorkload _workload = null!;
    [GlobalSetup] public void Setup()
    {
        _workload = new(Scenario);
        _ = _workload.Run();
        RunEvidence.Child(GetType(), Scenario, input: new { version = SelectionMovementWorkload.Version, scenario = Scenario,
            horizontal = 20, vertical = 15, transitions = SelectionMovementWorkload.Path(Scenario).Length,
            unit = "one closed movement path; selection + set difference + active-set updates; provider/tasks/locks excluded", algorithm = "frozen original rebuild" });
    }
    [Benchmark] public long PlanAndApplyPath() => _workload.Run();
}

public class SelectionDeltaBenchmarks
{
    public IEnumerable<string> Cases => SelectionMovementWorkload.Cases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private SelectionDeltaWorkload _workload = null!;
    [GlobalSetup] public void Setup()
    {
        _workload = new(Scenario); _ = _workload.Run();
        RunEvidence.Child(GetType(), Scenario, input: new { version = SelectionMovementWorkload.Version, scenario = Scenario,
            horizontal = 20, vertical = 15, transitions = SelectionMovementWorkload.Path(Scenario).Length,
            unit = "one closed movement path; selection + set difference + active-set updates; provider/tasks/locks excluded", algorithm = "incremental column intervals; warmed bounded scratch and active set" });
    }
    [Benchmark] public long PlanAndApplyPath() => _workload.Run();
}

public class SelectionOriginalBenchmarks
{
    public IEnumerable<string> Cases => WorkloadCatalog.SelectionCases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private Basics.Game.Utilities.ChunkCoord _center;
    private int _horizontal, _vertical;
    [GlobalSetup] public void Setup()
    {
        (_center,_horizontal,_vertical) = WorkloadCatalog.Selection(Scenario);
        RunEvidence.Child(GetType(),Scenario,input: new { x = _center.X, y = _center.Y, z = _center.Z, horizontal = _horizontal, vertical = _vertical, lod = 0, algorithm = "frozen original Calculate", unit = "one fresh set" });
    }
    [Benchmark] public HashSet<Basics.Game.Utilities.ChunkCoord> CalculateAndAllocateSet() => SelectionReference.Calculate(_center,_horizontal,_vertical);
}
