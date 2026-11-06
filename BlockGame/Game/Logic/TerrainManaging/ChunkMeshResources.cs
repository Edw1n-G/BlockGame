using System.Collections.Concurrent;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;
using Silk.NET.OpenGL;

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
    private GL? _gl;

    public void AttachGraphicsContext(GL gl) => _gl = gl;

    public void Dispose()
    {
        foreach (var mesh in LoadedChunks.Values) mesh.Dispose();
        LoadedChunks.Clear();
        while (UploadQueue.TryDequeue(out var mesh)) mesh.Dispose();
        while (UnloadQueue.TryDequeue(out var mesh)) mesh.Dispose();
        while (VramPool.TryDequeue(out var buffer))
        {
            if (_gl is null) continue;
            _gl.DeleteVertexArray(buffer.Vao);
            _gl.DeleteBuffer(buffer.Vbo);
            _gl.DeleteBuffer(buffer.Ebo);
        }
        while (VertexListPool.TryDequeue(out _)) { }
        while (IndexListPool.TryDequeue(out _)) { }
    }
}
