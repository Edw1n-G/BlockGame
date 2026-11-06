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
