using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

/// <summary>
/// Meshing is pure CPU work (vertex/index lists), so it runs without OpenGL. The tests only use the
/// public surface of the meshers (<c>IsEmpty</c>, <c>ChunkPosition</c>, <c>Dispose</c>).
/// </summary>
public class MesherTests : EngineTestBase
{
    // ---------- Output ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void AirChunk_ProducesAnEmptyMesh(int lod)
    {
        var coord = new ChunkCoord(0, 0, 0, (byte)lod);

        using var mesher = CreateMesher(lod, coord, new ChunkData(coord));

        Assert.True(mesher.IsEmpty);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void ChunkWithAnExposedSurface_ProducesAMesh(int lod)
    {
        var coord = new ChunkCoord(0, 0, 0, (byte)lod);
        var chunk = new ChunkData(coord, FloorBlocks()); // solid lower half, air above

        using var mesher = CreateMesher(lod, coord, chunk);

        Assert.False(mesher.IsEmpty);
    }

    [Fact]
    public void SolidChunk_SurroundedBySolidChunks_HasNoVisibleFaces()
    {
        var center = new ChunkCoord(0, 0, 0, 0);
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            AddChunk(new ChunkCoord(x, y, z, 0), SolidBlocks());
        }

        using var mesher = CreateMesher(0, center, Provider.Chunkdata[center]);

        Assert.True(mesher.IsEmpty, "Faces between touching solid chunks must be culled.");
    }

    [Fact]
    public void Meshing_DoesNotModifyTheBlockData()
    {
        var coord = new ChunkCoord(0, 0, 0, 0);
        ushort[] blocks = FloorBlocks();
        ushort[] copy = (ushort[])blocks.Clone();
        var chunk = new ChunkData(coord, blocks);

        using var mesher = CreateMesher(0, coord, chunk);

        Assert.Equal(copy, chunk.Blocks);
        Assert.False(chunk.IsDirty);
    }

    // ---------- ChunkPosition ----------

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Mesher_RemembersItsChunkPosition(int lod)
    {
        var coord = new ChunkCoord(3, -2, 5, (byte)lod);
        var chunk = new ChunkData(coord, FloorBlocks());

        using var mesher = CreateMesher(lod, coord, chunk);

        Assert.Equal(coord, mesher.ChunkPosition);
    }

    [Fact]
    public void Lod0_AirChunk_StillRemembersItsChunkPosition()
    {
        var coord = new ChunkCoord(3, -2, 5, 0);

        using var mesher = CreateMesher(0, coord, new ChunkData(coord));

        Assert.Equal(coord, mesher.ChunkPosition);
    }

    // Lod1Mesher / Lod2Mesher assign ChunkPosition *after* their early return for chunks without a
    // block array, so an air chunk keeps the default position (0,0,0). Lod0Mesher does it in the
    // right order. Remove the Skip once the assignment is moved above the early return.
    [Fact(Skip = "Known bug: Lod1Mesher sets ChunkPosition after the early return for air chunks.")]
    public void Lod1_AirChunk_StillRemembersItsChunkPosition()
    {
        var coord = new ChunkCoord(3, -2, 5, 1);

        using var mesher = CreateMesher(1, coord, new ChunkData(coord));

        Assert.Equal(coord, mesher.ChunkPosition);
    }

    [Fact(Skip = "Known bug: Lod2Mesher sets ChunkPosition after the early return for air chunks.")]
    public void Lod2_AirChunk_StillRemembersItsChunkPosition()
    {
        var coord = new ChunkCoord(3, -2, 5, 2);

        using var mesher = CreateMesher(2, coord, new ChunkData(coord));

        Assert.Equal(coord, mesher.ChunkPosition);
    }

    // ---------- Pooling ----------

    [Fact]
    public void Dispose_ReturnsTheListsToThePool()
    {
        var coord = new ChunkCoord(0, 0, 0, 0);
        Assert.Empty(Provider.VertexListPool);
        Assert.Empty(Provider.IndexListPool);

        var mesher = CreateMesher(0, coord, new ChunkData(coord));
        mesher.Dispose();

        Assert.Single(Provider.VertexListPool);
        Assert.Single(Provider.IndexListPool);
    }

    [Fact]
    public void Dispose_Twice_DoesNotPoolTheListsTwice()
    {
        var coord = new ChunkCoord(0, 0, 0, 0);
        var mesher = CreateMesher(0, coord, new ChunkData(coord));

        mesher.Dispose();
        mesher.Dispose();

        Assert.Single(Provider.VertexListPool);
        Assert.Single(Provider.IndexListPool);
    }

    [Fact]
    public void NewMesher_ReusesPooledLists()
    {
        var coord = new ChunkCoord(0, 0, 0, 0);
        CreateMesher(0, coord, new ChunkData(coord)).Dispose();
        Assert.Single(Provider.VertexListPool);

        using var second = CreateMesher(0, coord, new ChunkData(coord));

        Assert.Empty(Provider.VertexListPool); // taken from the pool instead of allocating
        Assert.Empty(Provider.IndexListPool);
    }
}
