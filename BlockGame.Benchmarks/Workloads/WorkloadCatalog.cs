using Basics.Common;
using Basics.Settings;
using Basics.Terrain.Selection;

namespace BlockGame.Benchmarks.Workloads;

public static class WorkloadCatalog
{
    public const int Version = 1;
    // Frozen rather than inherited from a mutable engine default.
    public const string NodeTree = "KBE@CgL8EFQkVCRcJIQBvEgM8CQ0AB@CkG@BY0MUAwAA8EIEAhcJFgMAAKBCCgQIAACAPwwDzczMPgwCFwkbCRYDAABIwgr/BQAMAw@AEAMAhkJJQB7@BCSEAKVwPPQkNAAI@BJBiQLWDk0PRPsUTg9BA==";
    public static readonly string[] BlockNames = ["core:air", "core:dirt", "core:grass", "core:snow", "core:stone"];
    public static IEnumerable<string> MeshCases => ["null-air", "allocated-air", "exposed-solid", "occluded-solid", "floor", "checkerboard", "frozen-terrain"];
    public static IEnumerable<string> TerrainCases => ["air-s1", "solid-s1", "surface-s1", "outside-s1", "air-s42", "solid-s42", "surface-s42", "outside-s42"];
    public static IEnumerable<string> NoiseCases => ["grid-s1", "grid-s42"];
    public static IEnumerable<string> SelectionCases => ["zero", "vertical", "negative", "typical"];
    public static GameSettings Settings(int seed = 1) => new() { Seed = seed, Worldtype = NodeTree, MapSize = 100 };

    public static (int Seed, ChunkCoord Coord, string Kind) Terrain(string id)
    {
        if (!TerrainCases.Contains(id)) throw new ArgumentException($"Unknown terrain scenario: {id}");
        int seed = id.EndsWith("42", StringComparison.Ordinal) ? 42 : 1;
        string kind = id.Split('-')[0];
        return (seed, kind switch {
            "air" => new(0, 64, 0, 0), "solid" => new(0, -64, 0, 0),
            "surface" => new(0, seed == 1 ? -58 : -49, 0, 0), "outside" => new(101, 0, 0, 0),
            _ => throw new ArgumentException(id)
        }, kind);
    }

    public static (ChunkCoord Center, int Horizontal, int Vertical) Selection(string id) => id switch
    {
        "zero" => (new(0, 0, 0, 0), 0, 0),
        "vertical" => (new(-4, -2, 3, 0), 0, 3),
        "negative" => (new(-4, -2, 3, 0), 1, 1),
        "typical" => (new(12, 6, -8, 0), 20, 15),
        _ => throw new ArgumentException($"Unknown selection scenario: {id}")
    };

    public static (int Seed, int X, int Y, int Z) Noise(string id)
    {
        if (!NoiseCases.Contains(id)) throw new ArgumentException(id);
        var t = Terrain(id.EndsWith("42", StringComparison.Ordinal) ? "surface-s42" : "surface-s1");
        return (t.Seed, t.Coord.X * 16, t.Coord.Y * 16, t.Coord.Z * 16);
    }

    // Friend access keeps the extracted selection implementation internal to the engine.
    public static HashSet<ChunkCoord> Select(ChunkCoord center, int horizontal, int vertical) =>
        ChunkSelection.Calculate(center, horizontal, vertical);
}
