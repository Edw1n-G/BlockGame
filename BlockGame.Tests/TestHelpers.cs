using System.Diagnostics;
using Basics.Game;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Logic.TerrainManaging.Meshing;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

public static class TestHelpers
{
    /// <summary>
    /// Polls <paramref name="condition"/> until it is true or the timeout elapses. Use this instead of
    /// <c>Thread.Sleep</c> when waiting for worker threads: it returns as soon as the work is done and
    /// is cancelled by xUnit v3 when the test run is aborted.
    /// </summary>
    public static async Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 10_000)
    {
        var sw = Stopwatch.StartNew();
        while (!condition() && sw.ElapsedMilliseconds < timeoutMs)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        return condition();
    }

    /// <summary>A chunk whose lower half (y = 0..7) is solid and whose upper half is air.</summary>
    public static ushort[] FloorBlocks(ushort blockId)
    {
        var blocks = new ushort[ChunkData.BlockCount];
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 8; y++)
        for (int z = 0; z < 16; z++)
        {
            blocks[ChunkData.ToIndex(x, y, z)] = blockId;
        }

        return blocks;
    }

    public static BaseMesher CreateMesher(EngineSession session, int lod, ChunkCoord coord, ChunkData data)
    {
        ChunkProvider provider = session.ChunkProvider;
        var registry = session.BlockRegistry;
        return lod switch
        {
            0 => new Lod0Mesher(coord, data, provider, registry),
            1 => new Lod1Mesher(coord, data, provider, registry),
            2 => new Lod2Mesher(coord, data, provider, registry),
            _ => throw new ArgumentOutOfRangeException(nameof(lod), lod, "Only LOD 0-2 have meshers.")
        };
    }
}
