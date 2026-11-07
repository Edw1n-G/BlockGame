namespace Basics.Terrain.Chunks;

/// <summary>
/// GPU mesh retained for reuse on the main thread.
/// </summary>
public readonly record struct PooledMeshBuffer(IChunkGpuMesh Mesh);
