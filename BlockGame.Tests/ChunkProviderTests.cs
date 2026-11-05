using Basics.Game.Logic;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;

namespace BlockGame.Tests;

public class ChunkProviderTests : EngineTestBase
{
    private static readonly ChunkCoord Origin = new(0, 0, 0, 0);

    // ---------- Block access ----------

    [Fact]
    public void ModifyBlock_InAnUnloadedChunk_ReturnsFalse()
    {
        Assert.False(Session.World.SetBlock(5, 5, 5, Stone));
        Assert.Equal((ushort)0, Session.World.GetBlockAt(5, 5, 5));
    }

    [Fact]
    public void ModifyBlock_InALoadedChunk_WritesTheBlock()
    {
        AddChunk(Origin);

        Assert.True(Session.World.SetBlock(5, 6, 7, Stone));

        Assert.Equal(Stone, Session.World.GetBlockAt(5, 6, 7));
        Assert.Equal(Stone, Provider.Chunkdata[Origin].GetBlock(5, 6, 7));
        Assert.True(Provider.Chunkdata[Origin].IsDirty);
    }

    [Theory]
    [InlineData(-1, 15)]
    [InlineData(-16, 0)]
    [InlineData(-17, 15)]
    public void ModifyBlock_WithNegativeCoordinates_HitsTheRightChunkAndCell(int world, int expectedLocal)
    {
        int chunk = world >= -16 ? -1 : -2;
        var coord = new ChunkCoord(chunk, chunk, chunk, 0);
        AddChunk(coord);

        Assert.True(Session.World.SetBlock(world, world, world, Stone));

        Assert.Equal(Stone, Provider.Chunkdata[coord].GetBlock(expectedLocal, expectedLocal, expectedLocal));
        Assert.Equal(Stone, Session.World.GetBlockAt(world, world, world));
    }

    // ---------- Remeshing ----------

    [Fact]
    public void ModifyBlock_InAChunkInterior_RemeshesOnlyThatChunk()
    {
        LoadEmptyCube(2);

        Session.World.SetBlock(8, 8, 8, Stone);

        ChunkCoord queued = Assert.Single(Provider.PendingMeshRequests);
        Assert.Equal(Origin, queued);
        Assert.True(Provider.QueuedForMeshing.ContainsKey(Origin));
    }

    [Fact]
    public void ModifyBlock_OnAChunkBorder_AlsoRemeshesTheTouchingNeighbours()
    {
        LoadEmptyCube(2);

        // World (0,0,0) is local (0,0,0): it touches the -X, -Y and -Z neighbours.
        Session.World.SetBlock(0, 0, 0, Stone);

        var expected = new[]
        {
            Origin,
            new ChunkCoord(-1, 0, 0, 0),
            new ChunkCoord(0, -1, 0, 0),
            new ChunkCoord(0, 0, -1, 0),
        };
        Assert.Equal(expected.Length, Provider.PendingMeshRequests.Count);
        Assert.All(expected, c => Assert.True(Provider.QueuedForMeshing.ContainsKey(c), $"{c} should be queued"));
        Assert.DoesNotContain(new ChunkCoord(1, 0, 0, 0), Provider.PendingMeshRequests);
        Assert.DoesNotContain(new ChunkCoord(0, 1, 0, 0), Provider.PendingMeshRequests);
        Assert.DoesNotContain(new ChunkCoord(0, 0, 1, 0), Provider.PendingMeshRequests);
    }

    [Fact]
    public void ModifyBlock_OnThePositiveBorder_RemeshesThePositiveNeighbours()
    {
        LoadEmptyCube(2);

        Session.World.SetBlock(15, 15, 15, Stone);

        Assert.Equal(4, Provider.PendingMeshRequests.Count);
        Assert.Contains(new ChunkCoord(1, 0, 0, 0), Provider.PendingMeshRequests);
        Assert.Contains(new ChunkCoord(0, 1, 0, 0), Provider.PendingMeshRequests);
        Assert.Contains(new ChunkCoord(0, 0, 1, 0), Provider.PendingMeshRequests);
    }

