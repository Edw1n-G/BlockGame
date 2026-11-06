using System.Numerics;
using System.Reflection;
using Basics.Game;
using Basics.Game.Input;
using Basics.Game.Logic;
using Basics.Game.Logic.Player;
using Basics.Game.Logic.TerrainManaging;
using Basics.Graphics.UI;

namespace BlockGame.Tests;

[Collection("StaticFacades")]
public class GameLifecycleTests
{
    [Fact]
    public void PlayerDispose_RemovesStaticUiSubscription()
    {
        var field = typeof(UIManager).GetField("OnRenderDistanceChanged", BindingFlags.Static | BindingFlags.NonPublic)!;
        using var session = new EngineSession(new GameSettings());
        var player = new PlayerCharacter(Vector3.Zero, new InputManager(), session.World, session.Physics);
        bool Subscribed() => ((Delegate?)field.GetValue(null))?.GetInvocationList().Any(d => ReferenceEquals(d.Target, player)) == true;
        try { Assert.True(Subscribed()); }
        finally { player.Dispose(); }
        Assert.False(Subscribed());
    }

    [Fact]
    public void ChunkRequestor_StopWaitsForUpdateAndUnsubscribes()
    {
        var settings = new GameSettings { RenderDistance = 1, VerticalRenderDistance = 1 };
        using var session = new EngineSession(settings);
        using var player = new PlayerCharacter(Vector3.Zero, new InputManager(), session.World, session.Physics);
        var requestor = new ChunkRequestor(player, session.ChunkProvider, settings);
        player.ForceChunkUpdate();
        Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
        player.ForceChunkUpdate();
        Assert.True(requestor.Stop(TimeSpan.FromSeconds(5)));
        Assert.True(session.Context.Shutdown(TimeSpan.FromSeconds(5)));
    }
}
