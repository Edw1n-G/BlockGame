using Basics.Common;
using Basics.Terrain;
using Basics.Terrain.Chunks;

namespace BlockGame.Benchmarks.Workloads;

public sealed class BlockAccessWorkload : IDisposable
{
    public static IEnumerable<string> Cases => ["positive", "negative", "mixed", "missing"];
    public const int ReadCount = 256;
    private readonly ChunkProvider _provider = new(NullChunkGpuMeshFactory.Instance);
    private readonly (int X, int Y, int Z)[] _positions;
    public int ExpectedSum { get; }

    public static (int X, int Y, int Z)[] Positions(string scenario)
    {
        if (!Cases.Contains(scenario)) throw new ArgumentException(scenario);
        return Enumerable.Range(0, ReadCount).Select(i =>
        {
            int x = i % 64, y = (i * 7) % 64, z = (i * 13) % 64;
            return scenario switch
            {
                "negative" => (-x - 1, -y - 1, -z - 1),
                "mixed" => (x - 32, y - 32, z - 32),
                _ => (x, y, z)
            };
        }).ToArray();
    }

    // Independent arithmetic oracle: floor division followed by subtraction.
    private static int Local(int world) => world - (int)Math.Floor(world / 16.0) * 16;
    private static ushort Expected(int x, int y, int z) => (ushort)(Local(x) * 256 + Local(y) * 16 + Local(z) + 1);

    public BlockAccessWorkload(string scenario)
    {
        _positions = Positions(scenario);
        if (scenario == "missing") return;
        foreach (var (x, y, z) in _positions)
        {
            var coord = new ChunkCoord((int)Math.Floor(x / 16.0), (int)Math.Floor(y / 16.0), (int)Math.Floor(z / 16.0), 0);
            if (!_provider.Chunkdata.ContainsKey(coord))
            {
                var blocks = new ushort[4096];
                for (int i = 0; i < blocks.Length; i++) blocks[i] = (ushort)(i + 1);
                _provider.Chunkdata[coord] = new ChunkData(coord, blocks);
            }
            ExpectedSum += Expected(x, y, z);
        }
    }

    public int ReadBatch()
    {
        int sum = 0;
        foreach (var (x, y, z) in _positions) sum += _provider.GetBlockAt(x, y, z);
        return sum;
    }

    public int[] Validate()
    {
        var results = new int[_positions.Length];
        bool missing = _provider.Chunkdata.IsEmpty;
        for (int i = 0; i < _positions.Length; i++)
        {
            var (x, y, z) = _positions[i];
            results[i] = _provider.GetBlockAt(x, y, z);
            if (results[i] != (missing ? 0 : Expected(x, y, z)))
                throw new InvalidOperationException("World block read disagrees with floor-division oracle");
        }
        if (ReadBatch() != ExpectedSum) throw new InvalidOperationException("Incorrect batch sum");
        return results;
    }

    public void Dispose() => _provider.Dispose();
}
