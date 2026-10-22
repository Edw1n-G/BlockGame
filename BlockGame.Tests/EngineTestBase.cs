using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

/// <summary>
/// Base class for tests that need an isolated engine. xUnit creates a new instance of the test
/// class for every test, so each test gets a fresh <see cref="EngineSession"/> and it is
/// disposed afterwards. No GL context is needed (the block registry is initialised headless).
/// </summary>
public abstract class EngineTestBase : IDisposable
{
    protected EngineSession Session { get; }
    protected ChunkProvider Provider => Session.ChunkProvider;

    /// <summary>Block id of "core:stone" (resolved from the registry, never hard-coded).</summary>
    protected ushort Stone { get; }

    protected EngineTestBase() : this(new GameSettings { Seed = 1 })
    {
    }

    protected EngineTestBase(GameSettings settings)
    {
        Session = new EngineSession(settings);
        Stone = Session.BlockRegistry.GetBlockId("core:stone");
    }

    /// <summary>Registers a chunk in the provider. <c>null</c> blocks = an all-air chunk.</summary>
    protected ChunkData AddChunk(ChunkCoord coord, ushort[]? blocks = null)
    {
        ChunkData data = blocks is null ? new ChunkData(coord) : new ChunkData(coord, blocks);
        Provider.Chunkdata[coord] = data;
        return data;
    }

    /// <summary>
    /// Registers all-air chunks for the cube [-radius, radius]^3 at the given LOD. This is what
    /// <see cref="ChunkProvider.RemeshChunk"/> requires (all 26 neighbours must exist).
    /// </summary>
    protected void LoadEmptyCube(int radius, byte lod = 0)
    {
        for (int x = -radius; x <= radius; x++)
        for (int y = -radius; y <= radius; y++)
        for (int z = -radius; z <= radius; z++)
        {
            AddChunk(new ChunkCoord(x, y, z, lod));
        }
    }

    /// <summary>Places a block straight into chunk data (creating the chunk if needed), bypassing remesh logic.</summary>
    protected void PlaceBlock(int worldX, int worldY, int worldZ, ushort blockId)
    {
        ChunkCoord coord = ChunkData.WorldToChunkCoord(worldX, worldY, worldZ);
        if (!Provider.Chunkdata.TryGetValue(coord, out ChunkData? chunk))
        {
            chunk = AddChunk(coord);
        }

        var (lx, ly, lz) = ChunkData.WorldToLocal(worldX, worldY, worldZ);
        chunk.SetBlock(lx, ly, lz, blockId);
    }

    /// <summary>A chunk whose lower half (y = 0..7) is solid and whose upper half is air.</summary>
    protected ushort[] FloorBlocks() => TestHelpers.FloorBlocks(Stone);

    /// <summary>A chunk that is solid everywhere.</summary>
    protected ushort[] SolidBlocks()
    {
        var blocks = new ushort[ChunkData.BlockCount];
        Array.Fill(blocks, Stone);
        return blocks;
    }

    protected BaseMesher CreateMesher(int lod, ChunkCoord coord, ChunkData data)
        => TestHelpers.CreateMesher(Session, lod, coord, data);

    public void Dispose()
    {
        Session.Dispose();
        GC.SuppressFinalize(this);
    }
}
