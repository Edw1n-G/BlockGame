using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class TerrainGenerationTests
{
    private static EngineSession NewSession(int seed, int mapSize = 100)
        => new(new GameSettings { Seed = seed, MapSize = mapSize });

    private static IEnumerable<ChunkCoord> SampleCoords()
    {
        for (int x = -2; x <= 2; x++)
        for (int y = -1; y <= 3; y++)
        for (int z = -2; z <= 2; z++)
        {
            yield return new ChunkCoord(x, y, z, 0);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(12345)]
    public void SameSeed_ProducesIdenticalChunks_AcrossSessions(int seed)
    {
        using var a = NewSession(seed);
        using var b = NewSession(seed);

        foreach (ChunkCoord coord in SampleCoords())
        {
            ChunkData chunkA = a.TerrainGenerator.GenerateChunk(coord);
            ChunkData chunkB = b.TerrainGenerator.GenerateChunk(coord);

            Assert.Equal(chunkA.Blocks, chunkB.Blocks);
        }
    }

    [Fact]
    public void GeneratingTheSameChunkTwice_IsRepeatable()
    {
        using var session = NewSession(7);
        var coord = new ChunkCoord(1, 0, -1, 0);

        ChunkData first = session.TerrainGenerator.GenerateChunk(coord);
        ChunkData second = session.TerrainGenerator.GenerateChunk(coord);

        Assert.Equal(first.Blocks, second.Blocks);
        Assert.Equal(coord, first.Coord);
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentTerrain()
    {
        using var a = NewSession(1);
        using var b = NewSession(2);

        bool anyDifference = SampleCoords().Any(coord =>
            !a.TerrainGenerator.GenerateChunk(coord).Blocks!
                .AsSpan()
                .SequenceEqual(b.TerrainGenerator.GenerateChunk(coord).Blocks!));

        Assert.True(anyDifference, "Seeds 1 and 2 generated identical terrain in every sampled chunk.");
    }

    [Fact]
    public void GeneratedBlocks_AreAllRegisteredTerrainBlocks()
    {
        using var session = NewSession(1);
        var registry = session.BlockRegistry;
        var allowed = new HashSet<ushort>
        {
            0,
            registry.GetBlockId("core:dirt"),
            registry.GetBlockId("core:grass"),
            registry.GetBlockId("core:stone"),
            registry.GetBlockId("core:snow"),
        };

        foreach (ChunkCoord coord in SampleCoords())
        {
            ChunkData chunk = session.TerrainGenerator.GenerateChunk(coord);

            Assert.NotNull(chunk.Blocks);
            Assert.All(chunk.Blocks!, id => Assert.Contains(id, allowed));
        }
    }

    [Theory]
    [InlineData(11, 0, 0)]
    [InlineData(-11, 0, 0)]
    [InlineData(0, 0, 11)]
    [InlineData(0, 0, -11)]
    public void ChunksOutsideTheMap_AreAir_ButStillExist(int x, int y, int z)
    {
        using var session = NewSession(1, mapSize: 10);
        var coord = new ChunkCoord(x, y, z, 0);

        ChunkData chunk = session.TerrainGenerator.GenerateChunk(coord);

        Assert.Equal(coord, chunk.Coord);
        Assert.True(chunk.IsEmpty());
    }

    [Fact]
    public void Generator_UsesTheSettingsOfItsOwnSession()
    {
        using var small = NewSession(1, mapSize: 10);
        using var large = NewSession(1, mapSize: 100);
        var coord = new ChunkCoord(20, 0, 0, 0); // outside the small map, inside the large one

        Assert.True(small.TerrainGenerator.GenerateChunk(coord).IsEmpty());
        Assert.NotNull(large.TerrainGenerator.GenerateChunk(coord).Blocks);
    }
}
