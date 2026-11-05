using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;
using Silk.NET.Maths;

namespace BlockGame.Tests;

/// <summary>
/// Two <see cref="EngineSession"/>s in one process must not share any state. These tests only use
/// instance APIs, so they are safe to run in parallel with everything else.
/// </summary>
public class SessionIsolationTests
{
    private static readonly ChunkCoord Origin = new(0, 0, 0, 0);

    private static EngineSession NewSession(int seed, int renderDistance = 8)
        => new(new GameSettings { Seed = seed, RenderDistance = renderDistance });

    [Fact]
    public void Settings_AreIsolated()
    {
        using var a = NewSession(101, 12);
        using var b = NewSession(999, 24);

        Assert.Equal(101, a.Settings.Seed);
        Assert.Equal(999, b.Settings.Seed);
        Assert.Equal(12, a.Settings.RenderDistance);
        Assert.Equal(24, b.Settings.RenderDistance);
        Assert.NotSame(a.Settings, b.Settings);
    }

    [Fact]
    public void Subsystems_AreNotShared()
    {
        using var a = NewSession(1);
        using var b = NewSession(1);

        Assert.NotSame(a.ChunkProvider, b.ChunkProvider);
        Assert.NotSame(a.World, b.World);
        Assert.NotSame(a.Physics, b.Physics);
        Assert.NotSame(a.BlockRegistry, b.BlockRegistry);
        Assert.NotSame(a.TerrainGenerator, b.TerrainGenerator);
        Assert.NotSame(a.JobScheduler, b.JobScheduler);
    }

    [Fact]
    public void BlockEdits_DoNotLeakBetweenSessions()
    {
        using var a = NewSession(101);
        using var b = NewSession(999);
        a.ChunkProvider.Chunkdata[Origin] = new ChunkData(Origin);
        b.ChunkProvider.Chunkdata[Origin] = new ChunkData(Origin);
        ushort stone = a.BlockRegistry.GetBlockId("core:stone");
        ushort dirt = b.BlockRegistry.GetBlockId("core:dirt");

        Assert.True(a.World.SetBlock(5, 5, 5, stone));

        Assert.Equal(stone, a.World.GetBlockAt(5, 5, 5));
        Assert.Equal((ushort)0, b.World.GetBlockAt(5, 5, 5));

        Assert.True(b.World.SetBlock(5, 5, 5, dirt));

        Assert.Equal(dirt, b.World.GetBlockAt(5, 5, 5));
        Assert.Equal(stone, a.World.GetBlockAt(5, 5, 5)); // unchanged by B's edit
    }

    [Fact]
    public void ChunkData_IsNotShared()
    {
        using var a = NewSession(1);
        using var b = NewSession(1);

        a.ChunkProvider.Chunkdata[Origin] = new ChunkData(Origin);

        Assert.Single(a.ChunkProvider.Chunkdata);
        Assert.Empty(b.ChunkProvider.Chunkdata);
        Assert.Null(b.ChunkProvider.GetChunkData(Origin));
    }

    [Fact]
    public void Raycasts_OnlySeeTheirOwnWorld()
    {
        using var a = NewSession(1);
        using var b = NewSession(1);
        a.ChunkProvider.Chunkdata[Origin] = new ChunkData(Origin);
        b.ChunkProvider.Chunkdata[Origin] = new ChunkData(Origin);
        a.World.SetBlock(5, 5, 5, a.BlockRegistry.GetBlockId("core:stone"));

        var start = new Vector3D<float>(5.5f, 10.5f, 5.5f);
        var down = new Vector3D<float>(0f, -1f, 0f);

        Assert.True(a.Physics.CastRay(start, down, 10f).Hit);
        Assert.False(b.Physics.CastRay(start, down, 10f).Hit);
    }

    [Fact]
    public void MemoryPools_AreNotShared()
    {
        using var a = NewSession(1);
        using var b = NewSession(1);

        a.ChunkProvider.VertexListPool.Enqueue([1, 2, 3]);
        b.ChunkProvider.IndexListPool.Enqueue([10u, 20u]);

        Assert.Single(a.ChunkProvider.VertexListPool);
        Assert.Empty(b.ChunkProvider.VertexListPool);
        Assert.Empty(a.ChunkProvider.IndexListPool);
        Assert.Single(b.ChunkProvider.IndexListPool);
    }

    [Fact]
    public void ManySessions_CanRunConcurrentlyWithoutInterfering()
    {
        const int sessions = 8;
        var results = new ushort[sessions];

        Parallel.For(0, sessions, i =>
        {
            using var s = NewSession(seed: i + 1);
            s.ChunkProvider.Chunkdata[Origin] = new ChunkData(Origin);
            ushort id = (ushort)(i + 1); // a distinct id per session
            for (int n = 0; n < 200; n++)
            {
                s.World.SetBlock(n % 16, 3, 3, id);
            }

            results[i] = s.World.GetBlockAt(7, 3, 3);
        });

        Assert.Equal(Enumerable.Range(1, sessions).Select(n => (ushort)n), results);
    }
}
