namespace Basics.Terrain.Meshing;

internal static class ChunkVertexFormat
{
    public const int VertexStride = 16;
    public const int PositionComponents = 3;
    public const int TextureLayerComponents = 1;
    public const int AmbientOcclusionComponents = 1;
    public const int PositionOffset = 0;
    public const int TextureLayerOffset = 12;
    public const int AmbientOcclusionOffset = 14;
}
