using System.Numerics;
using Basics.Game;
using Basics.Game.Input;
using Basics.Game.Logic;
using Basics.Game.Logic.Player;
using Basics.Game.Logic.TerrainManaging;
using Basics.Game.Utilities;
using BlockGame.Workloads;

namespace BlockGame.Tests;

[Collection("StaticFacades")]
public class ChunkRequestorMovementTests
{
    private sealed class BlockingProvider(GameSettings settings) : ChunkProvider(NullChunkGpuMeshFactory.Instance, settings: settings)
    {
        public readonly ManualResetEventSlim Entered = new();
        public readonly ManualResetEventSlim Release = new();
        private int _first;
        public int Requests;
        public override void RequestChunk(ChunkCoord coord)
        {
            Interlocked.Increment(ref Requests);
            if (Interlocked.Exchange(ref _first, 1) == 0)
            {
                Entered.Set();
                Release.Wait(TimeSpan.FromSeconds(5));
            }
            Chunkdata.TryAdd(coord, new ChunkData(coord));
        }
    }

    private sealed class FailAfterPublishProvider(GameSettings settings) : ChunkProvider(NullChunkGpuMeshFactory.Instance, settings: settings)
    {
        private int _requests;
        public override void RequestChunk(ChunkCoord coord)
        {
            Chunkdata.TryAdd(coord,new ChunkData(coord));
            if (Interlocked.Increment(ref _requests) == 2) throw new InvalidOperationException("failure after publish");
        }
    }

    [Fact]
    public async Task ProviderFailureRollsBackPartialSelectionAndNextNotificationCanRetry()
    {
        var settings = new GameSettings { RenderDistance=1, VerticalRenderDistance=1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero,new InputManager(),session.World,session.Physics);
        using var provider = new FailAfterPublishProvider(settings);
        var requestor = new ChunkRequestor(player,provider,settings);
        try
        {
            player.ForceChunkUpdate();
            Assert.True(await TestHelpers.WaitUntil(() => requestor.LastUpdateFailure is not null));
            Assert.Empty(provider.Chunkdata);
            player.ForceChunkUpdate();
            var expected=SelectionReference.Calculate(new(0,0,0,0),1,1);
            Assert.True(await TestHelpers.WaitUntil(() => provider.Chunkdata.Keys.ToHashSet().SetEquals(expected)));
            Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
            Assert.True(provider.Chunkdata.Keys.ToHashSet().SetEquals(expected));
            Assert.Contains("failure after publish",requestor.LastUpdateFailure!.ToString());
        }
        finally { Assert.True(requestor.Stop(TimeSpan.FromSeconds(5))); }
    }

    [Fact]
    public async Task RapidNotificationsSkipObsoleteRequestsAndReachLatestTarget()
    {
        var settings = new GameSettings { RenderDistance = 1, VerticalRenderDistance = 1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero,new InputManager(),session.World,session.Physics);
        using var provider = new BlockingProvider(settings);
        var requestor = new ChunkRequestor(player,provider,settings);
        try
        {
            player.ForceChunkUpdate(); Assert.True(provider.Entered.Wait(TimeSpan.FromSeconds(5)));
            for (int i = 1; i <= 200; i++) { player.Camera.Position = new Vector3(i*16,0,i*16); player.ForceChunkUpdate(); }
            var expected = SelectionReference.Calculate(new ChunkCoord(200,0,200,0),1,1);
            provider.Release.Set();
            Assert.True(await TestHelpers.WaitUntil(() => provider.Chunkdata.Keys.ToHashSet().SetEquals(expected)));
            Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
            Assert.Equal(expected.Count+1,provider.Requests); // only the already in-flight old request, then the final set
            Assert.True(provider.Chunkdata.Keys.ToHashSet().SetEquals(expected));
        }
        finally { provider.Release.Set(); Assert.True(requestor.Stop(TimeSpan.FromSeconds(5))); provider.Entered.Dispose(); provider.Release.Dispose(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResetDuringRequestStaysEmptyUnlessNotifiedAgain(bool notifyAgain)
    {
        var settings = new GameSettings { RenderDistance = 1, VerticalRenderDistance = 1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero,new InputManager(),session.World,session.Physics);
        using var provider = new BlockingProvider(settings);
        var requestor = new ChunkRequestor(player,provider,settings);
        try
        {
            player.ForceChunkUpdate(); Assert.True(provider.Entered.Wait(TimeSpan.FromSeconds(5)));
            requestor.UnloadAllChunks();
            if (notifyAgain) player.ForceChunkUpdate();
            provider.Release.Set();
            if (notifyAgain)
            {
                var expected = SelectionReference.Calculate(new(0,0,0,0),1,1);
                Assert.True(await TestHelpers.WaitUntil(() => provider.Chunkdata.Keys.ToHashSet().SetEquals(expected)));
                Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
                Assert.True(provider.Chunkdata.Keys.ToHashSet().SetEquals(expected));
            }
            else { Assert.True(requestor.Stop(TimeSpan.FromSeconds(5))); Assert.Empty(provider.Chunkdata); }
        }
        finally { provider.Release.Set(); Assert.True(requestor.Stop(TimeSpan.FromSeconds(5))); provider.Entered.Dispose(); provider.Release.Dispose(); }
    }

    [Fact]
    public async Task ResizingAtSamePositionReconcilesPreviouslyCommittedChunks()
    {
        var settings = new GameSettings { RenderDistance = 1, VerticalRenderDistance = 1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero,new InputManager(),session.World,session.Physics);
        using var provider = new BlockingProvider(settings);
        provider.Release.Set();
        var requestor = new ChunkRequestor(player,provider,settings);
        try
        {
            player.ForceChunkUpdate();
            Assert.True(await TestHelpers.WaitUntil(() => provider.Chunkdata.Count == 7));
            foreach (var (h,v) in new[] {(3,2),(0,2),(2,0),(1,1)})
            {
                settings.RenderDistance=h; settings.VerticalRenderDistance=v; player.ForceChunkUpdate();
                var expected=SelectionReference.Calculate(new(0,0,0,0),h,v);
                Assert.True(await TestHelpers.WaitUntil(() => provider.Chunkdata.Keys.ToHashSet().SetEquals(expected)));
            }
            Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
            Assert.True(provider.Chunkdata.Keys.ToHashSet().SetEquals(SelectionReference.Calculate(new(0,0,0,0),1,1)));
        }
        finally { provider.Release.Set(); Assert.True(requestor.Stop(TimeSpan.FromSeconds(5))); provider.Entered.Dispose(); provider.Release.Dispose(); }
    }

    [Fact]
    public async Task SupersededRequestsAreUnloadedAfterMovement()
    {
        var settings = new GameSettings { RenderDistance = 1, VerticalRenderDistance = 1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero, new InputManager(), session.World, session.Physics);
        using var provider = new BlockingProvider(settings);
        var requestor = new ChunkRequestor(player, provider, settings);
        try
        {
            player.ForceChunkUpdate();
            Assert.True(provider.Entered.Wait(TimeSpan.FromSeconds(5)));
            player.Camera.Position = new Vector3(160, 0, 0);
            player.ForceChunkUpdate();
            provider.Release.Set();
            var target = player.Camera.GetChunkCoord(player.Camera.Position);
            var expected = ChunkSelection.Calculate(target, 1, 1);
            Assert.True(await TestHelpers.WaitUntil(() => provider.Chunkdata.Keys.ToHashSet().SetEquals(expected)));
        }
        finally
        {
            provider.Release.Set();
            Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
            provider.Entered.Dispose();
            provider.Release.Dispose();
        }
    }
}
