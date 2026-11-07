using System.Collections.Concurrent;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;

namespace Basics.Game.Logic.TerrainManaging;

/// <summary>Owns mesh queues and pools; disposal must run on the graphics thread.</summary>
internal sealed class ChunkMeshResources : IDisposable
{
    public ConcurrentQueue<PooledMeshBuffer> VramPool { get; } = new();
    public ConcurrentQueue<List<byte>> VertexListPool { get; } = new();
    public ConcurrentQueue<List<uint>> IndexListPool { get; } = new();
    public ConcurrentQueue<BaseMesher> UploadQueue { get; } = new();
    public ConcurrentQueue<BaseMesher> UnloadQueue { get; } = new();
    public ConcurrentDictionary<ChunkCoord, BaseMesher> LoadedChunks { get; } = new();

    public void Dispose()
    {
        foreach (var mesh in LoadedChunks.Values) mesh.Dispose();
        LoadedChunks.Clear();
        while (UploadQueue.TryDequeue(out var mesh)) mesh.Dispose();
        while (UnloadQueue.TryDequeue(out var mesh)) mesh.Dispose();
        while (VramPool.TryDequeue(out var buffer)) buffer.Mesh.Dispose();
        while (VertexListPool.TryDequeue(out _)) { }
        while (IndexListPool.TryDequeue(out _)) { }
    }
}
