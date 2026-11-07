using Silk.NET.OpenGL;
using static Basics.Game.Logic.TerrainManaging.Meshing.ChunkVertexFormat;

namespace Basics.Game.Graphics;

internal static class ChunkVertexLayout
{
    public static unsafe void Configure(GL gl)
    {
        gl.VertexAttribPointer(0, PositionComponents, VertexAttribPointerType.Float, false, VertexStride, (void*)PositionOffset);
        gl.EnableVertexAttribArray(0);

        gl.VertexAttribIPointer(1, TextureLayerComponents, VertexAttribIType.UnsignedShort, VertexStride, (void*)TextureLayerOffset);
        gl.EnableVertexAttribArray(1);

        gl.VertexAttribIPointer(2, AmbientOcclusionComponents, VertexAttribIType.UnsignedByte, VertexStride, (void*)AmbientOcclusionOffset);
        gl.EnableVertexAttribArray(2);
    }
}
