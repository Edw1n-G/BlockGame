namespace Basics.Game.Logic.TerrainManaging;

public sealed class NullChunkGpuMeshFactory : IChunkGpuMeshFactory
{
    public static NullChunkGpuMeshFactory Instance { get; } = new();
    private static readonly NullChunkGpuMesh Mesh = new();
    public IChunkGpuMesh Create(ReadOnlySpan<byte> vertices, ReadOnlySpan<uint> indices) => Mesh;
    public void Update(IChunkGpuMesh mesh, ReadOnlySpan<byte> vertices, ReadOnlySpan<uint> indices) { }
}

internal sealed class NullChunkGpuMesh : IChunkGpuMesh
{
    public bool CanReuse => false;
    public void Dispose() { }
}
