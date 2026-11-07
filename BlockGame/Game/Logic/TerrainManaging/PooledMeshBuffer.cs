namespace Basics.Game.Logic.TerrainManaging;

/// <summary>
/// GPU mesh retained for reuse on the main thread.
/// </summary>
public readonly record struct PooledMeshBuffer(IChunkGpuMesh Mesh);
