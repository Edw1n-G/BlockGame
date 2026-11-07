using Basics.Common;
using Basics.Engine;
using Basics.Graphics.UI;
using Basics.Input;
using Basics.Player;
using Basics.Settings;
using Basics.Terrain;
using Basics.Terrain.Chunks;
using Basics.Terrain.Selection;
using System.Numerics;

namespace BlockGame.Tests;

[Collection("StaticFacades")]
public class GameLifecycleTests
{
    private sealed class BlockingProvider(GameSettings settings) : ChunkProvider(NullChunkGpuMeshFactory.Instance, settings: settings)
    {
        public readonly ManualResetEventSlim Entered = new();
        public readonly ManualResetEventSlim Release = new();
        public int Requests;
        public bool FailRequest;

        public override void RequestChunk(ChunkCoord coord)
        {
            Interlocked.Increment(ref Requests);
            Entered.Set();
            Release.Wait(TimeSpan.FromSeconds(5));
            if (FailRequest) throw new InvalidOperationException("request failed");
        }
    }

    [Fact]
    public void PlayerDispose_RemovesStaticUiSubscription()
    {
        using var session = new EngineSession(new GameSettings());
        var player = new PlayerCharacter(Vector3.Zero, new InputManager(), session.World, session.Physics);
        int notifications = 0;
        player.OnChunkChanged += _ => notifications++;
        try
        {
            UIManager.NotifyRenderDistanceChanged();
            Assert.Equal(1, notifications);
        }
        finally { player.Dispose(); }
        UIManager.NotifyRenderDistanceChanged();
        Assert.Equal(1, notifications);
    }

    [Fact]
    public void ChunkRequestor_StopWaitsForUpdateAndUnsubscribes()
    {
        var settings = new GameSettings { RenderDistance = 1, VerticalRenderDistance = 1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero, new InputManager(), session.World, session.Physics);
        using var provider = new BlockingProvider(settings);
        using var entered = provider.Entered;
        using var release = provider.Release;
        var requestor = new ChunkRequestor(player, provider, settings);
        player.ForceChunkUpdate();
        try
        {
            Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
            Assert.False(requestor.Stop(TimeSpan.FromMilliseconds(50)));
        }
        finally { release.Set(); }
        Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
        int count = Volatile.Read(ref provider.Requests);
        player.ForceChunkUpdate();
        Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
        Assert.Equal(count, Volatile.Read(ref provider.Requests));
        Assert.True(session.Context.Shutdown(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public void ChunkRequestor_FaultIsExposedAfterTaskExits()
    {
        var settings = new GameSettings { RenderDistance = 1, VerticalRenderDistance = 1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero, new InputManager(), session.World, session.Physics);
        using var provider = new BlockingProvider(settings) { FailRequest = true };
        provider.Release.Set();
        var requestor = new ChunkRequestor(player, provider, settings);
        player.ForceChunkUpdate();
        Assert.True(provider.Entered.Wait(TimeSpan.FromSeconds(5)));
        Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
        Assert.Contains("request failed", requestor.LastUpdateFailure?.ToString());
    }
}
