using Basics.Terrain.Chunks;
using Silk.NET.OpenGL;

namespace Basics.Graphics.Chunks;

internal sealed class GlChunkMeshFactory(GL gl) : IChunkGpuMeshFactory
{
    public IChunkGpuMesh Create(ReadOnlySpan<byte> vertices, ReadOnlySpan<uint> indices)
    {
        var mesh = new GlChunkMesh(gl);
        mesh.Create(vertices, indices);
        return mesh;
    }

    public void Update(IChunkGpuMesh mesh, ReadOnlySpan<byte> vertices, ReadOnlySpan<uint> indices) =>
        ((GlChunkMesh)mesh).Update(vertices, indices);
}
