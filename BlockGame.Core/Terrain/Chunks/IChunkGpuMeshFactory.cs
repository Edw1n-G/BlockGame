namespace Basics.Terrain.Chunks;

/// <summary>Uploads completed CPU mesh data on the main thread.</summary>
public interface IChunkGpuMeshFactory
{
    IChunkGpuMesh Create(ReadOnlySpan<byte> vertices, ReadOnlySpan<uint> indices);
    void Update(IChunkGpuMesh mesh, ReadOnlySpan<byte> vertices, ReadOnlySpan<uint> indices);
}
