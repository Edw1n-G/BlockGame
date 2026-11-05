using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

/// <summary>
/// Tests that run real worker threads (generation and meshing jobs). Everything is awaited with
/// <see cref="TestHelpers.WaitUntil"/> rather than fixed sleeps. Run with
/// <c>--filter-not-trait "Category=Threaded"</c> to skip them.
/// </summary>
[Trait("Category", "Threaded")]
public class SchedulerMeshingTests
{
    private static readonly ChunkCoord Origin = new(0, 0, 0, 0);

    private static EngineSession StartSession(int seed)
        => new(new EngineContext(new GameSettings { Seed = seed }, startJobScheduler: true, workerCores: 2));

    private static void QueueMeshing(EngineSession session, ChunkCoord coord)
    {
        ushort stone = session.BlockRegistry.GetBlockId("core:stone");
        session.ChunkProvider.Chunkdata[coord] = new ChunkData(coord, TestHelpers.FloorBlocks(stone));
        session.ChunkProvider.QueuedForMeshing[coord] = 1;
        session.ChunkProvider.PendingMeshRequests.Enqueue(coord);
        session.ChunkProvider.RequestMeshes();
    }

    [Fact]
    public async Task WorkerThreads_MeshQueuedChunks_InTwoConcurrentSessions()
    {
        using var a = StartSession(1);
        using var b = StartSession(2);

        QueueMeshing(a, Origin);
        QueueMeshing(b, Origin);

        Assert.True(await TestHelpers.WaitUntil(() => a.ChunkProvider.UploadQueue.Count == 1 && a.ChunkProvider.QueuedForMeshing.IsEmpty),
            "Session A never produced its mesh.");
        Assert.True(await TestHelpers.WaitUntil(() => b.ChunkProvider.UploadQueue.Count == 1 && b.ChunkProvider.QueuedForMeshing.IsEmpty),
            "Session B never produced its mesh.");

        var meshA = Assert.Single(a.ChunkProvider.UploadQueue);
        var meshB = Assert.Single(b.ChunkProvider.UploadQueue);
        Assert.Equal(Origin, meshA.ChunkPosition);
        Assert.Equal(Origin, meshB.ChunkPosition);
        Assert.False(meshA.IsEmpty);
        Assert.False(meshB.IsEmpty);
        // Each mesh was built against its own session's provider, never the other one's.
        Assert.Same(a.ChunkProvider, meshA.Provider);
        Assert.Same(b.ChunkProvider, meshB.Provider);
    }

    [Fact]
    public async Task MeshRequests_ForChunksThatWereUnloaded_ProduceNothing()
    {
        using var session = StartSession(1);
        session.ChunkProvider.PendingMeshRequests.Enqueue(Origin); // no chunk data exists for it

        session.ChunkProvider.RequestMeshes();

        Assert.True(await TestHelpers.WaitUntil(() => session.ChunkProvider.PendingMeshRequests.IsEmpty));
        await Task.Delay(100, TestContext.Current.CancellationToken); // give a (wrong) mesh time to appear
        Assert.Empty(session.ChunkProvider.UploadQueue);
    }

    [Fact]
    public async Task RequestChunk_GeneratesTheChunkOnAWorkerThread()
    {
        using var session = StartSession(1);
        var coord = new ChunkCoord(2, 0, -3, 0);

        session.ChunkProvider.RequestChunk(coord);

        Assert.True(await TestHelpers.WaitUntil(() => session.ChunkProvider.Chunkdata.ContainsKey(coord)));
        Assert.Equal(coord, session.ChunkProvider.Chunkdata[coord].Coord);
    }

    [Fact]
    public async Task AChunk_IsQueuedForMeshing_OnlyOnceAll26NeighboursExist()
    {
        using var session = StartSession(1);
        var center = new ChunkCoord(0, 0, 0, 0);
        var corner = new ChunkCoord(-1, -1, -1, 0);

        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            session.ChunkProvider.RequestChunk(new ChunkCoord(x, y, z, 0));
        }

        Assert.True(await TestHelpers.WaitUntil(() => session.ChunkProvider.Chunkdata.Count == 27),
            "Not all 27 chunks were generated.");
        Assert.True(await TestHelpers.WaitUntil(() => session.ChunkProvider.PendingMeshRequests.Contains(center)),
            "The centre chunk (all neighbours present) was never queued for meshing.");

        // Every other chunk is missing some of its neighbours, so only the centre may be queued.
        Assert.DoesNotContain(corner, session.ChunkProvider.PendingMeshRequests);
        Assert.Single(session.ChunkProvider.PendingMeshRequests);
    }

    [Fact]
    public async Task GeneratedChunks_CanBeMeshedEndToEnd()
    {
        using var session = StartSession(1);
        var center = new ChunkCoord(0, 0, 0, 0);

        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        for (int z = -1; z <= 1; z++)
        {
            session.ChunkProvider.RequestChunk(new ChunkCoord(x, y, z, 0));
        }

        Assert.True(await TestHelpers.WaitUntil(() => session.ChunkProvider.PendingMeshRequests.Contains(center)));

        session.ChunkProvider.RequestMeshes();

        Assert.True(await TestHelpers.WaitUntil(() => session.ChunkProvider.UploadQueue.Count == 1),
            "The queued centre chunk was never meshed.");
        var mesh = Assert.Single(session.ChunkProvider.UploadQueue);
        Assert.Equal(center, mesh.ChunkPosition);
    }
}
