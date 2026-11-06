using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Basics.Game.Configurations;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;

namespace BlockGame.Workloads;

public sealed record FrozenChunk(int X, int Y, int Z, int[] Runs);
public sealed record FrozenNeighborhood(int Version, int Seed, string NodeTree, string NativeSha256, uint FeatureSet, string[] Names, FrozenChunk[] Chunks);

public sealed class MeshWorkload : IDisposable
{
    public string Scenario { get; }
    public ChunkProvider Provider { get; } = new(settings: WorkloadCatalog.Settings());
    public ChunkData Data { get; }
    private readonly BlockRegistry _registry;
    public MeshWorkload(string scenario, BlockRegistry registry)
    {
        if (!WorkloadCatalog.MeshCases.Contains(scenario)) throw new ArgumentException(scenario);
        Scenario = scenario;
        _registry = registry;
        ushort stone = registry.GetBlockId("core:stone");
        if (scenario == "frozen-terrain")
        {
            using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BlockGame.Workloads.FrozenTerrain.json")
                ?? throw new FileNotFoundException("Frozen terrain fixture missing");
            var frozen = JsonSerializer.Deserialize<FrozenNeighborhood>(stream)!;
            if (frozen.Version != WorkloadCatalog.Version || frozen.Chunks.Length != 27)
                throw new InvalidDataException("Unexpected frozen workload version/neighborhood");
            foreach (var chunk in frozen.Chunks)
            {
                var blocks = new ushort[4096]; int offset = 0;
                for (int i = 0; i < chunk.Runs.Length; i += 2)
                {
                    ushort id = registry.GetBlockId(frozen.Names[chunk.Runs[i]]);
                    int count = chunk.Runs[i + 1];
                    if (count <= 0 || offset + count > blocks.Length) throw new InvalidDataException("Invalid frozen run");
                    Array.Fill(blocks, id, offset, count); offset += count;
                }
                if (offset != blocks.Length) throw new InvalidDataException("Incomplete frozen chunk");
                var coord = new ChunkCoord(chunk.X, chunk.Y, chunk.Z, 0);
                if (!Provider.Chunkdata.TryAdd(coord, new ChunkData(coord, blocks))) throw new InvalidDataException("Duplicate frozen chunk");
            }
        }
        else
        {
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dz = -1; dz <= 1; dz++)
            {
                var coord = new ChunkCoord(dx, dy, dz, 0);
                ushort[]? blocks = null;
                if (scenario == "occluded-solid" || (dx == 0 && dy == 0 && dz == 0 && scenario != "null-air"))
                {
                    blocks = new ushort[4096];
                    for (int x = 0; x < 16; x++)
                    for (int y = 0; y < 16; y++)
                    for (int z = 0; z < 16; z++)
                        if (scenario is "exposed-solid" or "occluded-solid" || scenario == "floor" && y < 8 || scenario == "checkerboard" && (x + y + z) % 2 == 0)
                            blocks[ChunkData.ToIndex(x, y, z)] = stone;
                }
                Provider.Chunkdata[coord] = blocks is null ? new ChunkData(coord) : new ChunkData(coord, blocks);
            }
        }
        Data = Provider.Chunkdata[new ChunkCoord(0, 0, 0, 0)];
    }

    public Lod0Mesher Build() => new(Data.Coord, Data, Provider, _registry);
    public static ReadOnlySpan<byte> Vertices(BaseMesher mesh) => mesh.CpuVertices;
    public static ReadOnlySpan<uint> Indices(BaseMesher mesh) => mesh.CpuIndices;
    public static int Summary(BaseMesher mesh) => mesh.CpuVertices.Length + mesh.CpuIndices.Length;

    public string Fingerprint()
    {
        using var stream = new MemoryStream();
        foreach (var chunk in Provider.Chunkdata.Values.OrderBy(c => c.Coord.X).ThenBy(c => c.Coord.Y).ThenBy(c => c.Coord.Z))
        {
            stream.WriteByte(chunk.Blocks is null ? (byte)0 : (byte)1);
            if (chunk.Blocks is { } blocks) stream.Write(MemoryMarshal.AsBytes(blocks.AsSpan()));
        }
        return WorkloadEnvironment.Hash(stream.ToArray());
    }

    public void ValidateRetainedState()
    {
        if (Provider.Chunkdata.Count != 27 || Provider.VertexListPool.Count != 1 || Provider.IndexListPool.Count != 1 ||
            !Provider.VramPool.IsEmpty || !Provider.UploadQueue.IsEmpty || !Provider.UnloadQueue.IsEmpty ||
            !Provider.LoadedChunks.IsEmpty || !Provider.PendingMeshRequests.IsEmpty || !Provider.QueuedForMeshing.IsEmpty)
            throw new InvalidOperationException("CPU workload leaked buffers or changed queues/neighborhood");
    }
    public void Dispose() => Provider.Dispose();
}