    [Fact]
    public void RemeshChunk_WithoutAllNeighboursLoaded_DoesNothing()
    {
        AddChunk(Origin); // alone: none of its 26 neighbours exist

        Assert.True(Session.World.SetBlock(8, 8, 8, Stone)); // the edit still succeeds

        Assert.Empty(Provider.PendingMeshRequests);
        Assert.Empty(Provider.QueuedForMeshing);
    }

    [Fact]
    public void RemeshChunk_OfAnUnknownChunk_DoesNothing()
    {
        Provider.RemeshChunk(new ChunkCoord(9, 9, 9, 0));

        Assert.Empty(Provider.PendingMeshRequests);
    }

    [Fact]
    public void RemeshChunk_CalledTwice_QueuesTheChunkOnce()
    {
        LoadEmptyCube(1);

        Provider.RemeshChunk(Origin);
        Provider.RemeshChunk(Origin);

        Assert.Single(Provider.PendingMeshRequests);
    }

    // ---------- Mesh request throttling ----------

    [Fact]
    public void RequestMeshes_WhenTheUploadQueueIsFull_LeavesRequestsPending()
    {
        Session.Settings.MaxChunkMeshesInRam = 2;
        for (int i = 0; i < 2; i++)
        {
            var c = new ChunkCoord(i, 0, 0, 0);
            Provider.UploadQueue.Enqueue(CreateMesher(0, c, new ChunkData(c)));
        }

        for (int i = 0; i < 3; i++)
        {
            Provider.PendingMeshRequests.Enqueue(new ChunkCoord(i, 5, 0, 0));
        }

        Provider.RequestMeshes();

        Assert.Equal(3, Provider.PendingMeshRequests.Count);
    }

    [Fact]
    public void RequestMeshes_WhenThereIsRoom_ConsumesThePendingRequests()
    {
        Session.Settings.MaxChunkMeshesInRam = 10;
        for (int i = 0; i < 3; i++)
        {
            Provider.PendingMeshRequests.Enqueue(new ChunkCoord(i, 5, 0, 0));
        }

        Provider.RequestMeshes(); // the scheduler is not started, so the jobs just queue up

        Assert.Empty(Provider.PendingMeshRequests);
    }

    // ---------- Unloading ----------

    [Fact]
    public void UnloadChunk_RemovesTheDataAndHandsTheMeshToTheUnloadQueue()
    {
        AddChunk(Origin);
        var mesher = CreateMesher(0, Origin, Provider.Chunkdata[Origin]);
        Provider.LoadedChunks[Origin] = mesher;
        Provider.QueuedForMeshing[Origin] = 1;

        Provider.UnloadChunk(Origin);

        Assert.False(Provider.Chunkdata.ContainsKey(Origin));
        Assert.False(Provider.LoadedChunks.ContainsKey(Origin));
        Assert.False(Provider.QueuedForMeshing.ContainsKey(Origin));
        Assert.True(Provider.UnloadQueue.TryDequeue(out var queued));
        Assert.Same(mesher, queued);
        Assert.False(Provider.IsChunkLoaded(Origin));
    }

    [Fact]
    public void UnloadChunk_OfAnUnknownChunk_IsANoOp()
    {
        AddChunk(Origin);

        Provider.UnloadChunk(new ChunkCoord(4, 4, 4, 0));

        Assert.Single(Provider.Chunkdata);
        Assert.Empty(Provider.UnloadQueue);
    }

    [Fact]
    public void IsChunkLoaded_ReflectsLoadedMeshes_NotRawChunkData()
    {
        AddChunk(Origin);
        Assert.False(Provider.IsChunkLoaded(Origin));

        Provider.LoadedChunks[Origin] = CreateMesher(0, Origin, Provider.Chunkdata[Origin]);

        Assert.True(Provider.IsChunkLoaded(Origin));
        Assert.Single(Provider.GetLoadedChunks());
    }
}
