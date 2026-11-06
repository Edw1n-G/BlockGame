using BenchmarkDotNet.Attributes;
using BlockGame.Workloads;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;
using BlockGame.Benchmarking;

namespace BlockGame.Benchmarks;

[ScenarioFamily("mesh")]
public class MeshingBenchmarks
{
    public IEnumerable<string> Cases => WorkloadCatalog.MeshCases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private WorkloadEnvironment _environment = null!;
    private MeshWorkload _fixture = null!;
    private string _fingerprint = null!;
    [GlobalSetup] public void Setup()
    {
        _environment = new(); _fixture = new(Scenario, _environment.Registry);
        _fingerprint = _fixture.Fingerprint();
        string outputFingerprint;
        using (var warm = _fixture.Build()) { _ = MeshWorkload.Summary(warm); outputFingerprint=OutputChecks.Mesh(warm); }
        _fixture.ValidateRetainedState();
        RunEvidence.Child(GetType(), Scenario, _environment, new { fingerprint = _fingerprint, neighbors = 26, lod = 0, poolPolicy = "one representative warmed pair; return in operation" },outputFingerprint);
    }
    [Benchmark(Description = "BuildAndReturnBuffers")]
    public int BuildAndReturnBuffers()
    {
        using var mesh = _fixture.Build();
        return MeshWorkload.Summary(mesh);
    }
    [GlobalCleanup] public void Cleanup()
    {
        try
        {
            _fixture.ValidateRetainedState();
            if (_fingerprint != _fixture.Fingerprint()) throw new InvalidOperationException("Meshing mutated workload input");
        }
        finally { _fixture.Dispose(); _environment.Dispose(); }
    }
}

[ScenarioFamily("terrain")]
public class TerrainBenchmarks
{
    public IEnumerable<string> Cases => WorkloadCatalog.TerrainCases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private WorkloadEnvironment _environment = null!;
    private ChunkCoord _coord;
    [GlobalSetup] public void Setup()
    {
        var t = WorkloadCatalog.Terrain(Scenario); _environment = new(t.Seed); _coord = t.Coord;
        RunEvidence.Child(GetType(), Scenario, _environment, new { t.Seed, x = t.Coord.X, y = t.Coord.Y, z = t.Coord.Z, lod = 0, mapSize = 100, t.Kind },OutputChecks.Terrain(_environment.Generator.GenerateChunk(_coord)));
    }
    [Benchmark] public ChunkData GenerateChunk() => _environment.Generator.GenerateChunk(_coord);
    [GlobalCleanup] public void Cleanup() => _environment.Dispose();
}

[ScenarioFamily("noise")]
public class NoiseBenchmarks
{
    public IEnumerable<string> Cases => WorkloadCatalog.NoiseCases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private WorkloadEnvironment _environment = null!;
    private int _x, _y, _z;
    [GlobalSetup] public void Setup()
    {
        var n = WorkloadCatalog.Noise(Scenario); _environment = new(n.Seed); (_x, _y, _z) = (n.X, n.Y, n.Z);
        RunEvidence.Child(GetType(), Scenario, _environment, new { n.Seed, n.X, n.Y, n.Z, sizeX = 16, sizeY = 17, sizeZ = 16, step = 1 },OutputChecks.Noise(_environment.Noise.GetNoiseValues(_x,_y,_z,16,17,16,1)));
    }
    [Benchmark] public float[] GenerateGridAndArray() => _environment.Noise.GetNoiseValues(_x, _y, _z, 16, 17, 16, 1);
    [GlobalCleanup] public void Cleanup() => _environment.Dispose();
}

[ScenarioFamily("selection-full")]
public class SelectionBenchmarks
{
    public IEnumerable<string> Cases => WorkloadCatalog.SelectionCases;
    [ParamsSource(nameof(Cases))] public string Scenario { get; set; } = null!;
    private ChunkCoord _center;
    private int _horizontal, _vertical;
    [GlobalSetup] public void Setup()
    {
        (_center, _horizontal, _vertical) = WorkloadCatalog.Selection(Scenario);
        RunEvidence.Child(GetType(), Scenario, input: new { x = _center.X, y = _center.Y, z = _center.Z, horizontal = _horizontal, vertical = _vertical, lod = 0 },outputFingerprint:OutputChecks.Selection(WorkloadCatalog.Select(_center,_horizontal,_vertical)));
    }
    [Benchmark] public HashSet<ChunkCoord> CalculateAndAllocateSet() => WorkloadCatalog.Select(_center, _horizontal, _vertical);
}
