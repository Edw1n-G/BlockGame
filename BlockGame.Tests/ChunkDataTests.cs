using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class ChunkDataTests
{
    private static readonly ChunkCoord Origin = new(0, 0, 0, 0);

    // ---------- Lazy allocation ----------

    [Fact]
    public void EmptyChunk_HasNoBlockArray_AndReadsAsAir()
    {
        var chunk = new ChunkData(Origin);

        Assert.Null(chunk.Blocks);
        Assert.Equal((ushort)0, chunk.GetBlock(3, 4, 5));
        Assert.True(chunk.IsEmpty());
    }

    [Fact]
    public void SetBlock_Air_OnEmptyChunk_DoesNotAllocateOrMarkDirty()
    {
        var chunk = new ChunkData(Origin);

        chunk.SetBlock(1, 2, 3, 0);

        Assert.Null(chunk.Blocks);
        Assert.False(chunk.IsDirty);
    }

    [Fact]
    public void SetBlock_NonAir_AllocatesAndMarksDirty()
    {
        var chunk = new ChunkData(Origin);

        chunk.SetBlock(1, 2, 3, 7);

        Assert.NotNull(chunk.Blocks);
        Assert.Equal(ChunkData.BlockCount, chunk.Blocks!.Length);
        Assert.Equal((ushort)7, chunk.GetBlock(1, 2, 3));
        Assert.True(chunk.IsDirty);
        Assert.False(chunk.IsEmpty());
    }

    [Fact]
    public void SetBlock_Air_OnAllocatedChunk_ClearsTheBlock()
    {
        var chunk = new ChunkData(Origin);
        chunk.SetBlock(1, 2, 3, 7);

        chunk.SetBlock(1, 2, 3, 0);

        Assert.Equal((ushort)0, chunk.GetBlock(1, 2, 3));
        Assert.True(chunk.IsEmpty());
    }

    [Fact]
    public void IsEmpty_AllocatedButAllZero_IsTrue()
    {
        var chunk = new ChunkData(Origin, new ushort[ChunkData.BlockCount]);

        Assert.True(chunk.IsEmpty());
    }

    // ---------- Indexing ----------

    [Theory]
    [InlineData(0, 0, 0, 0)]
    [InlineData(1, 0, 0, 256)]
    [InlineData(0, 1, 0, 16)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(15, 15, 15, 4095)]
    public void ToIndex_UsesXMajorLayout(int x, int y, int z, int expected)
    {
        Assert.Equal(expected, ChunkData.ToIndex(x, y, z));
    }

    // ---------- Bounds-checked access ----------

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(16, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 16, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(0, 0, 16)]
    public void SafeAccessors_OutOfRange_AreHarmless(int x, int y, int z)
    {
        var blocks = new ushort[ChunkData.BlockCount];
        Array.Fill(blocks, (ushort)1);
        var chunk = new ChunkData(Origin, blocks);

        Assert.Equal((ushort)0, chunk.GetBlockSafe(x, y, z));
        Assert.False(chunk.IsBlock(x, y, z));
        Assert.False(chunk.SetBlockSafe(x, y, z, 9));
        Assert.All(chunk.Blocks!, b => Assert.Equal((ushort)1, b)); // nothing was written
    }

    [Fact]
    public void SafeAccessors_InRange_BehaveLikeTheUncheckedOnes()
    {
        var chunk = new ChunkData(Origin);

        Assert.True(chunk.SetBlockSafe(15, 15, 15, 5));

        Assert.Equal((ushort)5, chunk.GetBlockSafe(15, 15, 15));
        Assert.True(chunk.IsBlock(15, 15, 15));
        Assert.False(chunk.IsBlock(0, 0, 0));
    }

    [Fact]
    public void SetBlockSafe_Air_OnEmptyChunk_SucceedsWithoutAllocating()
    {
        var chunk = new ChunkData(Origin);

        Assert.True(chunk.SetBlockSafe(1, 1, 1, 0));
        Assert.Null(chunk.Blocks);
    }

    // ---------- World -> chunk-local conversion (negative coordinates wrap correctly) ----------

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 15)]
    [InlineData(16, 0)]
    [InlineData(31, 15)]
    [InlineData(-1, 15)]
    [InlineData(-16, 0)]
    [InlineData(-17, 15)]
    public void WorldToLocal_WrapsIntoZeroToFifteen(int world, int expectedLocal)
    {
        var (x, y, z) = ChunkData.WorldToLocal(world, world, world);

        Assert.Equal((expectedLocal, expectedLocal, expectedLocal), (x, y, z));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(15, 0)]
    [InlineData(16, 1)]
    [InlineData(-1, -1)]
    [InlineData(-16, -1)]
    [InlineData(-17, -2)]
    public void WorldToChunkCoord_UsesFloorDivision(int world, int expectedChunk)
    {
        ChunkCoord coord = ChunkData.WorldToChunkCoord(world, world, world, 2);

        Assert.Equal(new ChunkCoord(expectedChunk, expectedChunk, expectedChunk, 2), coord);
    }
}
