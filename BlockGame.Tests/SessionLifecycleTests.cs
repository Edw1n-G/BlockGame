using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

/// <summary>
/// Replaces the old "50 benchmark iterations in one process" loop: sessions must start clean,
/// release everything on dispose, and leave nothing behind for the next one. Timing is deliberately
/// not asserted here (use BenchmarkDotNet for numbers).
/// </summary>
public class SessionLifecycleTests
{
    private static readonly ChunkCoord Origin = new(0, 0, 0, 0);

    private static void AssertPristine(EngineSession session, string because)
    {
        ChunkProvider p = session.ChunkProvider;
        Assert.True(p.Chunkdata.IsEmpty, $"{because}: Chunkdata");
        Assert.True(p.LoadedChunks.IsEmpty, $"{because}: LoadedChunks");
        Assert.True(p.UploadQueue.IsEmpty, $"{because}: UploadQueue");
        Assert.True(p.UnloadQueue.IsEmpty, $"{because}: UnloadQueue");
        Assert.True(p.PendingMeshRequests.IsEmpty, $"{because}: PendingMeshRequests");
        Assert.True(p.QueuedForMeshing.IsEmpty, $"{because}: QueuedForMeshing");
        Assert.True(p.VertexListPool.IsEmpty, $"{because}: VertexListPool");
        Assert.True(p.IndexListPool.IsEmpty, $"{because}: IndexListPool");
    }

    [Fact]
    public void ManySequentialSessions_EachStartClean_AndDoNotLeakIntoTheNext()
    {
        for (int i = 0; i < 10; i++)
        {
            var settings = new GameSettings { Seed = i * 10 + 1, MapSize = 100, RenderDistance = 10 };
            using var session = new EngineSession(settings);
            using (session.Activate())
            {
                AssertPristine(session, $"iteration {i}");

                for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                {
                    var c = new ChunkCoord(x, 0, z, 0);
                    session.ChunkProvider.Chunkdata[c] = session.TerrainGenerator.GenerateChunk(c);
                }

                Assert.Equal(9, session.ChunkProvider.Chunkdata.Count);

                ushort stone = session.BlockRegistry.GetBlockId("core:stone");
                Assert.True(World.Current.SetBlock(0, 5, 0, stone));
                Assert.Equal(stone, World.GetBlock(0, 5, 0));
                Assert.Equal(settings.Seed, GameSettings.Current.Seed);
            }
        }
    }

    [Fact]
    public void Dispose_ReleasesEverythingTheProviderHolds()
    {
        var session = new EngineSession(new GameSettings());
        ChunkProvider p = session.ChunkProvider;
        p.Chunkdata[Origin] = new ChunkData(Origin);
        p.LoadedChunks[Origin] = TestHelpers.CreateMesher(session, 0, Origin, p.Chunkdata[Origin]);
        var other = new ChunkCoord(1, 0, 0, 0);
        p.UploadQueue.Enqueue(TestHelpers.CreateMesher(session, 0, other, new ChunkData(other)));
        p.UnloadQueue.Enqueue(TestHelpers.CreateMesher(session, 0, other, new ChunkData(other)));
        p.PendingMeshRequests.Enqueue(other);
        p.QueuedForMeshing[other] = 1;
        p.VertexListPool.Enqueue(new List<byte>());
        p.IndexListPool.Enqueue(new List<uint>());

        session.Dispose();

        AssertPristine(session, "after Dispose");
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        var session = new EngineSession(new GameSettings());

        session.Dispose();
        var second = Record.Exception(session.Dispose);

        Assert.Null(second);
    }

    [Fact]
    public void Dispose_WithARunningScheduler_StopsWithoutThrowing()
    {
        var session = new EngineSession(new EngineContext(new GameSettings(), startJobScheduler: true, workerCores: 2));

        var exception = Record.Exception(session.Dispose);

        Assert.Null(exception);
    }

    [Fact]
    public void Dispose_RemovesTheSessionFromTheScopedContext()
    {
        var session = new EngineSession(new GameSettings());
        IDisposable scope = session.Activate();

        session.Dispose();
        scope.Dispose();

        Assert.NotSame(session.Context, EngineContext.Current);
    }

    [Fact]
    public void Constructor_WithAnExistingContext_UsesIt()
    {
        using var context = new EngineContext(new GameSettings { Seed = 77 });
        var session = new EngineSession(context);

        Assert.Same(context, session.Context);
        Assert.Equal(77, session.Settings.Seed);
        Assert.True(session.IsHeadless);
    }
}
