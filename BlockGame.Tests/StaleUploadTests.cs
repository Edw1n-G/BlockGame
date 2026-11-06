using Basics.Game;
using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class StaleUploadTests
{
    private static readonly ChunkCoord Coord = new(0, 0, 0, 0);

    [Fact]
    public void OldMeshPublishedAfterReload_IsRejectedAndBuffersReturnedOnce()
    {
        using var session = new EngineSession(new GameSettings());
        var provider = session.ChunkProvider;
        var oldData = new ChunkData(Coord);
        provider.Chunkdata[Coord] = oldData;
        var oldMesh = TestHelpers.CreateMesher(session, 0, Coord, oldData);
        Assert.True(provider.IsUploadCurrent(oldMesh)); // Renderer has passed its early check.

        provider.UnloadChunk(Coord);
        var newData = new ChunkData(Coord);
        provider.Chunkdata[Coord] = newData;

        Assert.False(provider.AcceptUpload(oldMesh));
        Assert.False(provider.LoadedChunks.ContainsKey(Coord));
        Assert.Single(provider.VertexListPool);
        Assert.Single(provider.IndexListPool);
        oldMesh.Dispose();
        Assert.Single(provider.VertexListPool);
        Assert.Single(provider.IndexListPool);

        var newMesh = TestHelpers.CreateMesher(session, 0, Coord, newData);
        Assert.True(provider.AcceptUpload(newMesh));
        Assert.Same(newMesh, provider.LoadedChunks[Coord]);
    }

    [Fact]
    public void UploadAcceptedBeforeUnload_IsRemovedBeforeReload()
    {
        using var session = new EngineSession(new GameSettings());
        var provider = session.ChunkProvider;
        var oldData = new ChunkData(Coord);
        provider.Chunkdata[Coord] = oldData;
        var oldMesh = TestHelpers.CreateMesher(session, 0, Coord, oldData);
        Assert.True(provider.AcceptUpload(oldMesh));

        provider.UnloadChunk(Coord);
        provider.Chunkdata[Coord] = new ChunkData(Coord);
        Assert.False(provider.LoadedChunks.ContainsKey(Coord));
        Assert.Same(oldMesh, Assert.Single(provider.UnloadQueue));
        Assert.True(provider.UnloadQueue.TryDequeue(out var retired));
        retired.Dispose();
        retired.Dispose();
        Assert.Single(provider.VertexListPool);
        Assert.Single(provider.IndexListPool);
    }

    [Fact]
    public void OldGenerationCannotPublishAfterUnloadAndNewRequest()
    {
        using var session = new EngineSession(new GameSettings());
        var provider = session.ChunkProvider;
        long first = Assert.IsType<long>(provider.BeginChunkRequest(Coord));
        provider.UnloadChunk(Coord);
        long second = Assert.IsType<long>(provider.BeginChunkRequest(Coord));
        Assert.NotEqual(first, second);

        Assert.False(provider.TryPublishChunkData(Coord, first, new ChunkData(Coord)));
        provider.CompleteChunkRequest(Coord, first);
        Assert.False(provider.Chunkdata.ContainsKey(Coord));
        var currentData = new ChunkData(Coord);
        Assert.True(provider.TryPublishChunkData(Coord, second, currentData));
        Assert.Same(currentData, provider.Chunkdata[Coord]);
    }

    [Fact]
    public async Task UnloadRacingWithPublication_NeverInstallsOldMeshIntoNewLifetime()
    {
        using var session = new EngineSession(new GameSettings());
        var provider = session.ChunkProvider;
        var oldData = new ChunkData(Coord);
        provider.Chunkdata[Coord] = oldData;
        var oldMesh = TestHelpers.CreateMesher(session, 0, Coord, oldData);
        using var start = new ManualResetEventSlim();

        var publish = Task.Run(() => { start.Wait(); provider.AcceptUpload(oldMesh); });
        var reload = Task.Run(() =>
        {
            start.Wait();
            provider.UnloadChunk(Coord);
            provider.Chunkdata[Coord] = new ChunkData(Coord);
        });
        start.Set();
        await Task.WhenAll(publish, reload).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.False(provider.LoadedChunks.ContainsKey(Coord));
        while (provider.UnloadQueue.TryDequeue(out var mesh)) mesh.Dispose();
        oldMesh.Dispose();
        Assert.Single(provider.VertexListPool);
        Assert.Single(provider.IndexListPool);
    }
}
